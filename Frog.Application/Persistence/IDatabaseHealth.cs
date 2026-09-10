namespace Frog.Application.Persistence;

public sealed record DatabaseHealthResult(bool Ok, string Detail);

public interface IDatabaseHealth
{
    Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken = default);
}
