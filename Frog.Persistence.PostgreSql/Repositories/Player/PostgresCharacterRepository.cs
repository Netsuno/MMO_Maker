using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresCharacterRepository : ICharacterRepository
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;

    public PostgresCharacterRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public Task<IReadOnlyList<CharacterRecord>> ListByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var entities = await db.PlayerCharacters
                .AsNoTracking()
                .Where(c => c.AccountId == accountId)
                .OrderBy(c => c.CreatedAtUtc)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return (IReadOnlyList<CharacterRecord>)entities.Select(PlayerEntityMapper.ToRecord).ToArray();
        }, cancellationToken);

    public Task<CharacterRecord?> FindByIdAsync(Guid characterId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var entity = await db.PlayerCharacters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == characterId, ct)
                .ConfigureAwait(false);
            return entity is null ? null : PlayerEntityMapper.ToRecord(entity);
        }, cancellationToken);

    public Task<bool> IsOwnedByAccountAsync(
        Guid accountId,
        Guid characterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            return await db.PlayerCharacters
                .AsNoTracking()
                .AnyAsync(c => c.Id == characterId && c.AccountId == accountId, ct)
                .ConfigureAwait(false);
        }, cancellationToken);

    public Task<CharacterCreateResult> CreateAsync(
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
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (!TryNormalizeDisplayName(displayName, out var name, out var err))
            {
                return new CharacterCreateResult(CharacterCreateStatus.InvalidName, ErrorMessage: err);
            }

            if (classId == Guid.Empty)
            {
                return new CharacterCreateResult(
                    CharacterCreateStatus.InvalidClass,
                    ErrorMessage: "Classe invalide.");
            }

            var accountExists = await db.AuthAccounts
                .AnyAsync(a => a.Id == accountId, ct)
                .ConfigureAwait(false);
            if (!accountExists)
            {
                return new CharacterCreateResult(CharacterCreateStatus.AccountNotFound);
            }

            var classExists = await db.Classes.AnyAsync(c => c.Id == classId, ct).ConfigureAwait(false);
            if (!classExists)
            {
                return new CharacterCreateResult(
                    CharacterCreateStatus.InvalidClass,
                    ErrorMessage: "Classe introuvable.");
            }

            var count = await db.PlayerCharacters
                .CountAsync(c => c.AccountId == accountId, ct)
                .ConfigureAwait(false);
            if (count >= GameplayLimits.MaxCharactersPerAccount)
            {
                return new CharacterCreateResult(
                    CharacterCreateStatus.SlotLimitReached,
                    ErrorMessage: "Nombre max. de persos atteint (8).");
            }

            var duplicate = await db.PlayerCharacters
                .AnyAsync(
                    c => c.AccountId == accountId && EF.Functions.ILike(c.DisplayName, name),
                    ct)
                .ConfigureAwait(false);
            if (duplicate)
            {
                return new CharacterCreateResult(
                    CharacterCreateStatus.DuplicateName,
                    ErrorMessage: "Ce nom de perso est deja utilise.");
            }

            var now = _clock.GetUtcNow();
            var entity = new CharacterEntity
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                DisplayName = name,
                ClassId = classId,
                MapId = mapId,
                PixelX = pixelX,
                PixelY = pixelY,
                Level = ProgressionCurve.MinLevel,
                Experience = 0,
                Hp = maxHp,
                MaxHp = maxHp,
                Mp = maxMp,
                MaxMp = maxMp,
                Gold = GameplayLimits.StartingGold,
                BankGold = 0,
                IsDead = false,
                Str = stats.Str,
                Agi = stats.Agi,
                Vit = stats.Vit,
                Int = stats.Int,
                Dex = stats.Dex,
                Luck = stats.Luck,
                StartingSpellId = startingSpellId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            db.PlayerCharacters.Add(entity);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new CharacterCreateResult(CharacterCreateStatus.Created, PlayerEntityMapper.ToRecord(entity));
        }, cancellationToken);

    public Task SaveAsync(CharacterRecord character, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            ArgumentNullException.ThrowIfNull(character);
            var entity = await db.PlayerCharacters
                .FirstOrDefaultAsync(c => c.Id == character.Id, ct)
                .ConfigureAwait(false);
            if (entity is null)
            {
                throw new InvalidOperationException($"Character {character.Id} not found.");
            }

            var updated = character with { UpdatedAtUtc = _clock.GetUtcNow() };
            PlayerEntityMapper.ApplyRecord(entity, updated);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken);

    private static bool TryNormalizeDisplayName(string? input, out string normalized, out string errorMessage)
    {
        normalized = string.Empty;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            errorMessage = "Nom vide.";
            return false;
        }

        var t = input.Trim();
        if (t.Length > 32)
        {
            errorMessage = "Nom trop long (32 caractères max).";
            return false;
        }

        foreach (var rune in t.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                continue;
            }

            if (Rune.IsLetter(rune) || Rune.IsDigit(rune) || rune.Value is '-' or '_')
            {
                continue;
            }

            errorMessage = "Nom : lettres, chiffres, espaces, tiret ou souligné uniquement.";
            return false;
        }

        normalized = t;
        return true;
    }
}
