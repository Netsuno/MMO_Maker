using Frog.Application.Gameplay;
using Frog.Persistence.PostgreSql.Entities.Player;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

internal static class PlayerEntityMapper
{
    public static CharacterRecord ToRecord(CharacterEntity entity)
        => new(
            entity.Id,
            entity.AccountId,
            entity.DisplayName,
            entity.ClassId,
            entity.MapId,
            entity.PixelX,
            entity.PixelY,
            entity.Level,
            entity.Experience,
            entity.Hp,
            entity.MaxHp,
            entity.Mp,
            entity.MaxMp,
            entity.Gold,
            entity.BankGold,
            entity.IsDead,
            new CharacterStats(entity.Str, entity.Agi, entity.Vit, entity.Int, entity.Dex, entity.Luck),
            entity.StartingSpellId,
            entity.EquippedWeaponItemId,
            entity.EquippedArmorItemId,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);

    public static void ApplyRecord(CharacterEntity entity, CharacterRecord record)
    {
        entity.AccountId = record.AccountId;
        entity.DisplayName = record.DisplayName;
        entity.ClassId = record.ClassId;
        entity.MapId = record.MapId;
        entity.PixelX = record.PixelX;
        entity.PixelY = record.PixelY;
        entity.Level = record.Level;
        entity.Experience = record.Experience;
        entity.Hp = record.Hp;
        entity.MaxHp = record.MaxHp;
        entity.Mp = record.Mp;
        entity.MaxMp = record.MaxMp;
        entity.Gold = record.Gold;
        entity.BankGold = record.BankGold;
        entity.IsDead = record.IsDead;
        entity.Str = record.Stats.Str;
        entity.Agi = record.Stats.Agi;
        entity.Vit = record.Stats.Vit;
        entity.Int = record.Stats.Int;
        entity.Dex = record.Stats.Dex;
        entity.Luck = record.Stats.Luck;
        entity.StartingSpellId = record.StartingSpellId;
        entity.EquippedWeaponItemId = record.EquippedWeaponItemId;
        entity.EquippedArmorItemId = record.EquippedArmorItemId;
        entity.UpdatedAtUtc = record.UpdatedAtUtc;
    }

    public static GroundItemRecord ToGroundItemRecord(GroundItemEntity entity)
        => new(
            entity.Id,
            entity.MapId,
            entity.PixelX,
            entity.PixelY,
            entity.ItemId,
            entity.Quantity,
            entity.OwnerCharacterId,
            entity.CreatedAtUtc);
}
