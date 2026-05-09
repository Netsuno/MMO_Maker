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

        return MapEventTriggerKinds.Interact;
    }
}
