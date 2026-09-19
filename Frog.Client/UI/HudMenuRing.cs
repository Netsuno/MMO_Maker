#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace Frog.Client.UI;

public enum HudMenuCommand
{
    Character,
    Inventory,
    Quests,
    Map,
    Options,
}

/// <summary>Menu BD — 5 pills vers des surfaces live (onglets / Options), jamais de bouton factice.</summary>
public sealed class HudMenuRing : Panel
{
    private readonly Button[] _pills;

    public event Action<HudMenuCommand>? Command;

    public HudMenuRing()
    {
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Size = new Size(280, 40);
        MinimumSize = new Size(220, 36);
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
        };
        var items = new (string Text, HudMenuCommand Cmd)[]
        {
            ("Perso", HudMenuCommand.Character),
            ("Inv", HudMenuCommand.Inventory),
            ("Quêtes", HudMenuCommand.Quests),
            ("Carte", HudMenuCommand.Map),
            ("Options", HudMenuCommand.Options),
        };
        _pills = new Button[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var btn = new Button
            {
                Text = item.Text,
                AutoSize = true,
                MinimumSize = new Size(48, 28),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(2, 0, 2, 0),
            };
            UiTheme.StyleButton(btn);
            btn.Click += (_, _) => Command?.Invoke(item.Cmd);
            _pills[i] = btn;
            row.Controls.Add(btn);
        }

        Controls.Add(row);
    }

    internal int PillCountForTest => _pills.Length;

    internal IReadOnlyList<string> PillTextsForTest => _pills.Select(p => p.Text).ToArray();
}
