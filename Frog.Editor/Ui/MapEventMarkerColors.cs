using System.Drawing;
using Frog.Core.Events;
using Frog.Core.Protocol;

namespace Frog.Editor.Ui;

/// <summary>Couleur stable par slug (canevas + mini-carte).</summary>
internal static class MapEventMarkerColors
{
    public static Color TintFromSlug(string slug)
    {
        unchecked
        {
            var h = 0;
            foreach (var c in slug)
            {
                h = h * 31 + c;
            }

            h ^= slug.Length * 1315423911;
            var palette = new[]
            {
                Color.MediumOrchid,
                Color.Turquoise,
                Color.Coral,
                Color.Gold,
                Color.LightSkyBlue,
                Color.LimeGreen,
                Color.Orange,
                Color.HotPink,
            };
            var idx = (h & int.MaxValue) % palette.Length;
            return palette[idx];
        }
    }

    public static bool IsPlayerContactTrigger(string? kind) =>
        string.Equals(kind, Phase8MapEventTriggerKinds.PlayerContact, StringComparison.Ordinal)
        || string.Equals(kind, MapEventTriggerKinds.StepOn, StringComparison.Ordinal);

    public static bool IsAutorunTrigger(string? kind) =>
        string.Equals(kind, Phase8MapEventTriggerKinds.Autorun, StringComparison.Ordinal)
        || string.Equals(kind, MapEventTriggerKinds.AutoTile, StringComparison.Ordinal);

    public static bool IsParallelTrigger(string? kind) =>
        string.Equals(kind, Phase8MapEventTriggerKinds.Parallel, StringComparison.Ordinal);

    public static bool IsLegacyPageTrigger(string? kind) =>
        string.Equals(kind, MapEventTriggerKinds.Page, StringComparison.Ordinal);
}
