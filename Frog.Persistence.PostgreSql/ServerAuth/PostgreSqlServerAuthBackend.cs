using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Application.Maps;
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
        services.AddSingleton<IInventoryTransferRepository>(sp =>
            new PostgresInventoryTransferRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<IMonsterKillRewardRepository>(sp =>
            new PostgresMonsterKillRewardRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<ICharacterWorldStateRepository>(sp =>
            new PostgresCharacterWorldStateRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<ICharacterQuestRepository>(sp =>
            new PostgresCharacterQuestRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<ICharacterProfessionRepository>(sp =>
            new PostgresCharacterProfessionRepository(sp.GetRequiredService<FrogDbContextGate>()));

        services.AddSingleton<PostgresPhase8PublishedCatalogs>(sp =>
            new PostgresPhase8PublishedCatalogs(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<IPublishedDialogueCatalog>(sp => sp.GetRequiredService<PostgresPhase8PublishedCatalogs>());
        services.AddSingleton<IPublishedQuestCatalog>(sp => sp.GetRequiredService<PostgresPhase8PublishedCatalogs>());
        services.AddSingleton<IPublishedCommonEventCatalog>(sp => sp.GetRequiredService<PostgresPhase8PublishedCatalogs>());
        services.AddSingleton<IPublishedProfessionCatalog>(sp => sp.GetRequiredService<PostgresPhase8PublishedCatalogs>());
        services.AddSingleton<IPublishedRecipeCatalog>(sp => sp.GetRequiredService<PostgresPhase8PublishedCatalogs>());
        services.AddSingleton<IPublishedRegionCatalog>(sp => sp.GetRequiredService<PostgresPhase8PublishedCatalogs>());
        services.AddSingleton<IPublishedWeatherCatalog>(sp => sp.GetRequiredService<PostgresPhase8PublishedCatalogs>());
        services.AddSingleton<IPhase8ContentEditorRepository>(sp => sp.GetRequiredService<PostgresPhase8PublishedCatalogs>());
        services.AddSingleton<IEventCraftRepository>(sp =>
            new PostgresEventCraftRepository(
                sp.GetRequiredService<FrogDbContextGate>(),
                sp.GetRequiredService<IPublishedRecipeCatalog>(),
                sp.GetRequiredService<IPublishedItemCatalog>(),
                sp.GetRequiredService<IPublishedProfessionCatalog>()));
        services.AddSingleton<IQuestMutationRepository>(sp =>
            new PostgresQuestMutationRepository(
                sp.GetRequiredService<FrogDbContextGate>(),
                sp.GetRequiredService<IPublishedQuestCatalog>(),
                sp.GetRequiredService<IPublishedItemCatalog>()));
        services.AddSingleton<IMapEventMutationRepository>(sp =>
            new PostgresMapEventMutationRepository(
                sp.GetRequiredService<FrogDbContextGate>(),
                sp.GetRequiredService<IPublishedItemCatalog>()));

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

        services.AddSingleton<PostgresMapEventRepository>(sp =>
            new PostgresMapEventRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<IMapEventRepository>(sp => sp.GetRequiredService<PostgresMapEventRepository>());
        services.AddSingleton<IPublishedMapEventCatalog>(sp => sp.GetRequiredService<PostgresMapEventRepository>());
        services.AddSingleton<IPublishedMapEventPlacementCatalog>(sp => sp.GetRequiredService<PostgresMapEventRepository>());

        services.AddSingleton<PostgresMapRepository>(sp =>
            new PostgresMapRepository(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<IMapRepository>(sp => sp.GetRequiredService<PostgresMapRepository>());
        services.AddSingleton<PostgresPublishedWorldCatalog>(sp =>
            new PostgresPublishedWorldCatalog(sp.GetRequiredService<FrogDbContextGate>()));
        services.AddSingleton<IPublishedWorldCatalog>(sp => sp.GetRequiredService<PostgresPublishedWorldCatalog>());
    }
}

public static class PostgreSqlServerAuthBackendRegistration
{
    public static void Register() => ServerAuthBackendRegistry.SetBackend(new PostgreSqlServerAuthBackend());
}
