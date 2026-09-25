using System.Windows.Forms;
using System.Windows.Input;
using Frog.Editor.Enums;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

public sealed class EditorToolHotkeysTests
{
    [Theory]
    [InlineData(Keys.B, EditorTool.Brush)]
    [InlineData(Keys.E, EditorTool.Eraser)]
    [InlineData(Keys.C, EditorTool.Cursor)]
    [InlineData(Keys.F, EditorTool.Fill)]
    [InlineData(Keys.R, EditorTool.Rectangle)]
    [InlineData(Keys.L, EditorTool.Line)]
    [InlineData(Keys.M, EditorTool.Selection)]
    [InlineData(Keys.D, EditorTool.Spawn)]
    [InlineData(Keys.P, EditorTool.Prefab)]
    public void LetterKeys_SelectExpectedTool(Keys key, EditorTool expected)
    {
        Assert.True(EditorToolHotkeys.TryResolve(key, out var tool));
        Assert.Equal(expected, tool);
    }

    [Fact]
    public void ControlModifier_DoesNotStealClipboardOrSave()
    {
        Assert.False(EditorToolHotkeys.TryResolve(Keys.Control | Keys.C, out _));
        Assert.False(EditorToolHotkeys.TryResolve(Keys.Control | Keys.Shift | Keys.C, out _));
        Assert.False(EditorToolHotkeys.TryResolve(Keys.Control | Keys.S, out _));
        Assert.False(EditorToolHotkeys.TryResolve(Keys.Control | Keys.V, out _));
        Assert.False(EditorToolHotkeys.TryResolve(Keys.Control | Keys.Z, out _));
    }

    [Fact]
    public void WpfLetterD_SelectsSpawn()
    {
        Assert.True(EditorToolHotkeys.TryResolveWpf(Key.D, ModifierKeys.None, out var tool));
        Assert.Equal(EditorTool.Spawn, tool);
        Assert.False(EditorToolHotkeys.TryResolveWpf(Key.D, ModifierKeys.Control, out _));
    }

    [Fact]
    public void WpfLetterL_SelectsLine()
    {
        Assert.True(EditorToolHotkeys.TryResolveWpf(Key.L, ModifierKeys.None, out var tool));
        Assert.Equal(EditorTool.Line, tool);
        Assert.False(EditorToolHotkeys.TryResolveWpf(Key.L, ModifierKeys.Shift, out _));
        Assert.False(EditorToolHotkeys.TryResolve(Keys.Shift | Keys.L, out _));
    }

    [Fact]
    public void WpfLetterP_SelectsPrefab()
    {
        Assert.True(EditorToolHotkeys.TryResolveWpf(Key.P, ModifierKeys.None, out var tool));
        Assert.Equal(EditorTool.Prefab, tool);
        Assert.False(EditorToolHotkeys.TryResolveWpf(Key.P, ModifierKeys.Control, out _));
    }

    [Fact]
    public void EveryEditorTool_HasLabelAndShortcutExceptUnknown()
    {
        foreach (EditorTool tool in Enum.GetValues<EditorTool>())
        {
            var label = EditorToolHotkeys.DisplayWithShortcut(tool);
            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.Contains(EditorToolHotkeys.DisplayName(tool), label, StringComparison.Ordinal);
        }

        Assert.Contains("L ligne", EditorToolHotkeys.PaletteHint, StringComparison.Ordinal);
        Assert.Contains("D", EditorToolHotkeys.PaletteHint, StringComparison.Ordinal);
        Assert.Contains("P", EditorToolHotkeys.PaletteHint, StringComparison.Ordinal);
        Assert.Contains("I", EditorToolHotkeys.PaletteHint, StringComparison.Ordinal);
        Assert.Contains("Q", EditorToolHotkeys.SelectionHint, StringComparison.Ordinal);
        Assert.Contains("pipette", EditorToolHotkeys.SelectionHint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("toutes les couches", EditorToolHotkeys.SelectionHint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ctrl+Maj", EditorToolHotkeys.SelectionHint, StringComparison.Ordinal);
        Assert.Contains("toutes les couches", EditorToolHotkeys.StatusHint(EditorTool.Selection), StringComparison.Ordinal);
        Assert.Contains("Ctrl+Maj", EditorToolHotkeys.StatusHint(EditorTool.Selection), StringComparison.Ordinal);
        Assert.Contains("2×2", EditorToolHotkeys.FormatSelectionCommitted(2, 2), StringComparison.Ordinal);
        Assert.Contains("toutes les couches", EditorToolHotkeys.FormatSelectionGesture(0, 0, 3, 1), StringComparison.Ordinal);
        Assert.Contains("4×2", EditorToolHotkeys.FormatSelectionGesture(0, 0, 3, 1), StringComparison.Ordinal);
        Assert.Equal("L", EditorToolHotkeys.ShortcutGlyph(EditorTool.Line));
        Assert.Equal("Ligne", EditorToolHotkeys.DisplayName(EditorTool.Line));
        Assert.Contains("Ligne (L)", EditorToolHotkeys.StatusHint(EditorTool.Line), StringComparison.Ordinal);
        Assert.Contains("Maj", EditorToolHotkeys.StatusHint(EditorTool.Line), StringComparison.Ordinal);
        Assert.Contains("Remplissage (F)", EditorToolHotkeys.StatusHint(EditorTool.Fill), StringComparison.Ordinal);
        Assert.Contains("4 directions", EditorToolHotkeys.StatusHint(EditorTool.Fill), StringComparison.Ordinal);
        Assert.Contains("efface", EditorToolHotkeys.StatusHint(EditorTool.Fill), StringComparison.Ordinal);
        Assert.Contains("couches visibles", EditorToolHotkeys.FormatFillStatus(true, false), StringComparison.Ordinal);
        Assert.Contains("collisions", EditorToolHotkeys.FormatFillStatus(false, true), StringComparison.Ordinal);
        Assert.Equal("Remplissage", EditorToolHotkeys.DisplayName(EditorTool.Fill));
        Assert.Contains("Rectangle (R)", EditorToolHotkeys.StatusHint(EditorTool.Rectangle), StringComparison.Ordinal);
        Assert.Contains("plein", EditorToolHotkeys.StatusHint(EditorTool.Rectangle), StringComparison.Ordinal);
        Assert.Contains("Contour", EditorToolHotkeys.StatusHint(EditorTool.Rectangle), StringComparison.Ordinal);
        Assert.Contains("Ellipse", EditorToolHotkeys.StatusHint(EditorTool.Rectangle), StringComparison.Ordinal);
        Assert.Contains("Maj = contour", EditorToolHotkeys.PaletteHint, StringComparison.Ordinal);
        Assert.Contains("contour", EditorToolHotkeys.FormatRectangleGesture(0, 0, 3, 2, outline: true), StringComparison.Ordinal);
        Assert.Contains("ellipse", EditorToolHotkeys.FormatRectangleGesture(0, 0, 4, 2, ellipse: true), StringComparison.Ordinal);
        Assert.Contains("plein", EditorToolHotkeys.FormatRectangleStatus(false, false), StringComparison.Ordinal);
        Assert.False(EditorToolHotkeys.TryResolve(Keys.Shift | Keys.R, out _));
        Assert.True(EditorToolHotkeys.TryResolveWpf(Key.R, ModifierKeys.None, out var rectangleTool));
        Assert.Equal(EditorTool.Rectangle, rectangleTool);
        Assert.False(EditorToolHotkeys.TryResolveWpf(Key.R, ModifierKeys.Shift, out _));
        var gesture = EditorToolHotkeys.FormatLineGesture(0, 0, 4, 2, 5, axisLocked: false);
        Assert.Contains("(0, 0) → (4, 2)", gesture, StringComparison.Ordinal);
        Assert.Contains("5 cases", gesture, StringComparison.Ordinal);
        Assert.Contains("relâchez pour peindre", gesture, StringComparison.Ordinal);
        Assert.Contains("axe verrouillé", EditorToolHotkeys.FormatLineGesture(1, 1, 1, 6, 6, axisLocked: true), StringComparison.Ordinal);
        Assert.Contains("1 case", EditorToolHotkeys.FormatLineGesture(2, 2, 2, 2, 1, false), StringComparison.Ordinal);
        Assert.Contains("4×3", EditorToolHotkeys.FormatRectangleGesture(0, 0, 3, 2), StringComparison.Ordinal);
        Assert.Equal("aperçu animé · 3 images", EditorToolHotkeys.FormatAnimatedTilePreview(3, true));
        Assert.Equal("aperçu animé · 1 image", EditorToolHotkeys.FormatAnimatedTilePreview(1, true));
        Assert.Equal("tuile animée · 3 images · aperçu arrêté", EditorToolHotkeys.FormatAnimatedTilePreview(3, false));
        Assert.DoesNotContain("frame", EditorToolHotkeys.FormatAnimatedTilePreview(4, true), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("D", EditorToolHotkeys.ShortcutGlyph(EditorTool.Spawn));
        Assert.Equal("P", EditorToolHotkeys.ShortcutGlyph(EditorTool.Prefab));
    }

    [Fact]
    public void PipetteAndSelectionKeys_AreNotToolHotkeys()
    {
        Assert.False(EditorToolHotkeys.TryResolve(Keys.I, out _));
        Assert.False(EditorToolHotkeys.TryResolve(Keys.Q, out _));
        Assert.False(EditorToolHotkeys.TryResolve(Keys.H, out _));
        Assert.False(EditorToolHotkeys.TryResolve(Keys.V, out _));
        Assert.False(EditorToolHotkeys.TryResolve(Keys.Alt | Keys.I, out _));
        Assert.False(EditorToolHotkeys.TryResolveWpf(Key.I, ModifierKeys.None, out _));
        Assert.False(EditorToolHotkeys.TryResolveWpf(Key.Q, ModifierKeys.None, out _));
    }
}
