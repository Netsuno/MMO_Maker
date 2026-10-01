namespace Frog.Editor.Ui;

/// <summary>
/// Distances du rail droit. Les minimums ne sont pas écrits sur le SplitContainer :
/// WinForms (<c>SplitContainer.ApplyPanel2MinSize</c>) compare le minimum à <c>Panel2.Width</c>
/// même en orientation horizontale, et réécrit <c>SplitterDistance</c> quand cette largeur
/// est nulle ou plus petite que le minimum.
/// </summary>
internal static class RightRailLayoutMath
{
    public const int LayersDockHeight = 168;
    public const int InspectorExpandedHeight = 160;
    public const int AssetsMinHeight = 48;

    public static bool TryTilesetDistance(
        int clientHeight,
        int splitterWidth,
        bool inspectorExpanded,
        int layersSplitterWidth,
        out int distance)
    {
        distance = 0;
        if (clientHeight <= splitterWidth + 64)
        {
            return false;
        }

        var bottom = LayersDockHeight;
        if (inspectorExpanded)
        {
            bottom += InspectorExpandedHeight + layersSplitterWidth;
        }

        var maxBottom = clientHeight - splitterWidth - AssetsMinHeight;
        if (maxBottom < 72)
        {
            return false;
        }

        if (bottom > maxBottom)
        {
            bottom = maxBottom;
        }

        distance = clientHeight - splitterWidth - bottom;
        return true;
    }

    public static bool TryLayersDistance(
        int clientHeight,
        int splitterWidth,
        int panel1Min,
        int panel2Min,
        out int distance)
    {
        distance = 0;
        var panel2 = panel2Min;
        if (clientHeight <= splitterWidth + panel1Min + panel2)
        {
            panel2 = Math.Max(80, clientHeight - splitterWidth - panel1Min);
            if (clientHeight <= splitterWidth + panel1Min + panel2)
            {
                return false;
            }
        }

        var max = clientHeight - splitterWidth - panel2;
        if (max < panel1Min)
        {
            return false;
        }

        distance = Math.Clamp(clientHeight - splitterWidth - InspectorExpandedHeight, panel1Min, max);
        return true;
    }
}
