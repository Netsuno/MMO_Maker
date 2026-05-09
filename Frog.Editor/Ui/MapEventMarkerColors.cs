using System.Drawing;

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
}
