using System.Text;

namespace Frog.Editor.Forms;

/// <summary>Paramètres de publication <c>frog_map</c> (id logique serveur, clé unique, nom affiché).</summary>
internal sealed class PublishMapDialog : Form
{
    private readonly NumericUpDown _numId = new() { Minimum = 1, Maximum = int.MaxValue, Value = 1, Width = 120 };
    private readonly TextBox _txtKey = new() { Width = 360 };
    private readonly TextBox _txtDisplay = new() { Width = 360 };

    public int PublishedMapId => (int)_numId.Value;
    public string PublishedMapKey => _txtKey.Text.Trim();
    public string PublishedDisplayName => _txtDisplay.Text.Trim();

    public PublishMapDialog(string defaultDisplayName, string defaultMapKey)
    {
        Text = "Publier la carte vers MariaDB";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 260);

        _txtDisplay.Text = defaultDisplayName;
        _txtKey.Text = defaultMapKey;

        var lblIntro = new Label
        {
            Text =
                "Upsert dans frog_map (révision +1 si la ligne existe). Copiez appsettings.Local.json.example vers appsettings.Local.json à côté de l’exécutable.",
            AutoSize = false,
            Width = 480,
            Height = 48,
            Location = new Point(16, 12),
        };

        var y = 70;
        Controls.Add(lblIntro);
        Controls.Add(MkLabel("Id frog_map", 16, y));
        _numId.Location = new Point(200, y - 2);
        Controls.Add(_numId);
        y += 40;
        Controls.Add(MkLabel("map_key (unique)", 16, y));
        _txtKey.Location = new Point(200, y - 2);
        Controls.Add(_txtKey);
        y += 40;
        Controls.Add(MkLabel("display_name", 16, y));
        _txtDisplay.Location = new Point(200, y - 2);
        Controls.Add(_txtDisplay);

        var btnOk = new Button { Text = "Publier", DialogResult = DialogResult.OK, Location = new Point(300, 200), AutoSize = true };
        var btnCancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, Location = new Point(400, 200), AutoSize = true };
        Controls.Add(btnOk);
        Controls.Add(btnCancel);

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Shown += (_, _) =>
        {
            _txtDisplay.Focus();
            _txtDisplay.SelectAll();
        };
    }

    private static Label MkLabel(string text, int x, int y)
        => new()
        {
            Text = text,
            AutoSize = true,
            Location = new Point(x, y),
        };

    public bool TryValidate(out string errorMessage)
    {
        errorMessage = string.Empty;
        if (PublishedMapId < 1)
        {
            errorMessage = "Identifiant carte invalide.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(PublishedMapKey))
        {
            errorMessage = "La clé map_key est obligatoire.";
            return false;
        }

        if (Encoding.UTF8.GetByteCount(PublishedMapKey) > 255)
        {
            errorMessage = "map_key trop long (max 255 octets UTF-8).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(PublishedDisplayName))
        {
            errorMessage = "Le nom affiché est obligatoire.";
            return false;
        }

        if (Encoding.UTF8.GetByteCount(PublishedDisplayName) > 512)
        {
            errorMessage = "display_name trop long (max 512 octets UTF-8).";
            return false;
        }

        return true;
    }
}
