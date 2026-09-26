using System.Windows.Forms;
using System.Windows.Input;
using Frog.Application.Maps;
using Frog.Core.Maps;

namespace Frog.Editor.Enums;

/// <summary>Raccourcis lettre sans modificateur — n’entrent pas en conflit avec Ctrl+C/X/V/Z/Y/N/O/S.</summary>
public static class EditorToolHotkeys
{
    public static bool TryResolve(Keys keyData, out EditorTool tool)
    {
        tool = default;
        if ((keyData & (Keys.Control | Keys.Alt | Keys.Shift)) != Keys.None)
        {
            return false;
        }

        return TryResolveKeyCode(keyData & Keys.KeyCode, out tool);
    }

    public static bool TryResolveWpf(Key key, ModifierKeys modifiers, out EditorTool tool)
    {
        tool = default;
        if (modifiers != ModifierKeys.None)
        {
            return false;
        }

        var mapped = key switch
        {
            Key.B => Keys.B,
            Key.E => Keys.E,
            Key.C => Keys.C,
            Key.F => Keys.F,
            Key.R => Keys.R,
            Key.L => Keys.L,
            Key.M => Keys.M,
            Key.D => Keys.D,
            Key.P => Keys.P,
            Key.N => Keys.N,
            Key.G => Keys.G,
            _ => Keys.None,
        };

        return mapped != Keys.None && TryResolveKeyCode(mapped, out tool);
    }

    public static bool TryResolveKeyCode(Keys code, out EditorTool tool)
    {
        switch (code)
        {
            case Keys.B:
                tool = EditorTool.Brush;
                return true;
            case Keys.E:
                tool = EditorTool.Eraser;
                return true;
            case Keys.C:
                tool = EditorTool.Cursor;
                return true;
            case Keys.F:
                tool = EditorTool.Fill;
                return true;
            case Keys.R:
                tool = EditorTool.Rectangle;
                return true;
            case Keys.L:
                tool = EditorTool.Line;
                return true;
            case Keys.M:
                tool = EditorTool.Selection;
                return true;
            case Keys.D:
                tool = EditorTool.Spawn;
                return true;
            case Keys.P:
                tool = EditorTool.Prefab;
                return true;
            case Keys.N:
                tool = EditorTool.Place;
                return true;
            case Keys.G:
                tool = EditorTool.Region;
                return true;
            default:
                tool = default;
                return false;
        }
    }

    public static string ShortcutGlyph(EditorTool tool) =>
        tool switch
        {
            EditorTool.Brush => "B",
            EditorTool.Eraser => "E",
            EditorTool.Cursor => "C",
            EditorTool.Fill => "F",
            EditorTool.Rectangle => "R",
            EditorTool.Line => "L",
            EditorTool.Selection => "M",
            EditorTool.Spawn => "D",
            EditorTool.Prefab => "P",
            EditorTool.Place => "N",
            EditorTool.Region => "G",
            _ => string.Empty,
        };

    public static string DisplayName(EditorTool tool) =>
        tool switch
        {
            EditorTool.Brush => "Pinceau",
            EditorTool.Eraser => "Gomme",
            EditorTool.Cursor => "Curseur",
            EditorTool.Fill => "Remplissage",
            EditorTool.Rectangle => "Rectangle",
            EditorTool.Line => "Ligne",
            EditorTool.Selection => "Sélection",
            EditorTool.Spawn => "Départ (spawn)",
            EditorTool.Prefab => "Prefab (objet)",
            EditorTool.Place => "Entités",
            EditorTool.Region => MapRegionLabels.ToolName,
            _ => tool.ToString(),
        };

    public static string DisplayWithShortcut(EditorTool tool)
    {
        var glyph = ShortcutGlyph(tool);
        return string.IsNullOrEmpty(glyph)
            ? DisplayName(tool)
            : $"{DisplayName(tool)} ({glyph})";
    }

    public const string PaletteHint =
        "B pinceau · E gomme · C curseur · F remplissage · R rectangle (Maj = contour) · L ligne (Maj = axe) · M sélection · D départ · P prefab · N entités · G région · I pipette";

    /// <summary>Phrase d'aide affichée dans la barre d'état et le panneau d'outils.</summary>
    public static string StatusHint(EditorTool tool) =>
        tool switch
        {
            EditorTool.Brush => "Pinceau (B) · clic ou glisser pour peindre la tuile",
            EditorTool.Eraser => "Gomme (E) · clic ou glisser pour effacer le tampon · couche active visible et déverrouillée",
            EditorTool.Cursor => "Curseur (C) · clic pour inspecter la tuile",
            EditorTool.Fill => FormatFillStatus(visibleUnlockedLayers: false, respectAttributes: false),
            EditorTool.Rectangle => FormatRectangleStatus(outline: false, ellipse: false),
            EditorTool.Line => "Ligne (L) · cliquez le départ, glissez, relâchez · Maj = axe",
            EditorTool.Selection => "Sélection (M) · tracez une zone rectangulaire · copie toutes les couches · Ctrl+C/X/V · Ctrl+Maj = couche active · Q/H/V · Suppr · modèle : menu Édition",
            EditorTool.Spawn => "Départ (D) · clic pour poser le spawn playtest",
            EditorTool.Prefab => "Prefab (P) · choisissez un objet, puis cliquez la carte · clic sélectionne · Ctrl+D duplique · Échap quitte",
            EditorTool.Place => "Entités (N) · Apparition, PNJ ou Objet · clic pose · glisser déplace · clic droit retire",
            EditorTool.Region => MapRegionLabels.FormatStatus(1),
            _ => DisplayName(tool),
        };

    /// <summary>
    /// Barre d'état du pot. 4 directions. La sélection vide et le clic droit effacent la région.
    /// Ctrl (ou la case à cocher) étend aux couches visibles déverrouillées, sauf Attributs.
    /// </summary>
    public static string FormatFillStatus(bool visibleUnlockedLayers, bool respectAttributes)
    {
        var layers = visibleUnlockedLayers
            ? "couches visibles déverrouillées"
            : "couche active · Ctrl = couches visibles";
        var attrs = respectAttributes ? " · collisions / attrs" : string.Empty;
        return $"Remplissage (F) · 4 directions · {layers}{attrs} · clic peint · clic droit ou tuile vide : efface";
    }

    /// <summary>Barre d'état de l'outil entités. Le type à poser vient du panneau de propriétés.</summary>
    public static string FormatPlaceStatus(MapPlacedKind kind, string? selectedName)
    {
        var kindLabel = MapPlacedEntityEdit.KindLabel(kind);
        var selected = string.IsNullOrWhiteSpace(selectedName)
            ? "aucune sélection"
            : $"sélection « {selectedName.Trim()} »";
        return $"Entités (N) · poser {kindLabel} · {selected} · clic pose ou sélectionne · glisser déplace · clic droit retire";
    }

    /// <summary>Mesure du trait en cours (barre d'état).</summary>
    public static string FormatLineGesture(int x0, int y0, int x1, int y1, int cells, bool axisLocked)
    {
        var count = Math.Max(0, cells);
        var noun = count <= 1 ? "case" : "cases";
        var axis = axisLocked ? "axe verrouillé" : "Maj = axe";
        return $"Ligne (L) · ({x0}, {y0}) → ({x1}, {y1}) · {count} {noun} · relâchez pour peindre · {axis}";
    }

    /// <summary>
    /// Barre d'état du rectangle. Défaut : rectangle plein sur la couche active.
    /// Maj pendant le tracé, ou la case Contour, ne peint que le bord. La case Ellipse inscrit l'ellipse.
    /// </summary>
    public static string FormatRectangleStatus(bool outline, bool ellipse)
    {
        var shape = ellipse ? "ellipse" : "rectangle";
        var mode = outline ? "contour" : "plein";
        return $"Rectangle (R) · {shape} {mode} · clic, glisser, relâcher · Maj ou case Contour = contour · case Ellipse · couche active";
    }

    /// <summary>Mesure du rectangle ou de l'ellipse en cours (barre d'état).</summary>
    public static string FormatRectangleGesture(int x0, int y0, int x1, int y1, bool outline = false, bool ellipse = false)
    {
        var w = Math.Abs(x1 - x0) + 1;
        var h = Math.Abs(y1 - y0) + 1;
        var shape = ellipse ? "ellipse" : "rectangle";
        var mode = outline ? "contour" : "plein";
        return $"Rectangle (R) · {shape} {mode} · ({x0}, {y0}) → ({x1}, {y1}) · {w}×{h} · relâchez pour peindre";
    }

    /// <summary>Rectangle de sélection en cours de tracé (barre d'état).</summary>
    public static string FormatSelectionGesture(int x0, int y0, int x1, int y1)
    {
        var w = Math.Abs(x1 - x0) + 1;
        var h = Math.Abs(y1 - y0) + 1;
        return $"Sélection (M) · zone ({x0}, {y0}) → ({x1}, {y1}) · {w}×{h} · toutes les couches · relâchez pour figer la sélection";
    }

    /// <summary>Sélection figée : copier-coller multi-couches, couche active avec Ctrl+Maj.</summary>
    public static string FormatSelectionCommitted(int width, int height)
        => $"Sélection (M) · zone {width}×{height} · copie toutes les couches · Ctrl+C/X/V (copie, coupe, colle) · Ctrl+Maj = couche active · Q/H/V · Suppr · modèle : menu Édition";

    /// <summary>Presse-papiers de zone prêt à être collé sous le curseur.</summary>
    public static string FormatZoneClipboard(int width, int height, bool singleLayer)
    {
        var scope = singleLayer ? "couche active" : "toutes les couches";
        return $"zone copiée {width}×{height} · {scope} · Ctrl+V colle sous le curseur";
    }

    /// <summary>État d’une tuile animée dans la barre d’état (français, sans le mot anglais « frames »).</summary>
    public static string FormatAnimatedTilePreview(int frameCount, bool previewEnabled)
    {
        var count = Math.Max(0, frameCount);
        var noun = count <= 1 ? "image" : "images";
        return previewEnabled
            ? $"aperçu animé · {count} {noun}"
            : $"tuile animée · {count} {noun} · aperçu arrêté";
    }

    public const string SelectionHint =
        "Copie toutes les couches : Ctrl+C copie la zone, Ctrl+X coupe, Ctrl+V colle · couche active : Ctrl+Maj+C/V · Q rotation · H/V miroir · I pipette (Alt+clic)";
}
