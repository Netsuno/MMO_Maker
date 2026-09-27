using Frog.Core.Gameplay;
using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// Applique <c>recover_all</c> et <c>change_hp_mp</c> sur les PV/PM courants.
/// Pas de troupes, de variable, ni de base d'états : le KO et les statuts restent inchangés.
/// </summary>
public static class CharacterVitalCommands
{
    public const string VitalHp = "HP";

    public const string VitalMp = "MP";

    public const int MaxAmount = CharacterProgressionAdjust.MaxVital;

    public static readonly IReadOnlyList<string> VitalKeys = [VitalHp, VitalMp];

    public static bool IsVital(string? discriminator) =>
        discriminator is MapEventCommandDiscriminators.RecoverAll
            or MapEventCommandDiscriminators.ChangeHpMp;

    public static bool TryCanonicalVital(string? raw, out string vital)
    {
        vital = raw?.Trim() ?? string.Empty;
        if (vital is VitalHp or VitalMp)
        {
            return true;
        }

        vital = string.Empty;
        return false;
    }

    public static string VitalLabel(string? vital) => vital switch
    {
        VitalHp => "PV",
        VitalMp => "PM",
        _ => string.IsNullOrWhiteSpace(vital) ? "PV/PM" : vital.Trim(),
    };

    public static bool TryApply(
        string? discriminator,
        string? parameterJson,
        CharacterVitals current,
        out CharacterVitals next,
        out bool vitalsChanged,
        out bool statsChanged,
        out string? error)
    {
        next = current;
        vitalsChanged = false;
        statsChanged = false;
        error = null;
        var json = parameterJson ?? string.Empty;
        switch (discriminator)
        {
            case MapEventCommandDiscriminators.RecoverAll:
                if (!MapEventParameterSchemas.TryParseRecoverAll(json, out error))
                {
                    return false;
                }

                next = current with
                {
                    Hp = Math.Max(0, current.MaxHp),
                    Mp = Math.Max(0, current.MaxMp),
                };
                break;

            case MapEventCommandDiscriminators.ChangeHpMp:
                if (!MapEventParameterSchemas.TryParseChangeHpMp(
                        json,
                        out var vital,
                        out var operation,
                        out var amount,
                        out error))
                {
                    return false;
                }

                var delta = operation == MapEventChangeOperation.Decrease ? -amount : amount;
                next = vital == VitalMp
                    ? CharacterProgressionAdjust.AdjustParam(current, CharacterProgressionAdjust.StatMp, delta)
                    : CharacterProgressionAdjust.AdjustParam(current, CharacterProgressionAdjust.StatHp, delta);
                break;

            default:
                error = "Commande de PV/PM inconnue.";
                return false;
        }

        vitalsChanged = CharacterProgressionAdjust.VitalsDiffer(current, next);
        return true;
    }
}
