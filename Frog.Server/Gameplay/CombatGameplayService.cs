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
    CharacterGameplayService characterService)
{
    private readonly IPublishedNpcCatalog _npcs = npcs;
    private readonly IPublishedSpellCatalog _spells = spells;
    private readonly IPublishedItemCatalog _items = items;
    private readonly ICharacterRepository _characters = characters;
    private readonly CharacterGameplayService _characterService = characterService;
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, MonsterInstance>> _monstersByMap = new();
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
        var instance = new MonsterInstance(
            Guid.NewGuid(),
            npc.Id,
            npc.Name,
            mapId,
            pixelX,
            pixelY,
            maxHp,
            maxHp,
            npc.Level);
        var map = _monstersByMap.GetOrAdd(mapId, static _ => new ConcurrentDictionary<Guid, MonsterInstance>());
        map[instance.InstanceId] = instance;
        return instance;
    }

    public MonsterInstance? SpawnMonster(int mapId, Guid npcDefinitionId, int pixelX, int pixelY)
        => SpawnMonsterAsync(mapId, npcDefinitionId, pixelX, pixelY).GetAwaiter().GetResult();

    public IReadOnlyList<MonsterInstance> ListMonstersOnMap(int mapId)
    {
        if (!_monstersByMap.TryGetValue(mapId, out var map))
        {
            return Array.Empty<MonsterInstance>();
        }

        return map.Values.Where(m => !m.Defeated).ToArray();
    }

    public void CancelForSession(Guid sessionId) => _sessionTargets.TryRemove(sessionId, out _);

    public void CancelForMapChange(Session session)
    {
        CancelForSession(session.Id);
    }

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

        if (!_monstersByMap.TryGetValue(attacker.CurrentMapId, out var map))
        {
            return MeleeCombatResult.Fail("Aucun monstre.");
        }

        var monster = FindMonsterInRange(map.Values, targetName, attacker.PixelX, attacker.PixelY);
        if (monster is null || monster.Defeated)
        {
            return MeleeCombatResult.Fail("Monstre hors portee ou introuvable.");
        }

        var weaponPower = await GetWeaponPowerAsync(attacker.EquippedWeaponItemId, ct).ConfigureAwait(false);
        var targetVit = monster.Level * 2;
        var damage = CombatFormulas.MeleeDamage(attacker.Stats?.Str ?? 10, weaponPower, targetVit);
        var newHp = Math.Max(0, monster.Hp - damage);
        attacker.LastMeleeUtc = now;
        _sessionTargets[attacker.Id] = monster.InstanceId;

        if (newHp <= 0)
        {
            if (!map.TryRemove(monster.InstanceId, out _))
            {
                return MeleeCombatResult.Fail("Monstre deja vaincu.");
            }

            var xp = CombatFormulas.MonsterExperienceReward(monster.Level);
            await GrantExperienceAsync(attacker, xp, ct).ConfigureAwait(false);
            return MeleeCombatResult.ForMonsterKilled(targetName, damage, xp);
        }

        map[monster.InstanceId] = monster with { Hp = newHp };
        return MeleeCombatResult.ForMonsterHit(targetName, damage, newHp, monster.MaxHp);
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

        caster.Mp -= spell.ManaCost;
        if (spell.CooldownMs > 0)
        {
            caster.SpellCooldownsUtc[spellId] = now.AddMilliseconds(spell.CooldownMs);
        }

        var spellPower = CombatFormulas.SpellPowerFromManaCost(spell.ManaCost);
        var intel = caster.Stats?.Int ?? 10;

        if (!string.IsNullOrWhiteSpace(targetName)
            && _monstersByMap.TryGetValue(caster.CurrentMapId, out var map))
        {
            var monster = FindMonsterInRange(map.Values, targetName, caster.PixelX, caster.PixelY, CombatFormulas.DefaultSpellRangePixels);
            if (monster is null || monster.Defeated)
            {
                caster.Mp += spell.ManaCost;
                return SpellCombatResult.Fail("Cible hors portee.");
            }

            var targetVit = monster.Level * 2;
            var damage = CombatFormulas.SpellDamage(intel, spellPower, targetVit);
            var newHp = Math.Max(0, monster.Hp - damage);
            if (newHp <= 0)
            {
                if (!map.TryRemove(monster.InstanceId, out _))
                {
                    caster.Mp += spell.ManaCost;
                    return SpellCombatResult.Fail("Monstre deja vaincu.");
                }

                var xp = CombatFormulas.MonsterExperienceReward(monster.Level);
                await GrantExperienceAsync(caster, xp, ct).ConfigureAwait(false);
                await PersistCombatStateAsync(caster, ct).ConfigureAwait(false);
                return SpellCombatResult.ForMonsterKilled(spell.Name, damage, xp, caster.Mp);
            }

            map[monster.InstanceId] = monster with { Hp = newHp };
            await PersistCombatStateAsync(caster, ct).ConfigureAwait(false);
            return SpellCombatResult.ForMonsterHit(spell.Name, damage, newHp, monster.MaxHp, caster.Mp);
        }

        await PersistCombatStateAsync(caster, ct).ConfigureAwait(false);
        return SpellCombatResult.Cast(spell.Name, caster.Mp);
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

        session.IsDead = false;
        session.Hp = session.MaxHp;
        session.Mp = session.MaxMp;
        session.CurrentMapId = GameplayLimits.DefaultSpawnMapId;
        SessionPixelSync.SetTileCenter(
            session,
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);
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

    private static MonsterInstance? FindMonsterInRange(
        IEnumerable<MonsterInstance> monsters,
        string targetName,
        int attackerX,
        int attackerY,
        int rangePixels = CombatFormulas.BasicAttackRangePixels)
    {
        MonsterInstance? best = null;
        long bestDist = long.MaxValue;
        foreach (var m in monsters)
        {
            if (m.Defeated || !string.Equals(m.Name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dist = Frog.Core.Constants.WorldMetrics.DistanceSquaredPixels(attackerX, attackerY, m.PixelX, m.PixelY);
            if (dist <= (long)rangePixels * rangePixels && dist < bestDist)
            {
                best = m;
                bestDist = dist;
            }
        }

        return best;
    }
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
