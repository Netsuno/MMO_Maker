using System.Windows.Forms;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EditorTextInputFocusTests
{
    [Fact]
    public void NestedPropertyGridAndTextEditors_IgnoreToolHotkeys()
    {
        StaTestRunner.Run(() =>
        {
            using var text = new TextBox();
            using var grid = new PropertyGrid();
            using var numeric = new NumericUpDown();
            using var button = new Button();
            using var panel = new Panel();
            using var listCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

            Assert.True(EditorTextInputFocus.ShouldIgnoreToolHotkeys(text));
            Assert.True(EditorTextInputFocus.ShouldIgnoreToolHotkeys(grid));
            Assert.True(EditorTextInputFocus.ShouldIgnoreToolHotkeys(numeric));
            Assert.False(EditorTextInputFocus.ShouldIgnoreToolHotkeys(button));
            Assert.False(EditorTextInputFocus.ShouldIgnoreToolHotkeys(panel));
            Assert.False(EditorTextInputFocus.ShouldIgnoreToolHotkeys(listCombo));
            Assert.True(EditorTextInputFocus.IsTextLike(grid));
        });
    }
}
