using System.Text.Json;
using Frog.Core.Gameplay;

namespace Frog.Core.Events;

/// <summary>
/// Ensemble de compétences apprises par un personnage (<c>change_skills</c>).
/// Stocké en JSON de guids. Hello reste 11 : pas de nouvel opcode.
/// </summary>
public static class CharacterSkillSet
{
    public const int MaxCount = 64;

    public static HashSet<Guid> Parse(string? json)
    {
        var set = new HashSet<Guid>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return set;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return set;
            }

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String
                    && Guid.TryParse(item.GetString(), out var id)
                    && id != Guid.Empty)
                {
                    set.Add(id);
                }
            }
        }
        catch (JsonException)
        {
            return set;
        }

        return set;
    }

    public static string Format(IEnumerable<Guid> ids)
    {
        var ordered = ids
            .Where(id => id != Guid.Empty)
            .Distinct()
            .OrderBy(id => id)
            .Select(id => "\"" + id.ToString("D") + "\"");
        return "[" + string.Join(",", ordered) + "]";
    }

    public static bool TryChange(
        IReadOnlySet<Guid> current,
        string operation,
        Guid skillId,
        out HashSet<Guid> next,
        out bool changed,
        out string? error)
    {
        next = new HashSet<Guid>(current);
        changed = false;
        error = null;
        if (skillId == Guid.Empty)
        {
            error = "change_skills: skillId requis.";
            return false;
        }

        if (operation == MapEventChangeOperation.Increase)
        {
            if (next.Contains(skillId))
            {
                return true;
            }

            if (next.Count >= MaxCount)
            {
                error = "change_skills: liste pleine.";
                return false;
            }

            next.Add(skillId);
            changed = true;
            return true;
        }

        if (operation == MapEventChangeOperation.Decrease)
        {
            if (!next.Remove(skillId))
            {
                error = "change_skills: compétence absente.";
                return false;
            }

            changed = true;
            return true;
        }

        error = "change_skills: opération inconnue.";
        return false;
    }

    /// <summary>
    /// Sorts connus = sort de départ + compétences apprises. Les compétences publiées
    /// restent lançables sans être dans cet ensemble (barre de sorts existante).
    /// </summary>
    public static void CopyKnown(HashSet<Guid> known, Guid? startingSpellId, IEnumerable<Guid> learned)
    {
        ArgumentNullException.ThrowIfNull(known);
        known.Clear();
        if (startingSpellId is Guid spell && spell != Guid.Empty)
        {
            known.Add(spell);
        }

        foreach (var id in learned)
        {
            if (id != Guid.Empty)
            {
                known.Add(id);
            }
        }
    }
}

/// <summary>
/// Force l'arme ou l'armure déjà portée (<c>change_equipment</c>).
/// Arme et armure seulement. Pas de nouvel emplacement paperdoll.
/// </summary>
public static class CharacterGearChange
{
    public const string Equip = "equip";

    public const string Unequip = "unequip";

    public const string SlotWeapon = "weapon";

    public const string SlotArmor = "armor";

    public static readonly IReadOnlyList<string> Operations = [Equip, Unequip];

    public static readonly IReadOnlyList<string> Slots = [SlotWeapon, SlotArmor];

    public static bool TryCanonicalOperation(string? raw, out string operation)
    {
        operation = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (operation is Equip or Unequip)
        {
            return true;
        }

        operation = string.Empty;
        return false;
    }

    public static bool TryCanonicalSlot(string? raw, out string slot)
    {
        slot = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (slot is SlotWeapon or SlotArmor)
        {
            return true;
        }

        slot = string.Empty;
        return false;
    }

    public static bool TryApply(
        Guid? weaponItemId,
        Guid? armorItemId,
        string operation,
        string slot,
        Guid itemId,
        out Guid? nextWeapon,
        out Guid? nextArmor,
        out bool changed,
        out string? error)
    {
        nextWeapon = weaponItemId;
        nextArmor = armorItemId;
        changed = false;
        error = null;
        if (!TryCanonicalSlot(slot, out slot))
        {
            error = "change_equipment: slot weapon|armor requis.";
            return false;
        }

        var current = slot == SlotWeapon ? weaponItemId : armorItemId;
        if (operation == Equip)
        {
            if (itemId == Guid.Empty)
            {
                error = "change_equipment: itemId requis.";
                return false;
            }

            if (current == itemId)
            {
                return true;
            }

            if (slot == SlotWeapon)
            {
                nextWeapon = itemId;
            }
            else
            {
                nextArmor = itemId;
            }

            changed = true;
            return true;
        }

        if (operation == Unequip)
        {
            if (current is null)
            {
                error = "change_equipment: emplacement vide.";
                return false;
            }

            if (slot == SlotWeapon)
            {
                nextWeapon = null;
            }
            else
            {
                nextArmor = null;
            }

            changed = true;
            return true;
        }

        error = "change_equipment: opération inconnue.";
        return false;
    }

    public static EquipmentSlotKind ToSlotKind(string slot) =>
        slot == SlotArmor ? EquipmentSlotKind.Armor : EquipmentSlotKind.Weapon;
}
