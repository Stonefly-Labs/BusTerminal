using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Lifecycle;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 004 / FR-015 / Q5. Append-only log. Never updates or deletes.
public interface IChangeEventLog
{
    Task AppendAsync(ChangeEvent evt, CancellationToken cancellationToken);

    IAsyncEnumerable<ChangeEvent> QueryAsync(ResourceId resourceId, CancellationToken cancellationToken);
}
