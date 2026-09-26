using Frog.Core.Gameplay;
using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// Applique <c>change_level</c>, <c>change_exp</c> et <c>change_param</c>
/// sur des constantes de personnage. Pas de troupes ni de combat.
/// </summary>
public static class CharacterProgressionCommands
{
    public static bool IsProgression(string? discriminator) =>
        discriminator is MapEventCommandDiscriminators.ChangeLevel
            or MapEventCommandDiscriminators.ChangeExp
            or MapEventCommandDiscriminators.ChangeParam;

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
        CharacterVitals adjusted;
        switch (discriminator)
        {
            case MapEventCommandDiscriminators.ChangeLevel:
                if (!MapEventParameterSchemas.TryParseLevelChange(json, out var levelDelta, out error))
                {
                    return false;
                }

                adjusted = CharacterProgressionAdjust.AdjustLevel(current, levelDelta);
                break;

            case MapEventCommandDiscriminators.ChangeExp:
                if (!MapEventParameterSchemas.TryParseExpChange(json, out var expDelta, out error))
                {
                    return false;
                }

                adjusted = CharacterProgressionAdjust.AdjustExperience(current, expDelta);
                break;

            case MapEventCommandDiscriminators.ChangeParam:
                if (!MapEventParameterSchemas.TryParseParamChange(json, out var stat, out var paramDelta, out error))
                {
                    return false;
                }

                adjusted = CharacterProgressionAdjust.AdjustParam(current, stat, paramDelta);
                break;

            default:
                error = "Commande de progression inconnue.";
                return false;
        }

        next = adjusted;
        vitalsChanged = CharacterProgressionAdjust.VitalsDiffer(current, adjusted);
        statsChanged = CharacterProgressionAdjust.StatsDiffer(current, adjusted);
        return true;
    }
}
