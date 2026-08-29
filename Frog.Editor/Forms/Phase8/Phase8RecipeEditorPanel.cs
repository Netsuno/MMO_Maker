using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Éditeur structuré de recette (métier, ingrédients, sortie).</summary>
internal sealed class Phase8RecipeEditorPanel : Phase8EditorPanelBase
{
    private readonly TextBox _professionId = new() { Width = 320 };
    private readonly NumericUpDown _requiredLevel = new() { Minimum = 1, Maximum = 999, Width = 80, Value = 1 };
    private readonly TextBox _outputItemId = new() { Width = 320 };
    private readonly NumericUpDown _outputQty = new() { Minimum = 1, Maximum = 9999, Width = 80, Value = 1 };
    private readonly DataGridView _ingredients = new()
    {
        Width = 520,
        Height = 200,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        RowHeadersVisible = false,
    };

    public Phase8RecipeEditorPanel()
    {
        _ingredients.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ItemId (GUID)", Width = 280 });
        _ingredients.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Quantité", Width = 80 });

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
            Padding = new Padding(8),
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void Row(string label, Control control)
        {
            var row = form.RowCount++;
            form.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            form.Controls.Add(control, 1, row);
        }

        Row("ProfessionId", _professionId);
        Row("Niveau requis", _requiredLevel);
        Row("OutputItemId", _outputItemId);
        Row("OutputQuantity", _outputQty);

        var ingPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        ingPanel.Controls.Add(_ingredients);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var btnAdd = new Button { Text = "Ajouter ingrédient", AutoSize = true };
        var btnRemove = new Button { Text = "Retirer ingrédient", AutoSize = true };
        btnAdd.Click += (_, _) =>
        {
            _ingredients.Rows.Add(Guid.NewGuid().ToString("D"), "1");
            NotifyChanged();
        };
        btnRemove.Click += (_, _) =>
        {
            if (_ingredients.CurrentRow is { IsNewRow: false } row)
            {
                _ingredients.Rows.Remove(row);
                NotifyChanged();
            }
        };
        buttons.Controls.Add(btnAdd);
        buttons.Controls.Add(btnRemove);
        ingPanel.Controls.Add(buttons);
        Row("Ingrédients", ingPanel);

        Controls.Add(form);

        _professionId.TextChanged += (_, _) => NotifyChanged();
        _requiredLevel.ValueChanged += (_, _) => NotifyChanged();
        _outputItemId.TextChanged += (_, _) => NotifyChanged();
        _outputQty.ValueChanged += (_, _) => NotifyChanged();
        _ingredients.CellValueChanged += (_, _) => NotifyChanged();
        _ingredients.RowsRemoved += (_, _) => NotifyChanged();
        _ingredients.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_ingredients.IsCurrentCellDirty)
            {
                _ingredients.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
    }

    public override Phase8ContentKind Kind => Phase8ContentKind.Recipe;

    public override void LoadPayload(string payloadJson)
    {
        if (!Phase8ContentPostgreSqlService.TryDeserialize(payloadJson, out RecipeDefinition def, out _))
        {
            def = new RecipeDefinition();
        }

        ContentId = def.Id;
        Binding = true;
        try
        {
            _professionId.Text = def.ProfessionId.ToString("D");
            _requiredLevel.Value = Math.Max(1, def.RequiredProfessionLevel);
            _outputItemId.Text = def.OutputItemId.ToString("D");
            _outputQty.Value = Math.Max(1, def.OutputQuantity);
            _ingredients.Rows.Clear();
            foreach (var ing in def.Ingredients)
            {
                _ingredients.Rows.Add(ing.ItemId.ToString("D"), ing.Quantity.ToString());
            }
        }
        finally
        {
            Binding = false;
        }
    }

    public override bool TryBuildPayload(out string payloadJson, out string? error)
    {
        if (!Guid.TryParse(_professionId.Text.Trim(), out var professionId))
        {
            payloadJson = string.Empty;
            error = "ProfessionId invalide.";
            return false;
        }

        if (!Guid.TryParse(_outputItemId.Text.Trim(), out var outputItemId))
        {
            payloadJson = string.Empty;
            error = "OutputItemId invalide.";
            return false;
        }

        var ingredients = new List<RecipeIngredientDefinition>();
        foreach (DataGridViewRow row in _ingredients.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            if (!Guid.TryParse(Convert.ToString(row.Cells[0].Value)?.Trim(), out var itemId))
            {
                payloadJson = string.Empty;
                error = "ItemId ingrédient invalide.";
                return false;
            }

            if (!int.TryParse(Convert.ToString(row.Cells[1].Value), out var qty) || qty <= 0)
            {
                payloadJson = string.Empty;
                error = "Quantité ingrédient invalide.";
                return false;
            }

            ingredients.Add(new RecipeIngredientDefinition { ItemId = itemId, Quantity = qty });
        }

        var def = new RecipeDefinition
        {
            Id = ContentId,
            ProfessionId = professionId,
            RequiredProfessionLevel = (int)_requiredLevel.Value,
            OutputItemId = outputItemId,
            OutputQuantity = (int)_outputQty.Value,
            Ingredients = ingredients,
        };
        if (!def.Validate(out error))
        {
            payloadJson = string.Empty;
            return false;
        }

        payloadJson = Phase8ContentPostgreSqlService.Serialize(def);
        error = null;
        return true;
    }

    public override void ResetForNew(Guid newId)
    {
        LoadPayload(Phase8ContentPostgreSqlService.CreateDefaultPayload(Phase8ContentKind.Recipe, newId, "Nouvelle recette"));
    }
}
