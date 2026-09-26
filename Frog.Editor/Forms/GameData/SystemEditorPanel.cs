using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.GameData;

/// <summary>
/// Fiche Système (base VX) : catalogue nommé des interrupteurs et des variables.
/// Un seul document, brouillon puis publication, comme les autres données de jeu.
/// </summary>
public sealed class SystemEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly SystemWorkspaceSession _session;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _switchList = new() { Dock = DockStyle.Fill };
    private readonly TextBox _switchId = new() { Width = 280 };
    private readonly TextBox _switchLabel = new() { Width = 280 };
    private readonly TextBox _switchNote = new()
    {
        Width = 360,
        Height = 72,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
    };
    private readonly Button _btnAddSwitch = new() { Text = "Ajouter", AutoSize = true };
    private readonly Button _btnRemoveSwitch = new() { Text = "Retirer", AutoSize = true };
    private readonly ListBox _variableList = new() { Dock = DockStyle.Fill };
    private readonly TextBox _variableId = new() { Width = 280 };
    private readonly TextBox _variableLabel = new() { Width = 280 };
    private readonly TextBox _variableNote = new()
    {
        Width = 360,
        Height = 72,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
    };
    private readonly Button _btnAddVariable = new() { Text = "Ajouter", AutoSize = true };
    private readonly Button _btnRemoveVariable = new() { Text = "Retirer", AutoSize = true };
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private bool _binding;
    private bool _suppressSwitch;
    private bool _suppressVariable;

    public event Action<string>? StatusChanged;

    public bool IsDirty => _session.IsDirty;

    internal long CurrentRevisionForTest => _session.CurrentRevision;

    internal long? PublishedRevisionForTest => _session.PublishedRevision;

    internal ContentPublishStatus CurrentStatusForTest => _session.CurrentStatus;

    internal GameDataPanelLifecycle LifecycleForTest => _lifecycle;

    internal Task<bool> DrainAsync(TimeSpan? timeout = null) => _lifecycle.DrainAsync(timeout ?? TimeSpan.FromSeconds(30));

    internal void BeginClosing() => _lifecycle.BeginClosing();

    internal void DisposeLifecycle() => _lifecycle.Dispose();

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal Button BtnAddSwitchForTest => _btnAddSwitch;

    internal Button BtnRemoveSwitchForTest => _btnRemoveSwitch;

    internal Button BtnAddVariableForTest => _btnAddVariable;

    internal Button BtnRemoveVariableForTest => _btnRemoveVariable;

    internal TextBox SwitchIdForTest => _switchId;

    internal TextBox SwitchLabelForTest => _switchLabel;

    internal TextBox SwitchNoteForTest => _switchNote;

    internal TextBox VariableIdForTest => _variableId;

    internal TextBox VariableLabelForTest => _variableLabel;

    internal TextBox VariableNoteForTest => _variableNote;

    internal ListBox SwitchListForTest => _switchList;

    internal ListBox VariableListForTest => _variableList;

    internal Label ValidationForTest => _validation;

    internal Label MetaForTest => _meta;

    public SystemEditorPanel(
        SystemWorkspaceSession session,
        ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _capabilities = capabilities;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildPage(
            "Interrupteurs",
            "Identifiants cités par les commandes « Régler interrupteur ».",
            _switchList,
            _switchId,
            _switchLabel,
            _switchNote,
            _btnAddSwitch,
            _btnRemoveSwitch));
        tabs.TabPages.Add(BuildPage(
            "Variables",
            "Identifiants cités par les commandes « Variable ».",
            _variableList,
            _variableId,
            _variableLabel,
            _variableNote,
            _btnAddVariable,
            _btnRemoveVariable));

        var intro = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(8, 8, 8, 0),
            Text = "Noms français des interrupteurs et variables globaux (clés des commandes d’événements).",
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 4, 8, 4),
        };
        buttons.Controls.Add(_btnSave);
        buttons.Controls.Add(_btnPublish);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12, 0, 8, 0),
        };
        footer.Controls.Add(_meta);
        footer.Controls.Add(_validation);

        Controls.Add(tabs);
        Controls.Add(footer);
        Controls.Add(buttons);
        Controls.Add(intro);

        _switchList.SelectedIndexChanged += (_, _) =>
        {
            if (!_suppressSwitch)
            {
                BindSwitchFields();
            }
        };
        _variableList.SelectedIndexChanged += (_, _) =>
        {
            if (!_suppressVariable)
            {
                BindVariableFields();
            }
        };

        _switchId.TextChanged += (_, _) => WriteSwitch(entry => entry.Id = _switchId.Text.Trim());
        _switchLabel.TextChanged += (_, _) => WriteSwitch(entry => entry.Label = _switchLabel.Text.Trim());
        _switchNote.TextChanged += (_, _) => WriteSwitch(entry =>
            entry.Note = string.IsNullOrWhiteSpace(_switchNote.Text) ? null : _switchNote.Text.Trim());
        _variableId.TextChanged += (_, _) => WriteVariable(entry => entry.Id = _variableId.Text.Trim());
        _variableLabel.TextChanged += (_, _) => WriteVariable(entry => entry.Label = _variableLabel.Text.Trim());
        _variableNote.TextChanged += (_, _) => WriteVariable(entry =>
            entry.Note = string.IsNullOrWhiteSpace(_variableNote.Text) ? null : _variableNote.Text.Trim());

        _btnAddSwitch.Click += (_, _) => AddSwitch();
        _btnRemoveSwitch.Click += (_, _) => RemoveSwitch();
        _btnAddVariable.Click += (_, _) => AddVariable();
        _btnRemoveVariable.Click += (_, _) => RemoveVariable();
        _btnSave.Click += (_, _) => _ = _lifecycle.TrackAsync(
            async _ => await SaveAsync(SaveContentIntent.SaveDraft).ConfigureAwait(true),
            "save");
        _btnPublish.Click += (_, _) => _ = _lifecycle.TrackAsync(
            async _ => await SaveAsync(SaveContentIntent.Publish).ConfigureAwait(true),
            "publish");

        var canWrite = _capabilities.AllowsSave;
        _btnSave.Enabled = canWrite;
        _btnPublish.Enabled = canWrite;
    }

    public async Task InitializeAsync()
    {
        await _session.LoadAsync().ConfigureAwait(true);
        EditorSystemNameCatalog.Replace(_session.Current);
        RebuildSwitchList(PreferredIndex(_session.Current?.Switches.Count ?? 0));
        RebuildVariableList(PreferredIndex(_session.Current?.Variables.Count ?? 0));
        BindMeta();
        LiveValidate();
        StatusChanged?.Invoke($"Backend système : {_capabilities.DisplayLabel}");
    }

    private static int PreferredIndex(int count) => count > 0 ? 0 : -1;

    private void AddSwitch()
    {
        var current = _session.Current;
        if (current is null || current.Switches.Count >= SystemDefinition.MaxEntries)
        {
            return;
        }

        current.Switches.Add(new SystemSwitchEntry
        {
            Id = NextId(current.Switches.Select(entry => entry.Id), "interrupteur_"),
            Label = "Nouvel interrupteur",
        });
        _session.MarkDirty();
        RebuildSwitchList(current.Switches.Count - 1);
        LiveValidate();
        StatusChanged?.Invoke("Modifié (non enregistré)");
    }

    private void RemoveSwitch()
    {
        var current = _session.Current;
        var index = _switchList.SelectedIndex;
        if (current is null || index < 0 || index >= current.Switches.Count)
        {
            return;
        }

        current.Switches.RemoveAt(index);
        _session.MarkDirty();
        var next = current.Switches.Count == 0 ? -1 : Math.Min(index, current.Switches.Count - 1);
        RebuildSwitchList(next);
        LiveValidate();
        StatusChanged?.Invoke("Modifié (non enregistré)");
    }

    private void AddVariable()
    {
        var current = _session.Current;
        if (current is null || current.Variables.Count >= SystemDefinition.MaxEntries)
        {
            return;
        }

        current.Variables.Add(new SystemVariableEntry
        {
            Id = NextId(current.Variables.Select(entry => entry.Id), "variable_"),
            Label = "Nouvelle variable",
        });
        _session.MarkDirty();
        RebuildVariableList(current.Variables.Count - 1);
        LiveValidate();
        StatusChanged?.Invoke("Modifié (non enregistré)");
    }

    private void RemoveVariable()
    {
        var current = _session.Current;
        var index = _variableList.SelectedIndex;
        if (current is null || index < 0 || index >= current.Variables.Count)
        {
            return;
        }

        current.Variables.RemoveAt(index);
        _session.MarkDirty();
        var next = current.Variables.Count == 0 ? -1 : Math.Min(index, current.Variables.Count - 1);
        RebuildVariableList(next);
        LiveValidate();
        StatusChanged?.Invoke("Modifié (non enregistré)");
    }

    private void WriteSwitch(Action<SystemSwitchEntry> apply)
    {
        if (_binding || _session.Current is null)
        {
            return;
        }

        var index = _switchList.SelectedIndex;
        if (index < 0 || index >= _session.Current.Switches.Count)
        {
            return;
        }

        apply(_session.Current.Switches[index]);
        RefreshListItem(_switchList, ref _suppressSwitch, index, Format(_session.Current.Switches[index]));
        _session.MarkDirty();
        LiveValidate();
        StatusChanged?.Invoke("Modifié (non enregistré)");
    }

    private void WriteVariable(Action<SystemVariableEntry> apply)
    {
        if (_binding || _session.Current is null)
        {
            return;
        }

        var index = _variableList.SelectedIndex;
        if (index < 0 || index >= _session.Current.Variables.Count)
        {
            return;
        }

        apply(_session.Current.Variables[index]);
        RefreshListItem(_variableList, ref _suppressVariable, index, Format(_session.Current.Variables[index]));
        _session.MarkDirty();
        LiveValidate();
        StatusChanged?.Invoke("Modifié (non enregistré)");
    }

    private void BindSwitchFields()
    {
        var entry = Selected(_session.Current?.Switches, _switchList.SelectedIndex);
        _binding = true;
        try
        {
            _switchId.Text = entry?.Id ?? string.Empty;
            _switchLabel.Text = entry?.Label ?? string.Empty;
            _switchNote.Text = entry?.Note ?? string.Empty;
            var enabled = entry is not null;
            _switchId.Enabled = enabled;
            _switchLabel.Enabled = enabled;
            _switchNote.Enabled = enabled;
            _btnRemoveSwitch.Enabled = enabled;
        }
        finally
        {
            _binding = false;
        }
    }

    private void BindVariableFields()
    {
        var entry = Selected(_session.Current?.Variables, _variableList.SelectedIndex);
        _binding = true;
        try
        {
            _variableId.Text = entry?.Id ?? string.Empty;
            _variableLabel.Text = entry?.Label ?? string.Empty;
            _variableNote.Text = entry?.Note ?? string.Empty;
            var enabled = entry is not null;
            _variableId.Enabled = enabled;
            _variableLabel.Enabled = enabled;
            _variableNote.Enabled = enabled;
            _btnRemoveVariable.Enabled = enabled;
        }
        finally
        {
            _binding = false;
        }
    }

    private void RebuildSwitchList(int selectIndex)
    {
        _suppressSwitch = true;
        _switchList.Items.Clear();
        if (_session.Current is not null)
        {
            foreach (var entry in _session.Current.Switches)
            {
                _switchList.Items.Add(Format(entry));
            }
        }

        if (selectIndex >= 0 && selectIndex < _switchList.Items.Count)
        {
            _switchList.SelectedIndex = selectIndex;
        }

        _suppressSwitch = false;
        BindSwitchFields();
    }

    private void RebuildVariableList(int selectIndex)
    {
        _suppressVariable = true;
        _variableList.Items.Clear();
        if (_session.Current is not null)
        {
            foreach (var entry in _session.Current.Variables)
            {
                _variableList.Items.Add(Format(entry));
            }
        }

        if (selectIndex >= 0 && selectIndex < _variableList.Items.Count)
        {
            _variableList.SelectedIndex = selectIndex;
        }

        _suppressVariable = false;
        BindVariableFields();
    }

    private static void RefreshListItem(ListBox list, ref bool suppress, int index, string text)
    {
        if (index < 0 || index >= list.Items.Count)
        {
            return;
        }

        suppress = true;
        list.Items[index] = text;
        if (list.SelectedIndex != index)
        {
            list.SelectedIndex = index;
        }

        suppress = false;
    }

    private void LiveValidate()
    {
        if (_session.Current is null)
        {
            _validation.Text = string.Empty;
            return;
        }

        _validation.Text = _session.Current.Validate(out var error) ? string.Empty : error ?? string.Empty;
    }

    private void BindMeta()
    {
        _meta.Text =
            $"rev={_session.CurrentRevision}  statut={_session.CurrentStatus}  publié={_session.PublishedRevision?.ToString() ?? "—"}";
    }

    private async Task SaveAsync(SaveContentIntent intent)
    {
        var result = await _session.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case SaveSystemResult.Success success:
                EditorSystemNameCatalog.Replace(_session.Current);
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={success.PublishedRevision}"
                        : $"Brouillon enregistré rev={success.NewRevision}");
                BindMeta();
                LiveValidate();
                break;
            case SaveSystemResult.ValidationFailed validation:
                _validation.Text = validation.Error;
                GameDataUiMessageBox.Show(
                    this,
                    validation.Error,
                    "Validation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case SaveSystemResult.Conflict conflict:
                GameDataUiMessageBox.Show(
                    this,
                    $"Conflit de révision (courante={conflict.CurrentRevision}).",
                    "Conflit");
                break;
            case SaveSystemResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveSystemResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private static TabPage BuildPage(
        string title,
        string help,
        ListBox list,
        TextBox id,
        TextBox label,
        TextBox note,
        Button add,
        Button remove)
    {
        var page = new TabPage(title) { Padding = new Padding(8) };
        var left = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(0, 0, 8, 0) };
        left.Controls.Add(list);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string caption, Control control)
        {
            var row = form.RowCount++;
            form.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            form.Controls.Add(control, 1, row);
        }

        Row("Identifiant", id);
        Row("Libellé", label);
        Row("Note", note);

        var actions = new FlowLayoutPanel { AutoSize = true };
        actions.Controls.Add(add);
        actions.Controls.Add(remove);
        Row("", actions);

        var helpLabel = new Label { AutoSize = true, MaximumSize = new Size(420, 0), Text = help };
        Row("", helpLabel);

        page.Controls.Add(form);
        page.Controls.Add(left);
        return page;
    }

    private static string NextId(IEnumerable<string> used, string prefix)
    {
        var taken = new HashSet<string>(used, StringComparer.Ordinal);
        for (var n = 1; n <= SystemDefinition.MaxEntries; n++)
        {
            var id = prefix + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!taken.Contains(id))
            {
                return id;
            }
        }

        return prefix + "1";
    }

    private static string Format(SystemCatalogEntry entry) => $"{entry.Id} — {entry.Label}";

    private static T? Selected<T>(IReadOnlyList<T>? entries, int index)
        where T : class
        => entries is not null && index >= 0 && index < entries.Count ? entries[index] : null;
}
