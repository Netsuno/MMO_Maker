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
        "B pinceau · E gomme · C curseur · F pot · R rectangle · M sélection · D départ · P prefab";
}
