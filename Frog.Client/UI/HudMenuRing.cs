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

/// <summary>
/// Menu BD — 5 pills live (onglets / Options). DA v2 step 1: dark circle + cream icon/label;
/// gold is the border only. Count stays at the existing 5 (step 4 adds layout, not wiring).
/// </summary>
public sealed class HudMenuRing : Panel
{
    private readonly Button[] _pills;

    public event Action<HudMenuCommand>? Command;

    public HudMenuRing()
    {
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Size = new Size(300, 44);
        MinimumSize = new Size(220, 40);
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
                MinimumSize = new Size(36, 36),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(2, 0, 2, 0),
            };
            UiTheme.StyleContrastHudButton(btn, enabled: true);

            var icon = UiPackAssets.CloneMenuIcon(item.Cmd);
            if (icon is not null)
            {
                btn.Image = icon;
                btn.ImageAlign = ContentAlignment.MiddleLeft;
                btn.TextAlign = ContentAlignment.MiddleRight;
                btn.TextImageRelation = TextImageRelation.ImageBeforeText;
                btn.Padding = new Padding(4, 0, 6, 0);
            }

            btn.Click += (_, _) => Command?.Invoke(item.Cmd);
            _pills[i] = btn;
            row.Controls.Add(btn);
        }

        Controls.Add(row);
    }

    internal int PillCountForTest => _pills.Length;

    internal IReadOnlyList<string> PillTextsForTest => _pills.Select(p => p.Text).ToArray();

    internal bool PillHasIconForTest(int index) =>
        (uint)index < _pills.Length && _pills[index].Image is not null;

    internal bool PillHasChromeForTest(int index) =>
        (uint)index < _pills.Length && HudHotbar.UsesContrastChrome(_pills[index]);

    internal Color PillBackColorForTest(int index) =>
        (uint)index < _pills.Length ? _pills[index].BackColor : Color.Empty;

    internal Color PillForeColorForTest(int index) =>
        (uint)index < _pills.Length ? _pills[index].ForeColor : Color.Empty;

    internal Color PillBorderColorForTest(int index) =>
        (uint)index < _pills.Length ? _pills[index].FlatAppearance.BorderColor : Color.Empty;
}
