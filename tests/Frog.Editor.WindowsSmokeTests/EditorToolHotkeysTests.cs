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
        Assert.Equal("L", EditorToolHotkeys.ShortcutGlyph(EditorTool.Line));
        Assert.Equal("Ligne", EditorToolHotkeys.DisplayName(EditorTool.Line));
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
