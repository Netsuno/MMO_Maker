using Frog.Application.Identity;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Frog.Persistence.PostgreSql.ServerAuth;

public sealed class PostgreSqlServerAuthBackend : IServerAuthBackend
{
    public void Register(IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddSingleton(new PostgreSqlAuthScopeHolder(new PostgreSqlAuthScope(connectionString)));
        services.AddSingleton<IAccountRepository>(sp =>
            new PostgresAccountRepository(sp.GetRequiredService<PostgreSqlAuthScopeHolder>().Scope!.Gate));
        services.AddSingleton<IAuthSessionRepository>(sp =>
            new PostgresAuthSessionRepository(sp.GetRequiredService<PostgreSqlAuthScopeHolder>().Scope!.Gate));
    }
}

public static class PostgreSqlServerAuthBackendRegistration
{
    public static void Register() => ServerAuthBackendRegistry.SetBackend(new PostgreSqlServerAuthBackend());
}
