using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Microsoft.Extensions.DependencyInjection;

namespace Frog.Persistence.PostgreSql.ServerAuth;

public sealed class PostgreSqlServerAuthBackend : IServerAuthBackend
{
    public void Register(IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddSingleton(new PostgreSqlAuthScopeHolder(new PostgreSqlAuthScope(connectionString)));
        services.AddSingleton(sp =>
            sp.GetRequiredService<PostgreSqlAuthScopeHolder>().Scope!.Gate);
        services.AddSingleton<IAccountRepository>(sp =>
            new PostgresAccountRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<IAuthSessionRepository>(sp =>
            new PostgresAuthSessionRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<ICharacterRepository>(sp =>
            new PostgresCharacterRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<IInventoryRepository>(sp =>
            new PostgresInventoryRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<IEquipmentRepository>(sp =>
            new PostgresEquipmentRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<IGroundItemRepository>(sp =>
            new PostgresGroundItemRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<IBankRepository>(sp =>
            new PostgresBankRepository(sp.GetRequiredService<FrogDbContextGate>()));
    }
}

public static class PostgreSqlServerAuthBackendRegistration
{
    public static void Register() => ServerAuthBackendRegistry.SetBackend(new PostgreSqlServerAuthBackend());
}
