using System.Drawing;
using System.Windows.Forms;

namespace Frog.Editor.Dialogs;

internal static class SimpleInputDialog
{
    public static string? Show(IWin32Window owner, string title, string prompt, string defaultValue)
    {
        using var f = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(360, 120),
            ShowInTaskbar = false
        };

        var lbl = new Label { Text = prompt, Location = new Point(12, 14), AutoSize = true };
        var txt = new TextBox { Text = defaultValue, Location = new Point(12, 38), Width = 330 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(180, 78), Width = 80 };
        var cancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, Location = new Point(268, 78), Width = 80 };
        f.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
        f.AcceptButton = ok;
        f.CancelButton = cancel;

        return f.ShowDialog(owner) == DialogResult.OK ? txt.Text.Trim() : null;
    }
}
