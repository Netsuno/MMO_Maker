using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Microsoft.EntityFrameworkCore;
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
        services.AddSingleton<IEconomyTransactionRepository>(sp =>
            new PostgresEconomyTransactionRepository(sp.GetRequiredService<FrogDbContextGate>()));

        RegisterPublishedCatalogs(services);
    }

    private static void RegisterPublishedCatalogs(IServiceCollection services)
    {
        services.AddSingleton<PostgresSpellRepository>(sp =>
            new PostgresSpellRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<ISpellRepository>(sp => sp.GetRequiredService<PostgresSpellRepository>());
        services.AddSingleton<IPublishedSpellCatalog>(sp => sp.GetRequiredService<PostgresSpellRepository>());

        services.AddSingleton<PostgresItemRepository>(sp =>
            new PostgresItemRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<IItemRepository>(sp => sp.GetRequiredService<PostgresItemRepository>());
        services.AddSingleton<IPublishedItemCatalog>(sp => sp.GetRequiredService<PostgresItemRepository>());

        services.AddSingleton<PostgresClassRepository>(sp =>
            new PostgresClassRepository(
                sp.GetRequiredService<FrogDbContextGate>(),
                sp.GetRequiredService<ISpellRepository>()));
        services.AddSingleton<IClassRepository>(sp => sp.GetRequiredService<PostgresClassRepository>());
        services.AddSingleton<IPublishedClassCatalog>(sp => sp.GetRequiredService<PostgresClassRepository>());

        services.AddSingleton<PostgresNpcRepository>(sp =>
            new PostgresNpcRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<INpcRepository>(sp => sp.GetRequiredService<PostgresNpcRepository>());
        services.AddSingleton<IPublishedNpcCatalog>(sp => sp.GetRequiredService<PostgresNpcRepository>());

        services.AddSingleton<PostgresShopRepository>(sp =>
            new PostgresShopRepository(
                sp.GetRequiredService<FrogDbContextGate>(),
                sp.GetRequiredService<IPublishedItemCatalog>()));
        services.AddSingleton<IShopRepository>(sp => sp.GetRequiredService<PostgresShopRepository>());
        services.AddSingleton<IPublishedShopCatalog>(sp => sp.GetRequiredService<PostgresShopRepository>());
        services.AddSingleton<IShopItemReferenceCatalog>(sp => sp.GetRequiredService<PostgresShopRepository>());
    }
}

public static class PostgreSqlServerAuthBackendRegistration
{
    public static void Register() => ServerAuthBackendRegistry.SetBackend(new PostgreSqlServerAuthBackend());
}
