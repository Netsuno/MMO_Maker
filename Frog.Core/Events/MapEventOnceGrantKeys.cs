namespace Frog.Core.Events;

/// <summary>Clés d'interrupteur pour récompenses événement à exécution unique (P8-G1).</summary>
public static class MapEventOnceGrantKeys
{
    public static string SwitchKeyFor(string onceKey) => $"event_once:{onceKey}";
}
