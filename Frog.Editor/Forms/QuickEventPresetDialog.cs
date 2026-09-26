using Frog.Application.Content;
using Frog.Editor.Services;

namespace Frog.Editor.Forms;

/// <summary>
/// Raccourci : nom, puis publication des pages (texte, interrupteur, variable, branche)
/// avant le clic de placement sur la carte.
/// </summary>
internal sealed class QuickEventPresetDialog : Form
{
    private readonly MapEventsPostgreSqlService _mapEvents;
    private readonly QuickEventPresetKind _kind;
    private readonly TextBox _txtName = new() { Width = 320 };
    private readonly Label _lblError = new()
    {
        AutoSize = true,
        ForeColor = Color.Firebrick,
        MaximumSize = new Size(440, 0),
    };
    private readonly Button _btnCreate = new() { Text = "Créer et placer", AutoSize = true };
    private readonly Button _btnCancel = new() { Text = "Annuler", AutoSize = true, DialogResult = DialogResult.Cancel };
    private bool _busy;

    public QuickEventPresetDialog(MapEventsPostgreSqlService mapEvents, QuickEventPresetKind kind)
    {
        _mapEvents = mapEvents ?? throw new ArgumentNullException(nameof(mapEvents));
        _kind = kind;

        Text = QuickEventPresetMessages.Title(kind);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 280);
        AcceptButton = _btnCreate;
        CancelButton = _btnCancel;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var row = 0;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.Controls.Add(new Label
        {
            Text = "Nom",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 8, 0),
        }, 0, row);
        _txtName.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        _txtName.Text = QuickEventPresetDraft.DefaultName(kind);
        _txtName.MaxLength = 128;
        layout.Controls.Add(_txtName, 1, row);
        row++;

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        var summary = new Label
        {
            Text = QuickEventPresetDraft.Describe(kind),
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            Margin = new Padding(0, 4, 0, 0),
        };
        layout.Controls.Add(summary, 0, row);
        layout.SetColumnSpan(summary, 2);
        row++;

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.Controls.Add(_lblError, 0, row);
        layout.SetColumnSpan(_lblError, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(12, 0, 12, 12),
        };
        buttons.Controls.Add(_btnCancel);
        buttons.Controls.Add(_btnCreate);

        Controls.Add(layout);
        Controls.Add(buttons);

        _btnCreate.Click += (_, _) => _ = CreateAsync();
        FormClosing += (_, e) =>
        {
            if (_busy)
            {
                e.Cancel = true;
            }
        };
    }

    public QuickEventPresetResult? Result { get; private set; }

    internal TextBox NameBoxForTest => _txtName;

    internal Button CreateButtonForTest => _btnCreate;

    internal QuickEventPresetKind KindForTest => _kind;

    private async Task CreateAsync()
    {
        if (_busy)
        {
            return;
        }

        _lblError.Text = string.Empty;
        _busy = true;
        _btnCreate.Enabled = false;
        _btnCancel.Enabled = false;
        var previous = _btnCreate.Text;
        _btnCreate.Text = "Création…";
        try
        {
            var result = await QuickEventPresetPublisher.PublishAsync(_mapEvents, _txtName.Text, _kind)
                .ConfigureAwait(true);
            if (!result.Success)
            {
                _lblError.Text = result.Error ?? "Création impossible.";
                return;
            }

            Result = result;
            _busy = false;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _lblError.Text = ex.Message;
        }
        finally
        {
            if (!IsDisposed && DialogResult != DialogResult.OK)
            {
                _busy = false;
                _btnCreate.Enabled = true;
                _btnCancel.Enabled = true;
                _btnCreate.Text = previous;
            }
        }
    }
}
