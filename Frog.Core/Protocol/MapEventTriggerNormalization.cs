namespace Frog.Core.Protocol;

public static class MapEventTriggerNormalization
{
    public static string NormalizeTriggerKind(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (v == MapEventTriggerKinds.StepOn)
        {
            return MapEventTriggerKinds.StepOn;
        }

        if (v == MapEventTriggerKinds.Page)
        {
            return MapEventTriggerKinds.Page;
        }

        if (v == MapEventTriggerKinds.AutoTile)
        {
            return MapEventTriggerKinds.AutoTile;
        }

        if (v == Frog.Core.Events.Phase8MapEventTriggerKinds.Autorun)
        {
            return Frog.Core.Events.Phase8MapEventTriggerKinds.Autorun;
        }

        if (v == Frog.Core.Events.Phase8MapEventTriggerKinds.Parallel)
        {
            return Frog.Core.Events.Phase8MapEventTriggerKinds.Parallel;
        }

        return MapEventTriggerKinds.Interact;
    }
}
