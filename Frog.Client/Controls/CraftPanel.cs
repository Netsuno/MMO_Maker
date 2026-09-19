using System;
using System.Windows.Forms;
using Frog.Core.Protocol;

namespace Frog.Client.Controls;

/// <summary>Craft recette (<see cref="Frog.Core.Enums.PacketId.CraftRequest"/>) par nom, Guid hors UI normale.</summary>
public sealed class CraftPanel : UserControl
{
    private readonly ComboBox _cmbRecipe = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 240,
        Enabled = false,
    };
    private readonly TextBox _txtRecipeId = new()
    {
        Width = 240,
        PlaceholderText = "Recette Guid (secours)",
        Visible = false,
    };
    private readonly Button _btnCraft = new() { Text = "Fabriquer", AutoSize = true, Enabled = false };
    private readonly Label _lblStatus = new() { AutoSize = true, Text = "—", Margin = new Padding(4, 8, 4, 4) };

    public event Action<Guid>? CraftRequested;

    public CraftPanel()
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(4),
        };
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };
        row.Controls.Add(new Label { Text = "Recette", AutoSize = true, Margin = new Padding(0, 8, 4, 4) });
        row.Controls.Add(_cmbRecipe);
        row.Controls.Add(_txtRecipeId);
        row.Controls.Add(_btnCraft);
        flow.Controls.Add(row);
        flow.Controls.Add(_lblStatus);
        Controls.Add(flow);
        _cmbRecipe.SelectedIndexChanged += (_, _) => SyncHiddenRecipeId();
        _btnCraft.Click += (_, _) =>
        {
            if (TryGetSelectedRecipeId(out var recipeId))
            {
                CraftRequested?.Invoke(recipeId);
            }
        };
    }

    public void BindRecipes(IReadOnlyList<PublishedRecipeWireEntry> recipes)
    {
        var previous = Guid.Empty;
        if (_cmbRecipe.SelectedItem is RecipePickRow selected)
        {
            previous = selected.Id;
        }

        _cmbRecipe.Items.Clear();
        foreach (var entry in recipes)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            if (Guid.TryParse(entry.Id, out var recipeId) && recipeId != Guid.Empty)
            {
                _cmbRecipe.Items.Add(new RecipePickRow(recipeId, entry.Name));
            }
        }

        if (_cmbRecipe.Items.Count == 0)
        {
            SyncHiddenRecipeId();
            return;
        }

        var restore = -1;
        if (previous != Guid.Empty)
        {
            for (var i = 0; i < _cmbRecipe.Items.Count; i++)
            {
                if (_cmbRecipe.Items[i] is RecipePickRow row && row.Id == previous)
                {
                    restore = i;
                    break;
                }
            }
        }

        _cmbRecipe.SelectedIndex = restore >= 0 ? restore : 0;
        SyncHiddenRecipeId();
    }

    public void ClearRecipes()
    {
        _cmbRecipe.Items.Clear();
        _txtRecipeId.Text = string.Empty;
    }

    public void SetCraftEnabled(bool enabled)
    {
        _btnCraft.Enabled = enabled && (_cmbRecipe.Items.Count > 0 || Guid.TryParse(_txtRecipeId.Text.Trim(), out _));
        _cmbRecipe.Enabled = enabled && _cmbRecipe.Items.Count > 0;
    }

    public void SetStatus(string message) => _lblStatus.Text = message;

    internal ComboBox RecipeComboForTest => _cmbRecipe;

    internal TextBox RecipeIdTextBoxForTest => _txtRecipeId;

    internal Button CraftButtonForTest => _btnCraft;

    internal string StatusTextForTest => _lblStatus.Text;

    internal string SelectedRecipeDisplayForTest => _cmbRecipe.SelectedItem?.ToString() ?? string.Empty;

    internal void ClickCraftForTest() => _btnCraft.PerformClick();

    internal bool TrySelectRecipeForTest(Guid recipeId)
    {
        for (var i = 0; i < _cmbRecipe.Items.Count; i++)
        {
            if (_cmbRecipe.Items[i] is RecipePickRow row && row.Id == recipeId)
            {
                _cmbRecipe.SelectedIndex = i;
                SyncHiddenRecipeId();
                return true;
            }
        }

        return false;
    }

    private bool TryGetSelectedRecipeId(out Guid recipeId)
    {
        recipeId = Guid.Empty;
        if (_cmbRecipe.SelectedItem is RecipePickRow row && row.Id != Guid.Empty)
        {
            recipeId = row.Id;
            return true;
        }

        return Guid.TryParse(_txtRecipeId.Text.Trim(), out recipeId) && recipeId != Guid.Empty;
    }

    private void SyncHiddenRecipeId()
    {
        if (_cmbRecipe.SelectedItem is RecipePickRow row)
        {
            _txtRecipeId.Text = row.Id.ToString("D");
        }
    }

    private sealed class RecipePickRow(Guid id, string name)
    {
        public Guid Id { get; } = id;

        public string Name { get; } = name;

        public override string ToString() => Name;
    }
}
