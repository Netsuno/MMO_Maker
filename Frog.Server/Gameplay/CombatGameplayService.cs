using System.Collections.Concurrent;
using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Server.Models;
using Frog.Server.Services;

namespace Frog.Server.Gameplay;

public sealed class CombatGameplayService(
    IPublishedNpcCatalog npcs,
    IPublishedSpellCatalog spells,
    IPublishedItemCatalog items,
    ICharacterRepository characters,
    CharacterGameplayService characterService,
    ICombatMutationRepository combatMutations)
{
    private readonly IPublishedNpcCatalog _npcs = npcs;
    private readonly IPublishedSpellCatalog _spells = spells;
    private readonly IPublishedItemCatalog _items = items;
    private readonly ICharacterRepository _characters = characters;
    private readonly CharacterGameplayService _characterService = characterService;
    private readonly ICombatMutationRepository _combatMutations = combatMutations;
    private readonly ConcurrentDictionary<Guid, Guid> _sessionTargets = new();

    public async Task<MonsterInstance?> SpawnMonsterAsync(
        int mapId,
        Guid npcDefinitionId,
        int pixelX,
        int pixelY,
        CancellationToken ct = default)
    {
        var npc = await FindNpcAsync(npcDefinitionId, ct).ConfigureAwait(false);
        if (npc is null || npc.Kind != Frog.Core.Models.NpcKind.Monster)
        {
            return null;
        }

        var maxHp = CombatFormulas.MonsterMaxHp(npc.Level);
        var spawned = await _combatMutations.SpawnMonsterAsync(
            mapId,
            npc.Id,
            npc.Name,
            npc.Level,
            pixelX,
            pixelY,
            maxHp,
            ct).ConfigureAwait(false);
        return spawned is null ? null : ToMonsterInstance(spawned);
    }

    public MonsterInstance? SpawnMonster(int mapId, Guid npcDefinitionId, int pixelX, int pixelY)
        => SpawnMonsterAsync(mapId, npcDefinitionId, pixelX, pixelY).GetAwaiter().GetResult();

    public IReadOnlyList<MonsterInstance> ListMonstersOnMap(int mapId)
        => _combatMutations.ListMonstersOnMap(mapId)
            .Where(m => m.Hp > 0)
            .Select(ToMonsterInstance)
            .ToArray();

    public void CancelForSession(Guid sessionId) => _sessionTargets.TryRemove(sessionId, out _);

    public void CancelForMapChange(Session session) => CancelForSession(session.Id);

    public async Task<MeleeCombatResult> TryMeleeAttackMonsterAsync(
        Session attacker,
        string targetName,
        CancellationToken ct = default)
    {
        if (attacker.IsDead)
        {
            return MeleeCombatResult.Fail("Personnage mort.");
        }

        if (!attacker.HasActiveCharacter())
        {
            return MeleeCombatResult.Fail("Aucun personnage actif.");
        }

        var now = DateTime.UtcNow;
        if ((now - attacker.LastMeleeUtc).TotalMilliseconds < CombatFormulas.BasicAttackCooldownMs)
        {
            return MeleeCombatResult.Fail("Attaque en recharge.");
        }

        if (_combatMutations.ListMonstersOnMap(attacker.CurrentMapId).Count == 0)
        {
            return MeleeCombatResult.Fail("Aucun monstre.");
        }

        if (FindMonsterInRange(
                _combatMutations.ListMonstersOnMap(attacker.CurrentMapId),
                targetName,
                attacker.PixelX,
                attacker.PixelY) is null)
        {
            return MeleeCombatResult.Fail("Monstre hors portee ou introuvable.");
        }

        var weaponPower = await GetWeaponPowerAsync(attacker.EquippedWeaponItemId, ct).ConfigureAwait(false);
        var preview = FindMonsterInRange(
            _combatMutations.ListMonstersOnMap(attacker.CurrentMapId),
            targetName,
            attacker.PixelX,
            attacker.PixelY)!;
        var damage = CombatFormulas.MeleeDamage(attacker.Stats?.Str ?? 10, weaponPower, preview.Level * 2);
        var applied = await _combatMutations.TryApplyDamageToNamedTargetAsync(
            attacker.CurrentMapId,
            targetName,
            attacker.PixelX,
            attacker.PixelY,
            CombatFormulas.BasicAttackRangePixels,
            damage,
            ct).ConfigureAwait(false);
        if (!applied.Success)
        {
            return MeleeCombatResult.Fail(applied.Message);
        }

        attacker.LastMeleeUtc = now;
        if (applied.Monster is not null)
        {
            _sessionTargets[attacker.Id] = applied.Monster.InstanceId;
        }

        if (applied.MonsterKilled && applied.Monster is not null)
        {
            var xp = CombatFormulas.MonsterExperienceReward(applied.Monster.Level);
            await GrantExperienceAsync(attacker, xp, ct).ConfigureAwait(false);
            return MeleeCombatResult.ForMonsterKilled(targetName, applied.DamageApplied, xp);
        }

        return MeleeCombatResult.ForMonsterHit(
            targetName,
            applied.DamageApplied,
            applied.Monster!.Hp,
            applied.Monster.MaxHp);
    }

    public async Task<SpellCombatResult> TryCastSpellAsync(
        Session caster,
        Guid spellId,
        string? targetName,
        CancellationToken ct = default)
    {
        if (caster.IsDead)
        {
            return SpellCombatResult.Fail("Personnage mort.");
        }

        if (!caster.HasActiveCharacter())
        {
            return SpellCombatResult.Fail("Aucun personnage actif.");
        }

        if (!caster.KnownSpellIds.Contains(spellId))
        {
            return SpellCombatResult.Fail("Sort inconnu.");
        }

        var spell = await FindSpellAsync(spellId, ct).ConfigureAwait(false);
        if (spell is null)
        {
            return SpellCombatResult.Fail("Sort invalide.");
        }

        var now = DateTime.UtcNow;
        if (caster.SpellCooldownsUtc.TryGetValue(spellId, out var readyAt) && now < readyAt)
        {
            return SpellCombatResult.Fail("Sort en recharge.");
        }

        if (caster.Mp < spell.ManaCost)
        {
            return SpellCombatResult.Fail("Mana insuffisant.");
        }

        CombatMonsterSnapshot? previewTarget = null;
        if (!string.IsNullOrWhiteSpace(targetName))
        {
            previewTarget = FindMonsterInRange(
                _combatMutations.ListMonstersOnMap(caster.CurrentMapId),
                targetName,
                caster.PixelX,
                caster.PixelY,
                CombatFormulas.DefaultSpellRangePixels);
            if (previewTarget is null)
            {
                return SpellCombatResult.Fail("Cible hors portee.");
            }
        }

        caster.Mp -= spell.ManaCost;
        if (spell.CooldownMs > 0)
        {
            caster.SpellCooldownsUtc[spellId] = now.AddMilliseconds(spell.CooldownMs);
        }

        if (previewTarget is not null && !string.IsNullOrWhiteSpace(targetName))
        {
            var spellPower = CombatFormulas.SpellPowerFromManaCost(spell.ManaCost);
            var intel = caster.Stats?.Int ?? 10;
            var damage = CombatFormulas.SpellDamage(intel, spellPower, previewTarget.Level * 2);
            var applied = await _combatMutations.TryApplyDamageToNamedTargetAsync(
                caster.CurrentMapId,
                targetName,
                caster.PixelX,
                caster.PixelY,
                CombatFormulas.DefaultSpellRangePixels,
                damage,
                ct).ConfigureAwait(false);
            if (!applied.Success)
            {
                caster.Mp += spell.ManaCost;
                caster.SpellCooldownsUtc.Remove(spellId);
                return SpellCombatResult.Fail(applied.Message);
            }

            if (applied.MonsterKilled && applied.Monster is not null)
            {
                var xp = CombatFormulas.MonsterExperienceReward(applied.Monster.Level);
                await GrantExperienceAsync(caster, xp, ct).ConfigureAwait(false);
                await PersistCombatStateAsync(caster, ct).ConfigureAwait(false);
                return SpellCombatResult.ForMonsterKilled(spell.Name, applied.DamageApplied, xp, caster.Mp);
            }

            await PersistCombatStateAsync(caster, ct).ConfigureAwait(false);
            return SpellCombatResult.ForMonsterHit(
                spell.Name,
                applied.DamageApplied,
                applied.Monster!.Hp,
                applied.Monster.MaxHp,
                caster.Mp);
        }

        await PersistCombatStateAsync(caster, ct).ConfigureAwait(false);
        return SpellCombatResult.Cast(spell.Name, caster.Mp);
    }

    public async Task<PlayerMeleeCombatResult> TryMeleeAttackPlayerAsync(
        Session attacker,
        Session defender,
        CancellationToken ct = default)
    {
        if (attacker.IsDead)
        {
            return PlayerMeleeCombatResult.Fail("Personnage mort.");
        }

        if (defender.IsDead)
        {
            return PlayerMeleeCombatResult.Fail("Cible deja morte.");
        }

        var now = DateTime.UtcNow;
        if ((now - attacker.LastMeleeUtc).TotalMilliseconds < CombatFormulas.BasicAttackCooldownMs)
        {
            return PlayerMeleeCombatResult.Fail("Attaque en recharge.");
        }

        var weaponPower = await GetWeaponPowerAsync(attacker.EquippedWeaponItemId, ct).ConfigureAwait(false);
        var targetVit = defender.Stats?.Vit ?? 10;
        var damage = CombatFormulas.MeleeDamage(attacker.Stats?.Str ?? 10, weaponPower, targetVit);
        defender.Hp = Math.Max(0, defender.Hp - damage);
        var killed = false;
        if (defender.Hp <= 0)
        {
            defender.IsDead = true;
            defender.Hp = 0;
            killed = true;
        }

        attacker.LastMeleeUtc = now;
        await PersistCombatStateAsync(defender, ct).ConfigureAwait(false);
        return PlayerMeleeCombatResult.Hit(damage, killed);
    }

    public async Task<RespawnResult> TryRespawnAsync(Session session, CancellationToken ct = default)
    {
        if (!session.IsDead)
        {
            return RespawnResult.Fail("Pas mort.");
        }

        if (!session.HasActiveCharacter())
        {
            return RespawnResult.Fail("Aucun personnage actif.");
        }

        var pose = await _characterService.TryGetRespawnPoseAsync(ct).ConfigureAwait(false);
        if (pose is null)
        {
            return RespawnResult.Fail("Configuration de respawn invalide ou manquante.");
        }

        session.IsDead = false;
        session.Hp = session.MaxHp;
        session.Mp = session.MaxMp;
        session.CurrentMapId = pose.Value.MapId;
        session.PixelX = pose.Value.PixelX;
        session.PixelY = pose.Value.PixelY;
        SessionPixelSync.SyncTileFromPixels(session);
        await PersistCombatStateAsync(session, ct).ConfigureAwait(false);
        return RespawnResult.Ok();
    }

    public async Task PersistCombatStateAsync(Session session, CancellationToken ct = default)
    {
        if (session.CharacterGuid is not Guid characterId)
        {
            return;
        }

        var record = await _characters.FindByIdAsync(characterId, ct).ConfigureAwait(false);
        if (record is null)
        {
            return;
        }

        await _characters.SaveAsync(session.ToCharacterPatch(record), ct).ConfigureAwait(false);
    }

    private async Task GrantExperienceAsync(Session session, long xp, CancellationToken ct)
    {
        var (level, experience, levelsGained) = ProgressionCurve.ApplyExperience(session.Level, session.Experience, xp);
        session.Level = level;
        session.Experience = experience;
        if (levelsGained > 0 && session.Stats is { } stats)
        {
            var maxHp = session.MaxHp;
            var maxMp = session.MaxMp;
            var str = stats.Str;
            var agi = stats.Agi;
            var vit = stats.Vit;
            var intel = stats.Int;
            var dex = stats.Dex;
            var luck = stats.Luck;
            ProgressionCurve.ApplyLevelUpBonuses(ref maxHp, ref maxMp, ref str, ref agi, ref vit, ref intel, ref dex, ref luck, levelsGained);
            session.MaxHp = maxHp;
            session.MaxMp = maxMp;
            session.Stats = new CharacterStats(str, agi, vit, intel, dex, luck);
            session.Hp = maxHp;
            session.Mp = maxMp;
        }

        session.LastExperienceGain = xp;
        await PersistCombatStateAsync(session, ct).ConfigureAwait(false);
    }

    private async Task<int> GetWeaponPowerAsync(Guid? weaponItemId, CancellationToken ct)
    {
        if (weaponItemId is not Guid id)
        {
            return 0;
        }

        var item = await _items.LoadPublishedByIdAsync(id, ct).ConfigureAwait(false);
        return item is null ? 0 : Math.Max(1, item.BuyPrice / 10);
    }

    private async Task<Frog.Core.Models.NpcDefinition?> FindNpcAsync(Guid npcId, CancellationToken ct)
    {
        var published = await _npcs.ListPublishedAsync(ct).ConfigureAwait(false);
        return published.FirstOrDefault(n => n.Id == npcId);
    }

    private async Task<Frog.Core.Models.SpellDefinition?> FindSpellAsync(Guid spellId, CancellationToken ct)
    {
        var published = await _spells.ListPublishedAsync(ct).ConfigureAwait(false);
        return published.FirstOrDefault(s => s.Id == spellId);
    }

    private static CombatMonsterSnapshot? FindMonsterInRange(
        IEnumerable<CombatMonsterSnapshot> monsters,
        string targetName,
        int attackerX,
        int attackerY,
        int rangePixels = CombatFormulas.BasicAttackRangePixels)
    {
        CombatMonsterSnapshot? best = null;
        long bestDist = long.MaxValue;
        foreach (var monster in monsters)
        {
            if (monster.Hp <= 0
                || !string.Equals(monster.Name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dist = Frog.Core.Constants.WorldMetrics.DistanceSquaredPixels(attackerX, attackerY, monster.PixelX, monster.PixelY);
            if (dist <= (long)rangePixels * rangePixels && dist < bestDist)
            {
                best = monster;
                bestDist = dist;
            }
        }

        return best;
    }

    private static MonsterInstance ToMonsterInstance(CombatMonsterSnapshot snapshot)
        => new(
            snapshot.InstanceId,
            snapshot.NpcDefinitionId,
            snapshot.Name,
            snapshot.MapId,
            snapshot.PixelX,
            snapshot.PixelY,
            snapshot.Hp,
            snapshot.MaxHp,
            snapshot.Level,
            snapshot.Hp <= 0);
}

public sealed record MonsterInstance(
    Guid InstanceId,
    Guid NpcDefinitionId,
    string Name,
    int MapId,
    int PixelX,
    int PixelY,
    int Hp,
    int MaxHp,
    int Level,
    bool Defeated = false);

public sealed record MeleeCombatResult(
    bool Success,
    bool HitMonster,
    bool MonsterKilled,
    string TargetName,
    string Message,
    int Damage,
    long ExperienceGained = 0,
    int TargetHp = 0,
    int TargetMaxHp = 0)
{
    public static MeleeCombatResult Fail(string message)
        => new(false, false, false, string.Empty, message, 0);

    public static MeleeCombatResult ForMonsterHit(string name, int damage, int hp, int maxHp)
        => new(true, true, false, name, "Touche.", damage, 0, hp, maxHp);

    public static MeleeCombatResult ForMonsterKilled(string name, int damage, long xp)
        => new(true, true, true, name, "Monstre vaincu.", damage, xp);
}

public sealed record SpellCombatResult(
    bool Success,
    bool HitMonster,
    bool MonsterKilled,
    string SpellName,
    string Message,
    int Damage,
    long ExperienceGained = 0,
    int RemainingMp = 0,
    int TargetHp = 0,
    int TargetMaxHp = 0)
{
    public static SpellCombatResult Fail(string message)
        => new(false, false, false, string.Empty, message, 0);

    public static SpellCombatResult Cast(string spellName, int mp)
        => new(true, false, false, spellName, "Sort lance.", 0, 0, mp);

    public static SpellCombatResult ForMonsterHit(string spellName, int damage, int hp, int maxHp, int mp)
        => new(true, true, false, spellName, "Touche.", damage, 0, mp, hp, maxHp);

    public static SpellCombatResult ForMonsterKilled(string spellName, int damage, long xp, int mp)
        => new(true, true, true, spellName, "Monstre vaincu.", damage, xp, mp);
}

public sealed record RespawnResult(bool Success, string Message)
{
    public static RespawnResult Ok() => new(true, "Ressuscite.");
    public static RespawnResult Fail(string message) => new(false, message);
}

public sealed record PlayerMeleeCombatResult(bool Success, string Message, int Damage, bool TargetKilled)
{
    public static PlayerMeleeCombatResult Fail(string message)
        => new(false, message, 0, false);

    public static PlayerMeleeCombatResult Hit(int damage, bool killed)
        => new(true, killed ? "Cible vaincue." : "Touche.", damage, killed);
}
