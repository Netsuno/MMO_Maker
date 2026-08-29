using System;
using System.Windows.Forms;

namespace Frog.Client.Controls;

/// <summary>Craft recette (<see cref="Frog.Core.Enums.PacketId.CraftRequest"/>) avec requestId idempotent.</summary>
public sealed class CraftPanel : UserControl
{
    private readonly TextBox _txtRecipeId = new() { Width = 240, PlaceholderText = "Recette Guid" };
    private readonly Button _btnCraft = new() { Text = "Craft", AutoSize = true, Enabled = false };
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
        row.Controls.Add(_txtRecipeId);
        row.Controls.Add(_btnCraft);
        flow.Controls.Add(row);
        flow.Controls.Add(_lblStatus);
        Controls.Add(flow);
        _btnCraft.Click += (_, _) =>
        {
            if (Guid.TryParse(_txtRecipeId.Text.Trim(), out var recipeId) && recipeId != Guid.Empty)
            {
                CraftRequested?.Invoke(recipeId);
            }
        };
    }

    public void SetCraftEnabled(bool enabled) => _btnCraft.Enabled = enabled;

    public void SetStatus(string message) => _lblStatus.Text = message;

    internal TextBox RecipeIdTextBoxForTest => _txtRecipeId;

    internal Button CraftButtonForTest => _btnCraft;

    internal string StatusTextForTest => _lblStatus.Text;

    internal void ClickCraftForTest() => _btnCraft.PerformClick();
}
