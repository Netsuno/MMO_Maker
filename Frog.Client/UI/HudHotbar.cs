#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace Frog.Client.UI;

/// <summary>Hotbar 10 slots (1–0). Slots 1–3 branchés ; 4–10 présents mais désactivés (pas factices).</summary>
public sealed class HudHotbar : Panel
{
    public const int SlotCount = 10;

    private readonly Button[] _slots = new Button[SlotCount];

    public event Action<int>? SlotActivated;

    public HudHotbar()
    {
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = UiTheme.BgPanel;
        Size = new Size(412, 56);
        MinimumSize = new Size(360, 48);
        Padding = new Padding(6, 6, 6, 6);
        Paint += DrawChrome;
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(2),
        };
        var labels = new[] { "1 Mêlée", "2 Sort", "3 Act.", "4", "5", "6", "7", "8", "9", "0" };
        for (var i = 0; i < SlotCount; i++)
        {
            var index = i;
            var wired = i < 3;
            var btn = new Button
            {
                Text = labels[i],
                Width = 36,
                Height = 36,
                Margin = new Padding(1),
                Enabled = wired,
                FlatStyle = FlatStyle.Flat,
                Tag = i,
            };
            UiTheme.StyleButton(btn);
            if (!wired)
            {
                btn.ForeColor = UiTheme.TextMuted;
                btn.FlatAppearance.BorderColor = UiTheme.AccentGoldDim;
            }

            btn.Click += (_, _) =>
            {
                if (btn.Enabled)
                {
                    SlotActivated?.Invoke(index);
                }
            };
            _slots[i] = btn;
            row.Controls.Add(btn);
        }

        Controls.Add(row);
    }

    internal int SlotCountForTest => _slots.Length;

    internal bool SlotEnabledForTest(int index) => (uint)index < SlotCount && _slots[index].Enabled;

    internal string SlotTextForTest(int index) => (uint)index < SlotCount ? _slots[index].Text : string.Empty;

    public void ActivateSlot(int index)
    {
        if ((uint)index >= SlotCount || !_slots[index].Enabled)
        {
            return;
        }

        SlotActivated?.Invoke(index);
    }

    private void DrawChrome(object? sender, PaintEventArgs e)
    {
        if (Width < 4 || Height < 4)
        {
            return;
        }

        using var gold = new Pen(UiTheme.AccentGold);
        e.Graphics.DrawRectangle(gold, 0, 0, Width - 1, Height - 1);
    }
}
