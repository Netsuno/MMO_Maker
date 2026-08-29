using System.Text.Json;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms;

/// <summary>Éditeur de pages/conditions/commandes pour un événement catalogue (P8-6).</summary>
internal sealed class MapEventPageEditorDialog : Form
{
    private readonly MapEventsPostgreSqlService _service;
    private readonly Guid _eventId;
    private readonly TextBox _txtPagesJson = new()
    {
        Multiline = true,
        Dock = DockStyle.Fill,
        Font = new Font(FontFamily.GenericMonospace, 9f),
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
    };

    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnClose = new() { Text = "Fermer", AutoSize = true };

    public MapEventPageEditorDialog(MapEventsPostgreSqlService service, Guid eventId, string eventName)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _eventId = eventId;
        Text = $"Pages — {eventName}";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ClientSize = new Size(720, 480);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.RightToLeft,
        };
        bottom.Controls.Add(_btnClose);
        bottom.Controls.Add(_btnPublish);
        bottom.Controls.Add(_btnSave);

        var hint = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 48,
            Text = "JSON tableau de pages (triggerKind, priority, conditions[], commands[]). "
                   + "Discriminators: show_text, set_switch, branch, give_item, start_quest, teleport, …",
            Padding = new Padding(8, 8, 8, 0),
        };

        Controls.Add(_txtPagesJson);
        Controls.Add(hint);
        Controls.Add(bottom);

        _btnClose.Click += (_, _) => Close();
        _btnSave.Click += async (_, _) => await SaveAsync(publish: false);
        _btnPublish.Click += async (_, _) => await SaveAsync(publish: true);

        Load += async (_, _) => await LoadPagesAsync();
    }

    private async Task LoadPagesAsync()
    {
        var pages = await _service.LoadPagesJsonAsync(_eventId).ConfigureAwait(true);
        _txtPagesJson.Text = pages ?? "[]";
    }

    private async Task SaveAsync(bool publish)
    {
        var json = _txtPagesJson.Text.Trim();
        if (!MapEventPagesCodec.TryDeserializePages(json, out _, out var error))
        {
            MessageBox.Show(this, error ?? "JSON pages invalide.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var ok = await _service.TrySavePagesAsync(_eventId, json, publish).ConfigureAwait(true);
        if (!ok)
        {
            MessageBox.Show(this, "Enregistrement échoué.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        MessageBox.Show(
            this,
            publish ? "Événement publié." : "Brouillon enregistré.",
            "OK",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
