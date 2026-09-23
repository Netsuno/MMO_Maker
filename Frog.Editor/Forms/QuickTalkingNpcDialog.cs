using Frog.Core.Events;
using Frog.Editor.Services;

namespace Frog.Editor.Forms;

/// <summary>
/// Raccourci : nom + texte + déclencheur, puis publication dialogue / pages
/// (<c>start_dialogue</c>) avant le clic de placement sur la carte.
/// </summary>
internal sealed class QuickTalkingNpcDialog : Form
{
    private readonly Phase8ContentPostgreSqlService _dialogues;
    private readonly MapEventsPostgreSqlService _mapEvents;
    private readonly TextBox _txtName = new() { Width = 320 };
    private readonly TextBox _txtText = new()
    {
        Width = 320,
        Height = 120,
        Multiline = true,
        AcceptsReturn = true,
        ScrollBars = ScrollBars.Vertical,
        MaxLength = 512,
    };
    private readonly RadioButton _rbAction = new()
    {
        Text = "Action (E)",
        AutoSize = true,
        Checked = true,
    };
    private readonly RadioButton _rbContact = new()
    {
        Text = "Contact joueur",
        AutoSize = true,
    };
    private readonly Label _lblError = new()
    {
        AutoSize = true,
        ForeColor = Color.Firebrick,
        MaximumSize = new Size(420, 0),
    };
    private readonly Button _btnCreate = new() { Text = "Créer et placer", AutoSize = true };
    private readonly Button _btnCancel = new() { Text = "Annuler", AutoSize = true, DialogResult = DialogResult.Cancel };
    private bool _busy;

    public QuickTalkingNpcDialog(Phase8ContentPostgreSqlService dialogues, MapEventsPostgreSqlService mapEvents)
    {
        _dialogues = dialogues ?? throw new ArgumentNullException(nameof(dialogues));
        _mapEvents = mapEvents ?? throw new ArgumentNullException(nameof(mapEvents));

        Text = "PNJ rapide";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 340);
        AcceptButton = _btnCreate;
        CancelButton = _btnCancel;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var row = 0;
        void AddRow(string label, Control control, int height = 28)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 8, 0),
            }, 0, row);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            layout.Controls.Add(control, 1, row);
            row++;
        }

        AddRow("Nom du PNJ", _txtName);
        AddRow("Texte du dialogue", _txtText, 128);

        var triggers = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        triggers.Controls.Add(_rbAction);
        triggers.Controls.Add(_rbContact);
        AddRow("Déclencheur", triggers, 32);

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        layout.Controls.Add(new Label
        {
            Text = "Crée et publie le dialogue et l'événement (start_dialogue). Le placement est le clic suivant sur la carte. La publication de la carte reste dans Fichier → Publier.",
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(0, 4, 0, 0),
        }, 0, row);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, row)!, 2);
        row++;

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
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

        _txtName.MaxLength = 128;
        _btnCreate.Click += (_, _) => _ = CreateAsync();
        FormClosing += (_, e) =>
        {
            if (_busy)
            {
                e.Cancel = true;
            }
        };
    }

    public QuickTalkingNpcResult? Result { get; private set; }

    internal TextBox NameBoxForTest => _txtName;

    internal TextBox TextBoxForTest => _txtText;

    internal RadioButton ActionForTest => _rbAction;

    internal RadioButton ContactForTest => _rbContact;

    internal Button CreateButtonForTest => _btnCreate;

    internal string SelectedTriggerForTest =>
        _rbContact.Checked ? Phase8MapEventTriggerKinds.PlayerContact : Phase8MapEventTriggerKinds.Action;

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
            var result = await QuickTalkingNpcPublisher.PublishAsync(
                _dialogues,
                _mapEvents,
                new QuickTalkingNpcRequest
                {
                    Name = _txtName.Text,
                    Text = _txtText.Text,
                    TriggerKind = SelectedTriggerForTest,
                }).ConfigureAwait(true);

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
