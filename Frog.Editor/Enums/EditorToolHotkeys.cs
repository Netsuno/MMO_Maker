using System.Windows.Forms;
using System.Windows.Input;

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
            _ => string.Empty,
        };

    public static string DisplayName(EditorTool tool) =>
        tool switch
        {
            EditorTool.Brush => "Pinceau",
            EditorTool.Eraser => "Gomme",
            EditorTool.Cursor => "Curseur",
            EditorTool.Fill => "Pot (remplissage)",
            EditorTool.Rectangle => "Rectangle",
            EditorTool.Line => "Ligne",
            EditorTool.Selection => "Sélection",
            EditorTool.Spawn => "Départ (spawn)",
            EditorTool.Prefab => "Prefab (objet)",
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
        "B pinceau · E gomme · C curseur · F pot · R rectangle · L ligne (Maj = axe) · M sélection · D départ · P prefab · I pipette";

    /// <summary>Phrase d'aide affichée dans la barre d'état et le panneau d'outils.</summary>
    public static string StatusHint(EditorTool tool) =>
        tool switch
        {
            EditorTool.Brush => "Pinceau (B) · clic ou glisser pour peindre la tuile",
            EditorTool.Eraser => "Gomme (E) · clic ou glisser pour effacer",
            EditorTool.Cursor => "Curseur (C) · clic pour inspecter la tuile",
            EditorTool.Fill => "Pot (F) · clic pour remplir les cases connectées",
            EditorTool.Rectangle => "Rectangle (R) · cliquez un coin, glissez, relâchez pour peindre",
            EditorTool.Line => "Ligne (L) · cliquez le départ, glissez, relâchez · Maj = axe",
            EditorTool.Selection => "Sélection (M) · tracez un rectangle · Q rotation · H/V miroir",
            EditorTool.Spawn => "Départ (D) · clic pour poser le spawn playtest",
            EditorTool.Prefab => "Prefab (P) · choisissez un objet, puis cliquez la carte · Échap quitte",
            _ => DisplayName(tool),
        };

    /// <summary>Mesure du trait en cours (barre d'état).</summary>
    public static string FormatLineGesture(int x0, int y0, int x1, int y1, int cells, bool axisLocked)
    {
        var count = Math.Max(0, cells);
        var noun = count <= 1 ? "case" : "cases";
        var axis = axisLocked ? "axe verrouillé" : "Maj = axe";
        return $"Ligne (L) · ({x0}, {y0}) → ({x1}, {y1}) · {count} {noun} · relâchez pour peindre · {axis}";
    }

    /// <summary>Mesure du rectangle en cours (barre d'état).</summary>
    public static string FormatRectangleGesture(int x0, int y0, int x1, int y1)
    {
        var w = Math.Abs(x1 - x0) + 1;
        var h = Math.Abs(y1 - y0) + 1;
        return $"Rectangle (R) · ({x0}, {y0}) → ({x1}, {y1}) · {w}×{h} · relâchez pour peindre";
    }

    public const string SelectionHint =
        "Sélection : Q rotation 90° · H miroir horizontal · V miroir vertical · I pipette (Alt+clic)";
}
