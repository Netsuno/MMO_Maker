using System.Globalization;
using Frog.Application.Content;
using Frog.Core.Models;

namespace Frog.Editor.Forms.GameData;

/// <summary>Liste + formulaire boutique (contenu uniquement, sans gameplay commercial).</summary>
public sealed class ShopEditorPanel : UserControl
{
    private readonly GameDataPanelAsyncGate _asyncGate = new();
    private readonly ShopWorkspaceSession _session;
    private readonly IPublishedItemCatalog _itemCatalog;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new()
    {
        Dock = DockStyle.Top,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly TextBox _description = new()
    {
        Width = 360,
        Height = 70,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
    };
    private readonly DataGridView _listings = new()
    {
        Width = 520,
        Height = 240,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        RowHeadersVisible = false,
    };
    private readonly DataGridViewComboBoxColumn _itemColumn = new()
    {
        HeaderText = "Objet publié",
        Width = 260,
        FlatStyle = FlatStyle.Flat,
    };
    private readonly Button _btnAddListing = new() { Text = "Ajouter article", AutoSize = true };
    private readonly Button _btnRemoveListing = new() { Text = "Retirer article", AutoSize = true };
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDup = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private bool _suppressList;
    private bool _binding;

    internal Button BtnNewForTest => _btnNew;

    internal Button BtnDupForTest => _btnDup;

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal Button BtnDeleteForTest => _btnDelete;

    internal Button BtnAddListingForTest => _btnAddListing;

    internal TextBox NameForTest => _name;

    internal TextBox SearchForTest => _search;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal DataGridView ListingsForTest => _listings;

    internal ListBox ListForTest => _list;

    public ShopEditorPanel(
        ShopWorkspaceSession session,
        IPublishedItemCatalog itemCatalog,
        ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _itemCatalog = itemCatalog;
        _capabilities = capabilities;

        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;
        _listings.Columns.Add(_itemColumn);
        _listings.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Prix",
            Width = 100,
        });
        _listings.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Stock (vide = ∞)",
            Width = 130,
        });

        var left = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(4) };
        left.Controls.Add(_list);
        left.Controls.Add(_search);
        left.Controls.Add(_statusFilter);

        var listingEditor = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        listingEditor.Controls.Add(_listings);
        var listingButtons = new FlowLayoutPanel { AutoSize = true };
        listingButtons.Controls.AddRange(new Control[] { _btnAddListing, _btnRemoveListing });
        listingEditor.Controls.Add(listingButtons);

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
        Row("Description", _description);
        Row("Articles", listingEditor);
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

        _search.TextChanged += (_, _) => _ = _asyncGate.RunAsync(async ct =>
        {
            _session.SearchFilter = _search.Text;
            await RefreshListAsync(ct).ConfigureAwait(true);
        });
        _statusFilter.SelectedIndexChanged += (_, _) => _ = _asyncGate.RunAsync(async ct =>
        {
            _session.StatusFilter = _statusFilter.SelectedIndex switch
            {
                1 => ContentPublishStatus.Draft,
                2 => ContentPublishStatus.Published,
                _ => null,
            };
            await RefreshListAsync(ct).ConfigureAwait(true);
        });
        _list.SelectedIndexChanged += (_, _) => _ = _asyncGate.RunAsync(async _ =>
        {
            if (_suppressList || _list.SelectedItem is not CatalogItem item)
            {
                return;
            }

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Boutiques", _session.IsDirty))
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
        });

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
        _listings.CellValueChanged += (_, _) => Mark();
        _listings.RowsRemoved += (_, _) => Mark();
        _listings.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_listings.IsCurrentCellDirty)
            {
                _listings.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _listings.DataError += (_, eventArgs) =>
        {
            eventArgs.ThrowException = false;
            _validation.Text = "Article de boutique invalide.";
        };

        _btnAddListing.Click += (_, _) =>
        {
            if (_itemColumn.Items.Count == 0)
            {
                return;
            }

            _listings.Rows.Add(_itemColumn.Items[0], "0", string.Empty);
            Mark();
        };
        _btnRemoveListing.Click += (_, _) =>
        {
            if (_listings.CurrentRow is { } row && !row.IsNewRow)
            {
                _listings.Rows.Remove(row);
            }
        };
        _btnNew.Click += (_, _) =>
        {
            _session.AdoptNewDraft(new ShopDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Nouvelle boutique",
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
        _btnSave.Click += async (_, _) =>
            await SaveAsync(SaveContentIntent.SaveDraft).ConfigureAwait(true);
        _btnPublish.Click += async (_, _) =>
            await SaveAsync(SaveContentIntent.Publish).ConfigureAwait(true);
        _btnDelete.Click += async (_, _) => await DeleteAsync().ConfigureAwait(true);

        var canWrite = _capabilities.AllowsSave;
        _btnSave.Enabled = canWrite;
        _btnPublish.Enabled = canWrite;
        _btnDelete.Enabled = canWrite;
        _btnAddListing.Enabled = canWrite;
        _btnRemoveListing.Enabled = canWrite;
    }

    public event Action<string>? StatusChanged;

    public bool IsDirty => _session.IsDirty;

    internal Task DrainAsync() => _asyncGate.DrainAsync(TimeSpan.FromSeconds(5));

    public async Task InitializeAsync()
    {
        await RefreshPublishedItemsAsync().ConfigureAwait(true);
        await RefreshListAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend boutiques : {_capabilities.DisplayLabel}");
    }

    private async Task RefreshPublishedItemsAsync()
    {
        var items = await _itemCatalog.ListPublishedAsync().ConfigureAwait(true);
        _binding = true;
        try
        {
            _itemColumn.Items.Clear();
            foreach (var item in items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                _itemColumn.Items.Add(new ItemChoice(item.Id, item.Name));
            }

            _btnAddListing.Enabled = _capabilities.AllowsSave && _itemColumn.Items.Count > 0;
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
                entry.ShopId,
                $"{entry.Name} ({entry.ListingCount} article(s)) [{entry.Status}]"));
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
            _listings.Rows.Clear();
            foreach (var listing in definition.Listings)
            {
                var choice = _itemColumn.Items
                    .Cast<ItemChoice>()
                    .FirstOrDefault(item => item.Id == listing.ItemId);
                if (choice is null)
                {
                    choice = new ItemChoice(listing.ItemId, $"[introuvable] {listing.ItemId:N}");
                    _itemColumn.Items.Add(choice);
                }

                _listings.Rows.Add(
                    choice,
                    listing.Price.ToString(CultureInfo.InvariantCulture),
                    listing.Stock?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            }

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
        var listings = new List<ShopListing>();
        foreach (DataGridViewRow row in _listings.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var priceText = Convert.ToString(row.Cells[1].Value, CultureInfo.InvariantCulture);
            var stockText = Convert.ToString(row.Cells[2].Value, CultureInfo.InvariantCulture);
            listings.Add(new ShopListing
            {
                ItemId = row.Cells[0].Value is ItemChoice choice ? choice.Id : Guid.Empty,
                Price = int.TryParse(
                    priceText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var price)
                    ? price
                    : -1,
                Stock = string.IsNullOrWhiteSpace(stockText)
                    ? null
                    : int.TryParse(
                        stockText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var stock)
                        ? stock
                        : -1,
            });
        }

        _session.Current.Listings = listings;
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
            case SaveShopResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={success.PublishedRevision}"
                        : $"Brouillon enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveShopResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(
                    this,
                    validation.Error,
                    "Validation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case SaveShopResult.Conflict conflict:
                GameDataUiMessageBox.Show(
                    this,
                    $"Conflit de révision (courante={conflict.CurrentRevision}).",
                    "Conflit");
                break;
            case SaveShopResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveShopResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteShopResult.Success:
                StatusChanged?.Invoke("Supprimée");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteShopResult.NotFound:
                GameDataUiMessageBox.Show(this, "Boutique introuvable.");
                break;
            case DeleteShopResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record ItemChoice(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}
