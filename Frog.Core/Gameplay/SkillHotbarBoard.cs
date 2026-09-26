using Frog.Core.Enums;
using Frog.Core.Protocol;

namespace Frog.Core.Gameplay;

/// <summary>Compétence publiée, telle que la barre de sorts peut la lier.</summary>
public readonly record struct PublishedSkillRef(
    Guid Id,
    string Name,
    int MpCost,
    int CooldownMs,
    TargetType Target);

/// <summary>Ligne du menu « lier une compétence » (libellés français).</summary>
public readonly record struct SkillHotbarMenuItem(
    string Label,
    bool Clears,
    Guid? SkillId,
    bool Enabled,
    bool Checked);

/// <summary>
/// Barre de sorts : six cases (touches 5 à 0, indices HUD 4 à 9) liées à des
/// compétences publiées (<see cref="SpellKind.Skill"/>). Les cases 1 à 4 du HUD
/// restent mêlée, sort, interaction et distance.
/// </summary>
public sealed class SkillHotbarBoard
{
    public const int SlotCount = 6;

    public const int FirstHudIndex = 4;

    public const string RemoveLabel = "Retirer la compétence";

    public const string EmptyCatalogLabel = "Aucune compétence publiée";

    private readonly Guid?[] _ids = new Guid?[SlotCount];

    private List<PublishedSkillRef> _published = new();

    /// <summary>
    /// Vrai dès qu’un enregistrement local existe ou qu’un catalogue a rempli la barre.
    /// Les cases vidées à la main ne sont pas reremplies au catalogue suivant.
    /// </summary>
    public bool IsPinned { get; private set; }

    public IReadOnlyList<PublishedSkillRef> Published => _published;

    public static bool IsSkillHudIndex(int hudIndex)
        => hudIndex >= FirstHudIndex && hudIndex < FirstHudIndex + SlotCount;

    public static int SkillSlot(int hudIndex) => hudIndex - FirstHudIndex;

    public static string DigitForHud(int hudIndex) => ((hudIndex + 1) % 10).ToString();

    public static bool NeedsNamedTarget(TargetType target)
        => target is TargetType.SingleEnemy or TargetType.SingleAlly;

    public Guid? IdAt(int skillSlot)
        => (uint)skillSlot < SlotCount ? _ids[skillSlot] : null;

    public PublishedSkillRef? ResolveSlot(int skillSlot)
    {
        if (IdAt(skillSlot) is not Guid id)
        {
            return null;
        }

        foreach (var skill in _published)
        {
            if (skill.Id == id)
            {
                return skill;
            }
        }

        return null;
    }

    public PublishedSkillRef? ResolveHud(int hudIndex)
        => IsSkillHudIndex(hudIndex) ? ResolveSlot(SkillSlot(hudIndex)) : null;

    public void Load(IReadOnlyList<string>? stored)
    {
        Array.Clear(_ids);
        IsPinned = false;
        if (stored is null || stored.Count == 0)
        {
            return;
        }

        IsPinned = true;
        var count = Math.Min(SlotCount, stored.Count);
        for (var i = 0; i < count; i++)
        {
            var text = (stored[i] ?? string.Empty).Trim();
            if (Guid.TryParse(text, out var id) && id != Guid.Empty)
            {
                _ids[i] = id;
            }
        }
    }

    /// <summary>Six identifiants si la barre est épinglée, sinon une liste vide (pas encore choisie).</summary>
    public List<string> Export()
    {
        if (!IsPinned)
        {
            return new List<string>();
        }

        var list = new List<string>(SlotCount);
        for (var i = 0; i < SlotCount; i++)
        {
            list.Add(_ids[i] is Guid id ? id.ToString("D") : string.Empty);
        }

        return list;
    }

    public void Reconcile(IReadOnlyList<PublishedSkillRef> published)
    {
        _published = published.ToList();
        var live = new HashSet<Guid>(_published.Select(static skill => skill.Id));
        for (var i = 0; i < SlotCount; i++)
        {
            if (_ids[i] is Guid id && !live.Contains(id))
            {
                _ids[i] = null;
            }
        }

        if (IsPinned || _published.Count == 0)
        {
            return;
        }

        var used = new HashSet<Guid>();
        foreach (var id in _ids)
        {
            if (id is Guid bound)
            {
                used.Add(bound);
            }
        }

        foreach (var skill in _published)
        {
            if (!used.Add(skill.Id))
            {
                continue;
            }

            var empty = -1;
            for (var i = 0; i < SlotCount; i++)
            {
                if (_ids[i] is null)
                {
                    empty = i;
                    break;
                }
            }

            if (empty < 0)
            {
                break;
            }

            _ids[empty] = skill.Id;
        }

        IsPinned = true;
    }

    /// <summary>Oublie les noms affichés sans effacer les liens (déconnexion).</summary>
    public void SuspendPresentation() => _published = new List<PublishedSkillRef>();

    public bool TryBind(int skillSlot, Guid skillId)
    {
        if ((uint)skillSlot >= SlotCount)
        {
            return false;
        }

        if (_published.All(skill => skill.Id != skillId))
        {
            return false;
        }

        for (var i = 0; i < SlotCount; i++)
        {
            if (i != skillSlot && _ids[i] == skillId)
            {
                _ids[i] = null;
            }
        }

        _ids[skillSlot] = skillId;
        IsPinned = true;
        return true;
    }

    public void Clear(int skillSlot)
    {
        if ((uint)skillSlot >= SlotCount)
        {
            return;
        }

        _ids[skillSlot] = null;
        IsPinned = true;
    }

    public string TooltipForHud(int hudIndex)
    {
        var digit = DigitForHud(hudIndex);
        var skill = ResolveHud(hudIndex);
        if (skill is not { } bound)
        {
            return $"Case {digit} — non liée";
        }

        var cost = bound.MpCost > 0 ? $"{bound.MpCost} PM" : "sans PM";
        return $"Compétence ({digit}) — {bound.Name} · {TargetTypeLabels.French(bound.Target)} · {cost}";
    }

    public IReadOnlyList<SkillHotbarMenuItem> MenuFor(int skillSlot)
    {
        var items = new List<SkillHotbarMenuItem>();
        var bound = IdAt(skillSlot);
        if (bound is Guid)
        {
            items.Add(new SkillHotbarMenuItem(RemoveLabel, Clears: true, SkillId: null, Enabled: true, Checked: false));
        }

        if (_published.Count == 0)
        {
            items.Add(new SkillHotbarMenuItem(EmptyCatalogLabel, Clears: false, SkillId: null, Enabled: false, Checked: false));
            return items;
        }

        foreach (var skill in _published)
        {
            items.Add(new SkillHotbarMenuItem(
                skill.Name,
                Clears: false,
                SkillId: skill.Id,
                Enabled: true,
                Checked: bound == skill.Id));
        }

        return items;
    }
}

/// <summary>Filtre le catalogue publié : seules les compétences (<c>kind=Skill</c>) entrent dans la barre.</summary>
public static class PublishedSkillCatalog
{
    public static bool IsSkillKind(string? kind)
        => string.Equals(kind, nameof(SpellKind.Skill), StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<PublishedSkillRef> FromWire(IEnumerable<PublishedSpellWireEntry>? spells)
    {
        if (spells is null)
        {
            return Array.Empty<PublishedSkillRef>();
        }

        var list = new List<PublishedSkillRef>();
        var seen = new HashSet<Guid>();
        foreach (var entry in spells)
        {
            if (!IsSkillKind(entry.Kind)
                || !Guid.TryParse(entry.Id, out var id)
                || id == Guid.Empty
                || !seen.Add(id)
                || string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            var target = Enum.TryParse<TargetType>(entry.TargetType, ignoreCase: false, out var parsed)
                ? parsed
                : TargetType.SingleEnemy;
            list.Add(new PublishedSkillRef(
                id,
                entry.Name.Trim(),
                Math.Max(0, entry.MpCost),
                Math.Max(0, entry.CooldownMs),
                target));
        }

        list.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return list;
    }
}
