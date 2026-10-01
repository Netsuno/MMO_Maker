namespace Frog.Editor.Ui;

/// <summary>
/// Ligne de statut unique : mode · outil · couche · (x, y) · tuile (a, b) · zoom.
/// Les avis et le catalogue restent en suffixe, sans second bandeau.
/// </summary>
internal static class EditorStatusFormatter
{
    public static string Format(
        string mode,
        string tool,
        string layer,
        int worldX,
        int worldY,
        int tileX,
        int tileY,
        int zoomPercent,
        string? notice,
        string? trailing)
    {
        var line =
            $"{mode} · {tool} · {layer} · ({worldX}, {worldY}) · tuile ({tileX}, {tileY}) · zoom {zoomPercent} %";
        if (!string.IsNullOrEmpty(notice))
        {
            line = notice + "    ·    " + line;
        }

        if (!string.IsNullOrEmpty(trailing))
        {
            line += trailing;
        }

        return line;
    }
}
