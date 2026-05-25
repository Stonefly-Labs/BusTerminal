using BusTerminal.Api.Domain;
using BusTerminal.Api.Domain.Lifecycle;

namespace BusTerminal.Api.Infrastructure.Persistence;

// Spec 004 / FR-015 / Q5. Append-only log. Never updates or deletes.
public interface IChangeEventLog
{
    Task AppendAsync(ChangeEvent evt, CancellationToken cancellationToken);

    IAsyncEnumerable<ChangeEvent> QueryAsync(ResourceId resourceId, CancellationToken cancellationToken);

    // Spec 004 / T143 (US8). Cross-partition export. Ordered by timestamp
    // ascending across all resources. Used by the load-fixtures `export`
    // subcommand when `--include-change-log` is set.
    IAsyncEnumerable<ChangeEvent> QueryAllAsync(CancellationToken cancellationToken);
}
