using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Server.Config;
using Frog.Server.Database;
using Microsoft.Extensions.Options;

namespace Frog.Server.Gameplay;

/// <summary>Création / sélection personnage à partir du contenu publié (classes).</summary>
public sealed class CharacterGameplayService(
    ICharacterRepository characters,
    IPublishedClassCatalog classes,
    IInventoryRepository inventory,
    IPublishedWorldCatalog world,
    IOptions<Phase7ContentOptions> contentOptions)
{
    private readonly ICharacterRepository _characters = characters;
    private readonly IPublishedClassCatalog _classes = classes;
    private readonly IInventoryRepository _inventory = inventory;
    private readonly IPublishedWorldCatalog _world = world;
    private readonly Phase7ContentOptions _contentOptions = contentOptions.Value;

    public Task<IReadOnlyList<CharacterRecord>> ListAsync(Guid accountId, CancellationToken ct = default)
        => _characters.ListByAccountAsync(accountId, ct);

    public Task<CharacterRecord?> FindAsync(Guid characterId, CancellationToken ct = default)
        => _characters.FindByIdAsync(characterId, ct);

    public Task<bool> IsOwnedAsync(Guid accountId, Guid characterId, CancellationToken ct = default)
        => _characters.IsOwnedByAccountAsync(accountId, characterId, ct);

    public async Task<CharacterCreateResult> CreateAsync(
        Guid accountId,
        string displayName,
        Guid classId,
        CancellationToken ct = default)
    {
        if (!CharacterDisplayNameRules.TryNormalize(displayName, out var name, out var err))
        {
            return new CharacterCreateResult(CharacterCreateStatus.InvalidName, ErrorMessage: err);
        }

        var published = await _classes.ListPublishedAsync(ct).ConfigureAwait(false);
        var klass = published.FirstOrDefault(c => c.Id == classId);
        if (klass is null)
        {
            if (_contentOptions.AllowSyntheticContentFallback
                && published.Count == 0
                && classId == Phase7ContentSeed.DefaultClassId)
            {
                klass = Phase7ContentSeed.CreateDefaultClass();
            }
            else
            {
                return new CharacterCreateResult(
                    CharacterCreateStatus.InvalidClass,
                    ErrorMessage: "Classe non publiee.");
            }
        }

        int mapId;
        int pixelX;
        int pixelY;
        try
        {
            if (_contentOptions.RequirePublishedWorld)
            {
                var spawn = await _world.GetSpawnConfigAsync(ct).ConfigureAwait(false);
                mapId = spawn.StartRuntimeMapId;
                (pixelX, pixelY) = WorldMetrics.TileCenterToPixels(spawn.StartTileX, spawn.StartTileY);
            }
            else
            {
                mapId = GameplayLimits.DefaultSpawnMapId;
                (pixelX, pixelY) = WorldMetrics.TileCenterToPixels(
                    GameplayLimits.DefaultSpawnTileX,
                    GameplayLimits.DefaultSpawnTileY);
            }
        }
        catch (Exception ex)
        {
            return new CharacterCreateResult(
                CharacterCreateStatus.InvalidClass,
                ErrorMessage: "Carte de depart invalide: " + ex.Message);
        }

        var stats = new CharacterStats(klass.Str, klass.Agi, klass.Vit, klass.Int, klass.Dex, klass.Luck);
        var result = await _characters.CreateAsync(
            accountId,
            name,
            klass.Id,
            stats,
            klass.BaseHp,
            klass.BaseMp,
            klass.StartingSpellId,
            mapId,
            pixelX,
            pixelY,
            ct).ConfigureAwait(false);

        if (result.Status == CharacterCreateStatus.Created && result.Character is not null)
        {
            await _inventory.GetAsync(result.Character.Id, ct).ConfigureAwait(false);
        }

        return result;
    }

    public async Task SavePoseAsync(CharacterRecord character, int mapId, int pixelX, int pixelY, CancellationToken ct = default)
    {
        var updated = character with
        {
            MapId = mapId,
            PixelX = pixelX,
            PixelY = pixelY,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        await _characters.SaveAsync(updated, ct).ConfigureAwait(false);
    }

    public Task SaveAsync(CharacterRecord character, CancellationToken ct = default)
        => _characters.SaveAsync(character, ct);

    public async Task<(int MapId, int PixelX, int PixelY)?> TryGetRespawnPoseAsync(CancellationToken ct = default)
    {
        if (!_contentOptions.RequirePublishedWorld)
        {
            var (px, py) = WorldMetrics.TileCenterToPixels(
                GameplayLimits.DefaultSpawnTileX,
                GameplayLimits.DefaultSpawnTileY);
            return (GameplayLimits.DefaultSpawnMapId, px, py);
        }

        try
        {
            var spawn = await _world.GetSpawnConfigAsync(ct).ConfigureAwait(false);
            var (px, py) = WorldMetrics.TileCenterToPixels(spawn.RespawnTileX, spawn.RespawnTileY);
            return (spawn.RespawnRuntimeMapId, px, py);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Contenu publié minimal pour playtest / E2E sans éditeur.</summary>
public static class Phase7ContentSeed
{
    public static readonly Guid DefaultClassId = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000001");
    public static readonly Guid DefaultSpellId = Guid.Parse("aaaaaaaa-0002-4000-8000-000000000001");
    public static readonly Guid DefaultItemId = Guid.Parse("aaaaaaaa-0003-4000-8000-000000000001");
    public static readonly Guid DefaultWeaponId = Guid.Parse("aaaaaaaa-0003-4000-8000-000000000002");
    public static readonly Guid DefaultArmorId = Guid.Parse("aaaaaaaa-0003-4000-8000-000000000003");
    public static readonly Guid DefaultMonsterId = Guid.Parse("aaaaaaaa-0004-4000-8000-000000000001");
    public static readonly Guid DefaultShopId = Guid.Parse("aaaaaaaa-0005-4000-8000-000000000001");

    public static ClassDefinition CreateDefaultClass() => new()
    {
        Id = DefaultClassId,
        Name = "Aventurier",
        Description = "Classe de départ Phase 7",
        BaseHp = 100,
        BaseMp = 50,
        Str = 10,
        Agi = 10,
        Vit = 10,
        Int = 10,
        Dex = 10,
        Luck = 10,
        StartingSpellId = DefaultSpellId,
    };

    public static SpellDefinition CreateDefaultSpell() => new()
    {
        Id = DefaultSpellId,
        Name = "Éclair",
        Kind = SpellKind.Spell,
        ManaCost = 8,
        CooldownMs = 1200,
        TargetType = TargetType.SingleEnemy,
        IconLogicalPath = "icons/spells/spark.png",
        Description = "Sort essentiel Phase 7",
    };

    public static ItemDefinition CreateDefaultConsumable() => new()
    {
        Id = DefaultItemId,
        Name = "Potion",
        Kind = ItemType.Consumable,
        IconLogicalPath = "icons/items/potion.png",
        MaxStack = 20,
        BuyPrice = 25,
        SellPrice = 10,
        Description = "Potion de test",
    };

    public static ItemDefinition CreateDefaultWeapon() => new()
    {
        Id = DefaultWeaponId,
        Name = "Épée courte",
        Kind = ItemType.Weapon,
        IconLogicalPath = "icons/items/sword.png",
        MaxStack = 1,
        BuyPrice = 100,
        SellPrice = 40,
        Description = "Arme de départ",
    };

    public static ItemDefinition CreateDefaultArmor() => new()
    {
        Id = DefaultArmorId,
        Name = "Tunique",
        Kind = ItemType.Armor,
        IconLogicalPath = "icons/items/tunic.png",
        MaxStack = 1,
        BuyPrice = 80,
        SellPrice = 30,
        Description = "Armure légère",
    };

    public static NpcDefinition CreateDefaultMonster() => new()
    {
        Id = DefaultMonsterId,
        Name = "Slime",
        Kind = NpcKind.Monster,
        SpriteLogicalPath = "sprites/npcs/slime.png",
        Level = 1,
        Notes = "Monstre Phase 7",
    };

    public static ShopDefinition CreateDefaultShop() => new()
    {
        Id = DefaultShopId,
        Name = "Échoppe",
        Description = "Boutique Phase 7",
        Listings =
        [
            new ShopListing { ItemId = DefaultItemId, Price = 25, Stock = null },
            new ShopListing { ItemId = DefaultWeaponId, Price = 100, Stock = null },
        ],
    };
}
