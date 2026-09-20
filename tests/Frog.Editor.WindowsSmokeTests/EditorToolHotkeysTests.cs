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
    [InlineData(Keys.M, EditorTool.Selection)]
    [InlineData(Keys.D, EditorTool.Spawn)]
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
    public void EveryEditorTool_HasLabelAndShortcutExceptUnknown()
    {
        foreach (EditorTool tool in Enum.GetValues<EditorTool>())
        {
            var label = EditorToolHotkeys.DisplayWithShortcut(tool);
            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.Contains(EditorToolHotkeys.DisplayName(tool), label, StringComparison.Ordinal);
        }

        Assert.Contains("D", EditorToolHotkeys.PaletteHint, StringComparison.Ordinal);
        Assert.Equal("D", EditorToolHotkeys.ShortcutGlyph(EditorTool.Spawn));
    }
}
