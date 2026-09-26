using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;

namespace Frog.Editor.Forms.GameData;

/// <summary>Liste + formulaire héros (brouillon / publication).</summary>
public sealed class ActorEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly ActorWorkspaceSession _session;
    private readonly IPublishedClassCatalog _classCatalog;
    private readonly IPublishedItemCatalog _itemCatalog;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly TextBox _description = new()
    {
        Width = 360,
        Height = 70,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
    };
    private readonly ComboBox _class = new() { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _facePath = new() { Width = 360 };
    private readonly ComboBox _body = new() { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _hair = new() { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _tunic = new() { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _weapon = new() { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _armor = new() { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _baseHp = new() { Minimum = 1, Maximum = int.MaxValue, Value = 100, Width = 120 };
    private readonly NumericUpDown _baseMp = new() { Minimum = 1, Maximum = int.MaxValue, Value = 50, Width = 120 };
    private readonly NumericUpDown _str = StatControl();
    private readonly NumericUpDown _agi = StatControl();
    private readonly NumericUpDown _vit = StatControl();
    private readonly NumericUpDown _int = StatControl();
    private readonly NumericUpDown _dex = StatControl();
    private readonly NumericUpDown _luck = StatControl();
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDup = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
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

    internal TextBox FacePathForTest => _facePath;

    internal TextBox SearchForTest => _search;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal ComboBox ClassForTest => _class;

    internal ComboBox BodyForTest => _body;

    internal ComboBox HairForTest => _hair;

    internal ComboBox TunicForTest => _tunic;

    internal ComboBox WeaponForTest => _weapon;

    internal ComboBox ArmorForTest => _armor;

    internal ListBox ListForTest => _list;

    internal Label ValidationForTest => _validation;

    public ActorEditorPanel(
        ActorWorkspaceSession session,
        IPublishedClassCatalog classCatalog,
        IPublishedItemCatalog itemCatalog,
        ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _classCatalog = classCatalog;
        _itemCatalog = itemCatalog;
        _capabilities = capabilities;

        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;
        FillLookCombos();
        _class.Items.Add(new GuidChoice(null, "Aucune"));
        _class.SelectedIndex = 0;
        _weapon.Items.Add(new GuidChoice(null, "Aucune"));
        _weapon.SelectedIndex = 0;
        _armor.Items.Add(new GuidChoice(null, "Aucune"));
        _armor.SelectedIndex = 0;

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
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
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
        Row("Description", _description);
        Row("Classe", _class);
        Row("Visage (chemin)", _facePath);
        Row("Corps", _body);
        Row("Cheveux", _hair);
        Row("Tunique", _tunic);
        Row("Arme de départ", _weapon);
        Row("Armure de départ", _armor);
        Row("PV de base", _baseHp);
        Row("PM de base", _baseMp);
        Row("FOR", _str);
        Row("AGI", _agi);
        Row("VIT", _vit);
        Row("INT", _int);
        Row("DEX", _dex);
        Row("CHANCE", _luck);
        Row("État", _meta);
        Row("", _validation);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
        };
        buttons.Controls.AddRange(
            new Control[] { _btnNew, _btnDup, _btnSave, _btnPublish, _btnDelete });

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

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Héros", _session.IsDirty))
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
        _description.TextChanged += (_, _) => Mark();
        _class.SelectedIndexChanged += (_, _) => Mark();
        _facePath.TextChanged += (_, _) => Mark();
        _body.SelectedIndexChanged += (_, _) => Mark();
        _hair.SelectedIndexChanged += (_, _) => Mark();
        _tunic.SelectedIndexChanged += (_, _) => Mark();
        _weapon.SelectedIndexChanged += (_, _) => Mark();
        _armor.SelectedIndexChanged += (_, _) => Mark();
        _baseHp.ValueChanged += (_, _) => Mark();
        _baseMp.ValueChanged += (_, _) => Mark();
        _str.ValueChanged += (_, _) => Mark();
        _agi.ValueChanged += (_, _) => Mark();
        _vit.ValueChanged += (_, _) => Mark();
        _int.ValueChanged += (_, _) => Mark();
        _dex.ValueChanged += (_, _) => Mark();
        _luck.ValueChanged += (_, _) => Mark();

        _btnNew.Click += (_, _) =>
        {
            _session.AdoptNewDraft(new ActorDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Nouveau héros",
                Body = 0,
                Hair = 0,
                Tunic = 0,
                BaseHp = 100,
                BaseMp = 50,
                Str = 10,
                Agi = 10,
                Vit = 10,
                Int = 10,
                Dex = 10,
                Luck = 10,
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

        var canWrite = _capabilities.AllowsSave;
        _btnSave.Enabled = canWrite;
        _btnPublish.Enabled = canWrite;
        _btnDelete.Enabled = canWrite;
    }

    public async Task InitializeAsync()
    {
        await RefreshLinkedCatalogsAsync().ConfigureAwait(true);
        await RefreshListAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend héros : {_capabilities.DisplayLabel}");
    }

    public Task RefreshLinkedCatalogsAsync() => RefreshLinkedCatalogsCoreAsync();

    internal void QueueRefreshLinkedCatalogs()
        => _ = _lifecycle.RunAsync(async ct =>
        {
            ct.ThrowIfCancellationRequested();
            await RefreshLinkedCatalogsCoreAsync().ConfigureAwait(true);
        }, "refresh");

    private async Task RefreshLinkedCatalogsCoreAsync()
    {
        var selectedClass = (_class.SelectedItem as GuidChoice)?.Id;
        var selectedWeapon = (_weapon.SelectedItem as GuidChoice)?.Id;
        var selectedArmor = (_armor.SelectedItem as GuidChoice)?.Id;
        var classes = await _classCatalog.ListPublishedAsync().ConfigureAwait(true);
        var items = await _itemCatalog.ListPublishedAsync().ConfigureAwait(true);
        _binding = true;
        try
        {
            FillGuidCombo(_class, "Aucune", classes.Select(c => new GuidChoice(c.Id, c.Name)), selectedClass);
            FillGuidCombo(
                _weapon,
                "Aucune",
                items.Where(i => i.Kind == ItemType.Weapon).Select(i => new GuidChoice(i.Id, i.Name)),
                selectedWeapon);
            FillGuidCombo(
                _armor,
                "Aucune",
                items.Where(i => i.Kind == ItemType.Armor).Select(i => new GuidChoice(i.Id, i.Name)),
                selectedArmor);
        }
        finally
        {
            _binding = false;
        }
    }

    private async Task RefreshListAsync(CancellationToken ct = default)
    {
        await _session.RefreshCatalogAsync(ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        _suppressList = true;
        _list.Items.Clear();
        foreach (var entry in _session.Catalog)
        {
            _list.Items.Add(new CatalogItem(
                entry.ActorId,
                $"{entry.Name} (PV {entry.BaseHp}, PM {entry.BaseMp}) [{entry.Status}]"));
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
            _description.Text = definition.Description ?? string.Empty;
            SelectGuid(_class, definition.ClassId);
            _facePath.Text = definition.FaceLogicalPath ?? string.Empty;
            _body.SelectedIndex = Math.Clamp(definition.Body, 0, Math.Max(0, _body.Items.Count - 1));
            _hair.SelectedIndex = Math.Clamp(definition.Hair, 0, Math.Max(0, _hair.Items.Count - 1));
            _tunic.SelectedIndex = Math.Clamp(definition.Tunic, 0, Math.Max(0, _tunic.Items.Count - 1));
            SelectGuid(_weapon, definition.StartingWeaponItemId);
            SelectGuid(_armor, definition.StartingArmorItemId);
            _baseHp.Value = Math.Clamp(definition.BaseHp, 1, int.MaxValue);
            _baseMp.Value = Math.Clamp(definition.BaseMp, 1, int.MaxValue);
            _str.Value = Math.Clamp(definition.Str, ActorDefinition.MinStat, ActorDefinition.MaxStat);
            _agi.Value = Math.Clamp(definition.Agi, ActorDefinition.MinStat, ActorDefinition.MaxStat);
            _vit.Value = Math.Clamp(definition.Vit, ActorDefinition.MinStat, ActorDefinition.MaxStat);
            _int.Value = Math.Clamp(definition.Int, ActorDefinition.MinStat, ActorDefinition.MaxStat);
            _dex.Value = Math.Clamp(definition.Dex, ActorDefinition.MinStat, ActorDefinition.MaxStat);
            _luck.Value = Math.Clamp(definition.Luck, ActorDefinition.MinStat, ActorDefinition.MaxStat);
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
        _session.Current.Description = string.IsNullOrWhiteSpace(_description.Text)
            ? null
            : _description.Text.Trim();
        _session.Current.ClassId = (_class.SelectedItem as GuidChoice)?.Id;
        _session.Current.FaceLogicalPath = string.IsNullOrWhiteSpace(_facePath.Text)
            ? null
            : _facePath.Text.Trim();
        _session.Current.Body = (byte)Math.Max(0, _body.SelectedIndex);
        _session.Current.Hair = (byte)Math.Max(0, _hair.SelectedIndex);
        _session.Current.Tunic = (byte)Math.Max(0, _tunic.SelectedIndex);
        _session.Current.StartingWeaponItemId = (_weapon.SelectedItem as GuidChoice)?.Id;
        _session.Current.StartingArmorItemId = (_armor.SelectedItem as GuidChoice)?.Id;
        _session.Current.BaseHp = (int)_baseHp.Value;
        _session.Current.BaseMp = (int)_baseMp.Value;
        _session.Current.Str = (int)_str.Value;
        _session.Current.Agi = (int)_agi.Value;
        _session.Current.Vit = (int)_vit.Value;
        _session.Current.Int = (int)_int.Value;
        _session.Current.Dex = (int)_dex.Value;
        _session.Current.Luck = (int)_luck.Value;
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
            case SaveActorResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={success.PublishedRevision}"
                        : $"Brouillon enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveActorResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(
                    this,
                    validation.Error,
                    "Validation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case SaveActorResult.Conflict conflict:
                GameDataUiMessageBox.Show(
                    this,
                    $"Conflit de révision (courante={conflict.CurrentRevision}).",
                    "Conflit");
                break;
            case SaveActorResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveActorResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteActorResult.Success:
                StatusChanged?.Invoke("Supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteActorResult.NotFound:
                GameDataUiMessageBox.Show(this, "Héros introuvable.");
                break;
            case DeleteActorResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private void FillLookCombos()
    {
        _body.Items.AddRange(CharacterLook.BodyLabels.Cast<object>().ToArray());
        _hair.Items.AddRange(CharacterLook.HairLabels.Cast<object>().ToArray());
        _tunic.Items.AddRange(CharacterLook.TunicLabels.Cast<object>().ToArray());
        _body.SelectedIndex = 0;
        _hair.SelectedIndex = 0;
        _tunic.SelectedIndex = 0;
    }

    private static void FillGuidCombo(
        ComboBox combo,
        string emptyLabel,
        IEnumerable<GuidChoice> choices,
        Guid? selectedId)
    {
        combo.Items.Clear();
        combo.Items.Add(new GuidChoice(null, emptyLabel));
        foreach (var choice in choices.OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase))
        {
            combo.Items.Add(choice);
        }

        SelectGuid(combo, selectedId);
    }

    private static void SelectGuid(ComboBox combo, Guid? id)
    {
        combo.SelectedItem = combo.Items
            .Cast<GuidChoice>()
            .FirstOrDefault(choice => choice.Id == id)
            ?? combo.Items.Cast<GuidChoice>().First();
    }

    private static NumericUpDown StatControl() => new()
    {
        Minimum = ActorDefinition.MinStat,
        Maximum = ActorDefinition.MaxStat,
        Value = 10,
        Width = 80,
    };

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record GuidChoice(Guid? Id, string Label)
    {
        public override string ToString() => Label;
    }
}
