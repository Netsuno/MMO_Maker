using System.Text;
using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>Validation publish-time des paramètres de commandes événement (P8-G1).</summary>
public static class MapEventCommandParameterValidator
{
    public static bool ValidateParameters(MapEventCommandDefinition command, out string? error)
    {
        error = null;
        if (command.SchemaVersion != 1)
        {
            error = $"SchemaVersion non supportée: {command.SchemaVersion}.";
            return false;
        }

        if (Encoding.UTF8.GetByteCount(command.ParameterJson) > MapEventRuntimeLimits.MaxCommandParameterBytes)
        {
            error = "Paramètres de commande trop volumineux (octets UTF-8).";
            return false;
        }

        var ok = command.Discriminator switch
        {
            MapEventCommandDiscriminators.ShowText =>
                MapEventParameterSchemas.TryParseShowText(command.ParameterJson, out _, out error),
            MapEventCommandDiscriminators.SetSwitch =>
                MapEventParameterSchemas.TryParseSetSwitch(command.ParameterJson, out _, out _, out error),
            MapEventCommandDiscriminators.SetVariable =>
                MapEventParameterSchemas.TryParseSetVariable(command.ParameterJson, out _, out _, out error),
            MapEventCommandDiscriminators.AddVariable =>
                MapEventParameterSchemas.TryParseAddVariable(command.ParameterJson, out _, out _, out error),
            MapEventCommandDiscriminators.SubVariable =>
                MapEventParameterSchemas.TryParseSubVariable(command.ParameterJson, out _, out _, out error),
            MapEventCommandDiscriminators.GiveItem or MapEventCommandDiscriminators.TakeItem =>
                MapEventParameterSchemas.TryParseItemMutation(command.ParameterJson, out _, out _, out _, out error),
            MapEventCommandDiscriminators.GiveGold or MapEventCommandDiscriminators.TakeGold =>
                MapEventParameterSchemas.TryParseGoldMutation(command.ParameterJson, out _, out _, out error),
            MapEventCommandDiscriminators.StartDialogue =>
                MapEventParameterSchemas.TryParseStartDialogue(command.ParameterJson, out _, out error),
            MapEventCommandDiscriminators.StartQuest or MapEventCommandDiscriminators.TurnInQuest =>
                MapEventParameterSchemas.TryParseQuestId(command.ParameterJson, out _, out error),
            MapEventCommandDiscriminators.AdvanceQuest =>
                MapEventParameterSchemas.TryParseAdvanceQuest(command.ParameterJson, out _, out _, out error),
            MapEventCommandDiscriminators.Teleport =>
                MapEventParameterSchemas.TryParseTeleport(command.ParameterJson, out _, out _, out _, out error),
            MapEventCommandDiscriminators.Wait =>
                MapEventParameterSchemas.TryParseWait(command.ParameterJson, out _, out error),
            MapEventCommandDiscriminators.CallCommonEvent =>
                MapEventParameterSchemas.TryParseCallCommonEvent(command.ParameterJson, out _, out _, out error),
            MapEventCommandDiscriminators.LearnProfession =>
                MapEventParameterSchemas.TryParseLearnProfession(command.ParameterJson, out _, out error),
            MapEventCommandDiscriminators.Branch => ValidateBranch(command.ParameterJson, 0, out error),
            MapEventCommandDiscriminators.ShowChoices => ValidateShowChoices(command.ParameterJson, 0, out error),
            MapEventCommandDiscriminators.PlayBgm or MapEventCommandDiscriminators.PlaySe =>
                MapEventParameterSchemas.TryParsePlayAudio(
                    command.ParameterJson,
                    command.Discriminator,
                    out _,
                    out error),
            _ => false,
        };

        if (!ok && error is null)
        {
            error = $"Paramètres invalides pour {command.Discriminator}.";
        }

        return ok;
    }

    private static bool ValidateBranch(string parameterJson, int depth, out string? error)
    {
        error = null;
        if (depth >= MapEventRuntimeLimits.MaxBranchDepth)
        {
            error = "Profondeur de branche maximale dépassée.";
            return false;
        }

        if (!MapEventParameterSchemas.TryParseBranch(
                parameterJson,
                out var condition,
                out var thenCommands,
                out var elseCommands,
                out error))
        {
            return false;
        }

        if (!condition.Validate(out error))
        {
            return false;
        }

        foreach (var cmd in thenCommands.Concat(elseCommands))
        {
            if (!ValidateNested(cmd, depth + 1, out error))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateShowChoices(string parameterJson, int depth, out string? error)
    {
        error = null;
        if (depth >= MapEventRuntimeLimits.MaxBranchDepth)
        {
            error = "Profondeur de branche maximale dépassée.";
            return false;
        }

        if (!MapEventParameterSchemas.TryParseShowChoices(
                parameterJson,
                out _,
                out _,
                out var branches,
                out var cancelCommands,
                out error))
        {
            return false;
        }

        foreach (var branch in branches)
        {
            foreach (var cmd in branch)
            {
                if (!ValidateNested(cmd, depth + 1, out error))
                {
                    return false;
                }
            }
        }

        foreach (var cmd in cancelCommands)
        {
            if (!ValidateNested(cmd, depth + 1, out error))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateNested(MapEventCommandDefinition cmd, int depth, out string? error)
    {
        if (!cmd.Validate(out error))
        {
            return false;
        }

        if (cmd.SchemaVersion != 1)
        {
            error = $"SchemaVersion non supportée: {cmd.SchemaVersion}.";
            return false;
        }

        if (cmd.Discriminator == MapEventCommandDiscriminators.Branch)
        {
            return ValidateBranch(cmd.ParameterJson, depth, out error);
        }

        if (cmd.Discriminator == MapEventCommandDiscriminators.ShowChoices)
        {
            return ValidateShowChoices(cmd.ParameterJson, depth, out error);
        }

        return ValidateParameters(cmd, out error);
    }
}
