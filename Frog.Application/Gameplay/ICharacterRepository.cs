namespace Frog.Application.Gameplay;

public sealed record CharacterStats(
    int Str,
    int Agi,
    int Vit,
    int Int,
    int Dex,
    int Luck);

public sealed record CharacterRecord(
    Guid Id,
    Guid AccountId,
    string DisplayName,
    Guid ClassId,
    int MapId,
    int PixelX,
    int PixelY,
    int Level,
    long Experience,
    int Hp,
    int MaxHp,
    int Mp,
    int MaxMp,
    int Gold,
    bool IsDead,
    CharacterStats Stats,
    Guid? StartingSpellId,
    Guid? EquippedWeaponItemId,
    Guid? EquippedArmorItemId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public enum CharacterCreateStatus
{
    Created,
    InvalidName,
    InvalidClass,
    DuplicateName,
    SlotLimitReached,
    AccountNotFound,
}

public sealed record CharacterCreateResult(
    CharacterCreateStatus Status,
    CharacterRecord? Character = null,
    string? ErrorMessage = null);

public interface ICharacterRepository
{
    Task<IReadOnlyList<CharacterRecord>> ListByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<CharacterRecord?> FindByIdAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<bool> IsOwnedByAccountAsync(
        Guid accountId,
        Guid characterId,
        CancellationToken cancellationToken = default);

    Task<CharacterCreateResult> CreateAsync(
        Guid accountId,
        string displayName,
        Guid classId,
        CharacterStats stats,
        int maxHp,
        int maxMp,
        Guid? startingSpellId,
        int mapId,
        int pixelX,
        int pixelY,
        CancellationToken cancellationToken = default);

    Task SaveAsync(CharacterRecord character, CancellationToken cancellationToken = default);
}
