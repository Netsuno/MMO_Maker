using Frog.Persistence.PostgreSql;

namespace Frog.Persistence.PostgreSql.ServerAuth;

public sealed class PostgreSqlAuthScope : IDisposable
{
    public PostgreSqlAuthScope(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        Gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connectionString)));
    }

    public FrogDbContextGate Gate { get; }

    public void Dispose() => Gate.Dispose();
}

public sealed class PostgreSqlAuthScopeHolder : IDisposable
{
    public PostgreSqlAuthScope? Scope { get; }

    public PostgreSqlAuthScopeHolder(PostgreSqlAuthScope? scope) => Scope = scope;

    public void Dispose() => Scope?.Dispose();
}
