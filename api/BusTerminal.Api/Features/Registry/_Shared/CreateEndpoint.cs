using System.Text.Json;
using BusTerminal.Api.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BusTerminal.Api.Features.Registry.Shared;

// Spec 006 / T075 / FR-001..FR-014. POST /api/registry — polymorphic create
// for every RegistryEntityType. Validation pipeline:
//   1) Type-discriminator + per-type FluentValidator dispatch
//   2) ParentExistenceRule (FR-008) — parent must exist in same env
//   3) DuplicateNameRule (FR-014) — no two siblings share the same name
//   4) Compute fullyQualifiedName (server-side, read-only from client)
//   5) Persist via IRegistryEntityStore
//   6) Write Created audit event (Story 1 AC #7: prefix with
//      `UNDER_DEPRECATED_PARENT:` when the parent is Deprecated)
public static class CreateEndpoint
{
    public static RouteGroupBuilder MapCreateRegistryEntity(this RouteGroupBuilder group)
    {
        group.MapPost("", HandleAsync).WithName("RegistryCreate");
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        JsonElement body,
        IRegistryEntityStore store,
        IAuditEventStore auditStore,
        RegistryValidatorDispatcher validators,
        RegistryDtoMapping mapping,
        IPlatformPrincipalAccessor principalAccessor,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        CreateEntityRequest? request;
        try
        {
            request = body.Deserialize<CreateEntityRequest>(RegistryJsonOptions.Default);
        }
        catch (JsonException ex)
        {
            return RegistryProblemFactory.BadRequest(
                "MalformedJson", "Malformed JSON body", ex.Message, context.Request.Path);
        }

        if (request is null)
        {
            return RegistryProblemFactory.BadRequest(
                "EmptyBody", "Empty body", "Request body is required.", context.Request.Path);
        }

        // Parent existence + Deprecated-parent detection (Story 1 AC #7).
        RegistryEntity? parent = null;
        if (request.ParentId.HasValue)
        {
            var expectedParent = ExpectedParentType(request.EntityType);
            if (expectedParent is null)
            {
                return RegistryProblemFactory.BadRequest(
                    "InvalidParent",
                    "Invalid parent reference",
                    $"{request.EntityType} entities must not declare a parentId.",
                    context.Request.Path);
            }
            parent = await store.FindParentAsync(
                request.ParentId.Value, expectedParent.Value, request.Environment, cancellationToken)
                .ConfigureAwait(false);
            if (parent is null)
            {
                return RegistryProblemFactory.BadRequest(
                    "ParentNotFound",
                    "Parent not found",
                    $"Parent of expected type {expectedParent.Value} with id {request.ParentId.Value} was not found in environment '{request.Environment}'.",
                    context.Request.Path);
            }
        }

        // Compute FQN with the resolved parent chain.
        var fqn = await ComputeFqnAsync(store, request.EntityType, request.Name, parent, request.Environment, cancellationToken)
            .ConfigureAwait(false);
        var nsName = ResolveNamespaceName(request.EntityType, request.Name, parent, fqn);

        var now = timeProvider.GetUtcNow();
        var entity = EntityMaterializer.FromCreateRequest(request, now, fqn, nsName);

        // Normalize tags (case-insensitive key match — first-write wins for casing).
        var normalizedTags = mapping.NormalizeTags(entity.Tags, persisted: null);
        entity = entity with { Tags = normalizedTags };

        // Validate via per-type dispatcher.
        var validation = await validators.ValidateAsync(entity, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return RegistryProblemFactory.ValidationProblem(validation, context.Request.Path);
        }

        // Duplicate name (FR-014) — scope: parentId + entityType + name within env.
        var duplicate = await store.FindByParentAndNameAsync(
            entity.ParentId, entity.EntityType, entity.Name, entity.Environment, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate is not null)
        {
            return RegistryProblemFactory.Conflict(
                "DuplicateName",
                "Duplicate name within parent scope",
                $"An entity of type {entity.EntityType} named '{entity.Name}' already exists under parent {entity.ParentId} in environment '{entity.Environment}'.",
                context.Request.Path);
        }

        var created = await store.CreateAsync(entity, cancellationToken).ConfigureAwait(false);

        var changeSummary = $"Created {created.EntityType} '{created.Name}'"
            + (created.NamespaceName is null
                ? string.Empty
                : $" under namespace '{created.NamespaceName}'");
        var audit = RegistryAuditFactory.Build(
            created, AuditEventType.Created, changeSummary,
            principalAccessor, timeProvider,
            parentIsDeprecated: parent?.Status == RegistryEntityStatus.Deprecated);
        await auditStore.WriteAsync(audit, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(created.Etag))
        {
            context.Response.Headers.ETag = created.Etag;
        }
        context.Response.Headers.Location = $"/api/registry/{created.Id:D}";
        return Results.Json(created, RegistryJsonOptions.Default, statusCode: 201);
    }

    internal static RegistryEntityType? ExpectedParentType(RegistryEntityType entityType) => entityType switch
    {
        RegistryEntityType.Namespace => null,
        RegistryEntityType.Queue => RegistryEntityType.Namespace,
        RegistryEntityType.Topic => RegistryEntityType.Namespace,
        RegistryEntityType.Subscription => RegistryEntityType.Topic,
        RegistryEntityType.Rule => RegistryEntityType.Subscription,
        _ => null,
    };

    internal static async Task<string> ComputeFqnAsync(
        IRegistryEntityStore store,
        RegistryEntityType entityType,
        string name,
        RegistryEntity? parent,
        string environment,
        CancellationToken cancellationToken)
    {
        // Walk the parent chain up so we can produce the full path. The
        // explorer/detail UI surfaces FQN; we compute server-side so the
        // wire shape is always authoritative.
        switch (entityType)
        {
            case RegistryEntityType.Namespace:
                return name;

            case RegistryEntityType.Queue:
            case RegistryEntityType.Topic:
                // Parent is the namespace.
                return parent is null ? name : $"{parent.Name}/{name}";

            case RegistryEntityType.Subscription:
                {
                    if (parent is null) return name;
                    var topicNs = await store.GetAsync(parent.ParentId ?? Guid.Empty, environment, cancellationToken).ConfigureAwait(false);
                    return topicNs is null
                        ? $"{parent.Name}/{name}"
                        : $"{topicNs.Name}/{parent.Name}/{name}";
                }

            case RegistryEntityType.Rule:
                {
                    if (parent is null) return name; // subscription
                    var topic = parent.ParentId.HasValue
                        ? await store.GetAsync(parent.ParentId.Value, environment, cancellationToken).ConfigureAwait(false)
                        : null;
                    var nsEntity = topic?.ParentId.HasValue == true
                        ? await store.GetAsync(topic.ParentId.Value, environment, cancellationToken).ConfigureAwait(false)
                        : null;
                    var segments = new List<string>();
                    if (nsEntity is not null) segments.Add(nsEntity.Name);
                    if (topic is not null) segments.Add(topic.Name);
                    segments.Add(parent.Name);
                    segments.Add(name);
                    return string.Join("/", segments);
                }

            default:
                return name;
        }
    }

    private static string? ResolveNamespaceName(
        RegistryEntityType entityType,
        string name,
        RegistryEntity? parent,
        string fullyQualifiedName)
    {
        // Namespace echoes its own name. Children carry the root namespace
        // name so the search index and explorer can group them.
        if (entityType == RegistryEntityType.Namespace) return name;
        if (parent is null) return null;
        // For Queue/Topic the parent IS the namespace. For deeper children
        // the FQN's first segment is the namespace name.
        if (parent.EntityType == RegistryEntityType.Namespace) return parent.Name;
        var idx = fullyQualifiedName.IndexOf('/');
        return idx > 0 ? fullyQualifiedName[..idx] : parent.NamespaceName;
    }
}
