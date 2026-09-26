using Frog.Application.Assets;
using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.GameData;

/// <summary>
/// Fiche Compétences (base VX) : même dépôt brouillon / publication que les sorts,
/// limité à <see cref="SpellKind.Skill"/>.
/// </summary>
public sealed class SkillEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly SpellWorkspaceSession _session;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly Label _kind = new() { AutoSize = true, Text = "Compétence" };
    private readonly NumericUpDown _manaCost = new() { Minimum = 0, Maximum = int.MaxValue, Width = 120 };
    private readonly NumericUpDown _cooldown = new() { Minimum = 0, Maximum = int.MaxValue, Width = 120 };
    private readonly ComboBox _targetType = new() { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _iconPath = new() { Width = 280 };
    private readonly Button _btnImport = new() { Text = "Importer…", AutoSize = true };
    private readonly TextBox _description = new()
    {
        Width = 360,
        Height = 90,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
    };
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDup = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private readonly AssetPreviewControl _preview = new() { Width = 128, Height = 128 };
    private bool _suppressList;
    private bool _binding;

    public event Action<string>? StatusChanged;

    public bool IsDirty => _session.IsDirty;

    internal long CurrentRevisionForTest => _session.CurrentRevision;

    internal long? PublishedRevisionForTest => _session.PublishedRevision;

    internal ContentPublishStatus CurrentStatusForTest => _session.CurrentStatus;

    internal GameDataPanelLifecycle LifecycleForTest => _lifecycle;

    internal Task<bool> DrainAsync(TimeSpan? timeout = null) => _lifecycle.DrainAsync(timeout ?? TimeSpan.FromSeconds(30));

    internal void BeginClosing() => _lifecycle.BeginClosing();

    internal void DisposeLifecycle() => _lifecycle.Dispose();

    internal Button BtnNewForTest => _btnNew;

    internal Button BtnDupForTest => _btnDup;

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal Button BtnDeleteForTest => _btnDelete;

    internal TextBox NameForTest => _name;

    internal TextBox IconPathForTest => _iconPath;

    internal TextBox DescriptionForTest => _description;

    internal TextBox SearchForTest => _search;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal ComboBox TargetForTest => _targetType;

    internal NumericUpDown ManaCostForTest => _manaCost;

    internal NumericUpDown CooldownForTest => _cooldown;

    internal Label TypeLabelForTest => _kind;

    internal ListBox ListForTest => _list;

    internal Label ValidationForTest => _validation;

    internal AssetPreviewControl PreviewForTest => _preview;

    public SkillEditorPanel(
        SpellWorkspaceSession session,
        ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _capabilities = capabilities;
        _session.KindFilter = SpellKind.Skill;
        _preview.AssetRoot = EditorTestHooks.OverrideProjectAssetRoot ?? ProjectAssetRoot.Resolve();

        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;
        foreach (var target in Enum.GetValues<TargetType>())
        {
            _targetType.Items.Add(new TargetChoice(target, TargetTypeLabels.French(target)));
        }

        SelectTarget(TargetType.SingleEnemy);

        var left = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(4) };
        left.Controls.Add(_list);
        left.Controls.Add(_search);
        left.Controls.Add(_statusFilter);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoScroll = true,
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control control)
        {
            var row = form.RowCount++;
            form.Controls.Add(
                new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left },
                0,
                row);
            form.Controls.Add(control, 1, row);
        }

        Row("Nom", _name);
        Row("Type", _kind);
        Row("Description", _description);
        Row("Coût en PM", _manaCost);
        Row("Recharge (ms)", _cooldown);
        Row("Cible", _targetType);
        Row("Chemin icône", _iconPath);
        Row("Aperçu", _preview);
        Row("État", _meta);
        Row("", _validation);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
        };
        buttons.Controls.AddRange(
            new Control[] { _btnNew, _btnDup, _btnImport, _btnSave, _btnPublish, _btnDelete });

        Controls.Add(form);
        Controls.Add(buttons);
        Controls.Add(left);

        _search.TextChanged += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            _session.SearchFilter = _search.Text;
            await RefreshListAsync(ct).ConfigureAwait(true);
        }, "refresh");
        _statusFilter.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            _session.StatusFilter = _statusFilter.SelectedIndex switch
            {
                1 => ContentPublishStatus.Draft,
                2 => ContentPublishStatus.Published,
                _ => null,
            };
            await RefreshListAsync(ct).ConfigureAwait(true);
        }, "refresh");
        _list.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(async _ =>
        {
            if (_suppressList || _list.SelectedItem is not CatalogItem item)
            {
                return;
            }

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Compétences", _session.IsDirty))
            {
                GameDataListNavigation.RevertListSelection(
                    _list,
                    ref _suppressList,
                    _session.CurrentId,
                    listItem => ((CatalogItem)listItem).Id);
                return;
            }

            await _session.OpenAsync(item.Id).ConfigureAwait(true);
            BindForm();
        }, "refresh");

        void Mark()
        {
            if (_binding)
            {
                return;
            }

            ApplyFormToSession();
            _session.MarkDirty();
            LiveValidate();
            StatusChanged?.Invoke("Modifié (non enregistré)");
        }

        _name.TextChanged += (_, _) => Mark();
        _manaCost.ValueChanged += (_, _) => Mark();
        _cooldown.ValueChanged += (_, _) => Mark();
        _targetType.SelectedIndexChanged += (_, _) => Mark();
        _iconPath.TextChanged += (_, _) =>
        {
            Mark();
            _preview.LogicalPath = _iconPath.Text.Trim();
        };
        _description.TextChanged += (_, _) => Mark();

        _btnNew.Click += (_, _) =>
        {
            _session.AdoptNewDraft(new SpellDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Nouvelle compétence",
                Kind = SpellKind.Skill,
                TargetType = TargetType.SingleEnemy,
                IconLogicalPath = $"icons/skills/new_{Guid.NewGuid():N}.png",
            });
            BindForm();
            StatusChanged?.Invoke("Nouveau brouillon");
        };
        _btnDup.Click += (_, _) =>
        {
            if (_session.Current is null)
            {
                return;
            }

            _session.DuplicateCurrent();
            BindForm();
            StatusChanged?.Invoke("Copie créée");
        };
        _btnSave.Click += (_, _) => _ = _lifecycle.TrackAsync(async _ => await SaveAsync(SaveContentIntent.SaveDraft).ConfigureAwait(true), "save");
        _btnPublish.Click += (_, _) => _ = _lifecycle.TrackAsync(async _ => await SaveAsync(SaveContentIntent.Publish).ConfigureAwait(true), "publish");
        _btnDelete.Click += (_, _) => _ = _lifecycle.RunAsync(async _ => await DeleteAsync().ConfigureAwait(true), "delete");
        _btnImport.Click += (_, _) => ImportIcon();

        var canWrite = _capabilities.AllowsSave;
        _btnSave.Enabled = canWrite;
        _btnPublish.Enabled = canWrite;
        _btnDelete.Enabled = canWrite;
    }

    private void ImportIcon()
    {
        if (!GameDataAssetImport.TryPickAndImport(this, ProjectAssetKind.Icons, out var imported))
        {
            if (!string.IsNullOrWhiteSpace(imported.Error) && imported.Error != "Annulé.")
            {
                GameDataUiMessageBox.Show(this, imported.Error, "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        GameDataAssetImport.ApplyToPathField(_iconPath, _preview, imported);
        StatusChanged?.Invoke("Icône importée (non enregistrée)");
    }

    public async Task InitializeAsync()
    {
        await RefreshListAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend compétences : {_capabilities.DisplayLabel}");
    }

    internal void QueueRefreshList()
        => _ = _lifecycle.RunAsync(async ct => await RefreshListAsync(ct).ConfigureAwait(true), "refresh");

    private async Task RefreshListAsync(CancellationToken ct = default)
    {
        await _session.RefreshCatalogAsync(ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        _suppressList = true;
        _list.Items.Clear();
        var selected = -1;
        var index = 0;
        foreach (var entry in _session.Catalog)
        {
            _list.Items.Add(new CatalogItem(
                entry.SpellId,
                $"{entry.Name} ({TargetTypeLabels.French(entry.TargetType)}) [{entry.Status}]"));
            if (_session.CurrentId == entry.SpellId)
            {
                selected = index;
            }

            index++;
        }

        if (selected >= 0)
        {
            _list.SelectedIndex = selected;
        }

        _suppressList = false;
    }

    private void BindForm()
    {
        var definition = _session.Current;
        if (definition is null)
        {
            return;
        }

        _binding = true;
        try
        {
            _name.Text = definition.Name;
            _kind.Text = "Compétence";
            _manaCost.Value = Math.Clamp(definition.ManaCost, 0, int.MaxValue);
            _cooldown.Value = Math.Clamp(definition.CooldownMs, 0, int.MaxValue);
            SelectTarget(definition.TargetType);
            _iconPath.Text = definition.IconLogicalPath;
            _preview.SetLogicalPathSilently(definition.IconLogicalPath);
            _description.Text = definition.Description ?? string.Empty;
            _meta.Text =
                $"Id={definition.Id:N}  rev={_session.CurrentRevision}  statut={_session.CurrentStatus}  publié={_session.PublishedRevision?.ToString() ?? "—"}";
        }
        finally
        {
            _binding = false;
        }

        LiveValidate();
    }

    private void ApplyFormToSession()
    {
        if (_session.Current is null)
        {
            return;
        }

        _session.Current.Name = _name.Text.Trim();
        _session.Current.Kind = SpellKind.Skill;
        _session.Current.ManaCost = (int)_manaCost.Value;
        _session.Current.CooldownMs = (int)_cooldown.Value;
        _session.Current.TargetType = (_targetType.SelectedItem as TargetChoice)?.Type ?? TargetType.SingleEnemy;
        _session.Current.IconLogicalPath = _iconPath.Text.Trim().Replace('\\', '/');
        _session.Current.Description = string.IsNullOrWhiteSpace(_description.Text)
            ? null
            : _description.Text.Trim();
    }

    private void LiveValidate()
    {
        if (_session.Current is null)
        {
            _validation.Text = string.Empty;
            return;
        }

        ApplyFormToSession();
        _validation.Text = _session.Current.Validate(out var error) ? string.Empty : error;
    }

    private async Task SaveAsync(SaveContentIntent intent)
    {
        ApplyFormToSession();
        var result = await _session.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case SaveSpellResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={success.PublishedRevision}"
                        : $"Brouillon enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveSpellResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(
                    this,
                    validation.Error,
                    "Validation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case SaveSpellResult.Conflict conflict:
                GameDataUiMessageBox.Show(
                    this,
                    $"Conflit de révision (courante={conflict.CurrentRevision}).",
                    "Conflit");
                break;
            case SaveSpellResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveSpellResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteSpellResult.Success:
                StatusChanged?.Invoke("Supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteSpellResult.NotFound:
                GameDataUiMessageBox.Show(this, "Compétence introuvable.");
                break;
            case DeleteSpellResult.Referenced referenced:
                GameDataUiMessageBox.Show(
                    this,
                    referenced.Error,
                    "Référence",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case DeleteSpellResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private void SelectTarget(TargetType type)
    {
        _targetType.SelectedItem = _targetType.Items
            .Cast<TargetChoice>()
            .FirstOrDefault(choice => choice.Type == type)
            ?? _targetType.Items.Cast<TargetChoice>().First();
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record TargetChoice(TargetType Type, string Label)
    {
        public override string ToString() => Label;
    }
}
