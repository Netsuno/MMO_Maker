using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Server.Models;
using Frog.Server.Services;

namespace Frog.Server.Gameplay;

public static class SessionGameplayExtensions
{
    public static void ApplyFromCharacter(this Session session, CharacterRecord record)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(record);
        session.CharacterGuid = record.Id;
        session.CharacterId = record.Id.ToString();
        session.ClassId = record.ClassId;
        session.StartingSpellId = record.StartingSpellId;
        session.Level = record.Level;
        session.Experience = record.Experience;
        session.Hp = record.Hp;
        session.MaxHp = record.MaxHp;
        session.Mp = record.Mp;
        session.MaxMp = record.MaxMp;
        session.Gold = record.Gold;
        session.BankGold = record.BankGold;
        session.IsDead = record.IsDead;
        session.Stats = record.Stats;
        session.EquippedWeaponItemId = record.EquippedWeaponItemId;
        session.EquippedArmorItemId = record.EquippedArmorItemId;
        session.CurrentMapId = record.MapId;
        session.PixelX = record.PixelX;
        session.PixelY = record.PixelY;
        SessionPixelSync.SyncTileFromPixels(session);

        session.KnownSpellIds.Clear();
        if (record.StartingSpellId is Guid spellId && spellId != Guid.Empty)
        {
            session.KnownSpellIds.Add(spellId);
        }
    }

    public static CharacterRecord ToCharacterPatch(this Session session, CharacterRecord record)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(record);
        return record with
        {
            MapId = session.CurrentMapId,
            PixelX = session.PixelX,
            PixelY = session.PixelY,
            Level = session.Level,
            Experience = session.Experience,
            Hp = session.Hp,
            MaxHp = session.MaxHp,
            Mp = session.Mp,
            MaxMp = session.MaxMp,
            Gold = session.Gold,
            BankGold = session.BankGold,
            IsDead = session.IsDead,
            Stats = session.Stats ?? record.Stats,
            EquippedWeaponItemId = session.EquippedWeaponItemId,
            EquippedArmorItemId = session.EquippedArmorItemId,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    public static bool HasActiveCharacter(this Session session)
        => session.CharacterGuid is Guid id && id != Guid.Empty;

    public static Guid RequireCharacterGuid(this Session session)
    {
        if (session.CharacterGuid is not Guid id || id == Guid.Empty)
        {
            throw new InvalidOperationException("Aucun personnage actif.");
        }

        return id;
    }
}
