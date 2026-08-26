using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Server.Gameplay;

namespace Frog.Persistence.IntegrationTests.Support;

public sealed record Phase7PostgresContentSeedResult(
    Guid ClassId,
    Guid SpellId,
    Guid ConsumableId,
    Guid WeaponId,
    Guid ArmorId,
    Guid MonsterId,
    Guid ShopId);

/// <summary>Publie le contenu Phase 7 minimal (Guids déterministes) dans PostgreSQL.</summary>
public static class Phase7PostgresContentSeed
{
    public static async Task<Phase7PostgresContentSeedResult> PublishAsync(FrogDbContextGate gate)
    {
        var spells = new PostgresSpellRepository(gate);
        var spellDef = Phase7ContentSeed.CreateDefaultSpell();
        await EnsurePublishedAsync(spells, spellDef);

        var classes = new PostgresClassRepository(gate, spells);
        var classDef = Phase7ContentSeed.CreateDefaultClass();
        await EnsurePublishedAsync(classes, classDef);

        var items = new PostgresItemRepository(gate);
        var consumable = Phase7ContentSeed.CreateDefaultConsumable();
        var weapon = Phase7ContentSeed.CreateDefaultWeapon();
        var armor = Phase7ContentSeed.CreateDefaultArmor();
        await EnsurePublishedAsync(items, consumable);
        await EnsurePublishedAsync(items, weapon);
        await EnsurePublishedAsync(items, armor);

        var npcs = new PostgresNpcRepository(gate);
        var monster = Phase7ContentSeed.CreateDefaultMonster();
        await EnsurePublishedAsync(npcs, monster);

        var shops = new PostgresShopRepository(gate, items);
        var shop = Phase7ContentSeed.CreateDefaultShop();
        await EnsurePublishedAsync(shops, shop);

        return new Phase7PostgresContentSeedResult(
            classDef.Id,
            spellDef.Id,
            consumable.Id,
            weapon.Id,
            armor.Id,
            monster.Id,
            shop.Id);
    }

    public static async Task<Guid> SeedGroundWeaponAsync(
        FrogDbContextGate gate,
        Guid weaponId,
        int mapId = GameplayLimits.DefaultSpawnMapId)
    {
        var ground = new PostgresGroundItemRepository(gate);
        var (pixelX, pixelY) = Frog.Core.Constants.WorldMetrics.TileCenterToPixels(
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);
        var dropped = await ground.DropAsync(mapId, pixelX, pixelY, weaponId, 1, null);
        if (dropped.Status != GroundItemMutationStatus.Ok || dropped.Item is null)
        {
            throw new InvalidOperationException("Ground weapon seed failed: " + dropped.Status);
        }

        return dropped.Item.Id;
    }

    private static async Task EnsurePublishedAsync(PostgresSpellRepository repo, Frog.Core.Models.SpellDefinition definition)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveSpellRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        if (saved is not SaveSpellResult.Success)
        {
            throw new InvalidOperationException("Spell publish failed: " + saved.GetType().Name);
        }
    }

    private static async Task EnsurePublishedAsync(PostgresClassRepository repo, Frog.Core.Models.ClassDefinition definition)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveClassRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        if (saved is not SaveClassResult.Success)
        {
            throw new InvalidOperationException("Class publish failed: " + saved.GetType().Name);
        }
    }

    private static async Task EnsurePublishedAsync(PostgresItemRepository repo, Frog.Core.Models.ItemDefinition definition)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveItemRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        if (saved is not SaveItemResult.Success)
        {
            throw new InvalidOperationException("Item publish failed: " + saved.GetType().Name);
        }
    }

    private static async Task EnsurePublishedAsync(PostgresNpcRepository repo, Frog.Core.Models.NpcDefinition definition)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveNpcRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        if (saved is not SaveNpcResult.Success)
        {
            throw new InvalidOperationException("Npc publish failed: " + saved.GetType().Name);
        }
    }

    private static async Task EnsurePublishedAsync(PostgresShopRepository repo, Frog.Core.Models.ShopDefinition definition)
    {
        if (await repo.LoadPublishedByIdAsync(definition.Id) is not null)
        {
            return;
        }

        var saved = await repo.SaveAsync(new SaveShopRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        if (saved is not SaveShopResult.Success)
        {
            throw new InvalidOperationException("Shop publish failed: " + saved.GetType().Name);
        }
    }
}
