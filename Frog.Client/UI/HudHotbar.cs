#nullable enable
using System.Drawing;
using System.Windows.Forms;
using Frog.Client.Config;

namespace Frog.Client.UI;

/// <summary>
/// Hotbar 10 slots (1–0). Chiffre dans la case ; libellé en tooltip. Slots 4–10 disabled.
/// DA v2 step 1: <c>bg.slot</c> fill + gold border; cream digits/icons (no or-sur-or).
/// </summary>
public sealed class HudHotbar : Panel
{
    public const int SlotCount = 10;

    private readonly Button[] _slots = new Button[SlotCount];
    private readonly string[] _tooltips;
    private readonly ToolTip _tips = new();

    public event Action<int>? SlotActivated;

    public HudHotbar()
    {
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = UiTheme.BgPanel;
        Size = new Size(392, 52);
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
        var digits = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
        _tooltips = new[]
        {
            "Mêlée (1) — poison",
            "Sort (2)",
            "Interagir (3)",
            "Distance (4) — étourdissement",
            "Slot 5 — non lié",
            "Slot 6 — non lié",
            "Slot 7 — non lié",
            "Slot 8 — non lié",
            "Slot 9 — non lié",
            "Slot 0 — non lié",
        };
        for (var i = 0; i < SlotCount; i++)
        {
            var index = i;
            var wired = i < 4;
            var btn = new Button
            {
                Text = digits[i],
                Width = 36,
                Height = 36,
                Margin = new Padding(1),
                Enabled = wired,
                FlatStyle = FlatStyle.Flat,
                Tag = i,
                Font = UiTheme.UiFont(10f, FontStyle.Bold),
            };
            UiTheme.StyleContrastHudButton(btn, wired);

            var icon = UiPackAssets.CloneHotbarIcon(index);
            if (icon is not null)
            {
                btn.Image = icon;
                btn.ImageAlign = ContentAlignment.TopCenter;
                btn.TextAlign = ContentAlignment.BottomRight;
                btn.TextImageRelation = TextImageRelation.Overlay;
                btn.Padding = new Padding(1);
            }

            _tips.SetToolTip(btn, _tooltips[i]);
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

    /// <summary>À 100 % : barre 392×52, case 36 — mêmes chiffres que le constructeur.</summary>
    public void ApplyUiScale(int percent)
    {
        var slot = ClientUiScale.ScaleDip(36, percent);
        MinimumSize = new Size(ClientUiScale.ScaleDip(360, percent), ClientUiScale.ScaleDip(48, percent));
        Size = new Size(ClientUiScale.ScaleDip(392, percent), ClientUiScale.ScaleDip(52, percent));
        Padding = new Padding(ClientUiScale.ScaleDip(6, percent));
        foreach (var btn in _slots)
        {
            btn.Width = slot;
            btn.Height = slot;
        }
    }

    internal int SlotCountForTest => _slots.Length;

    internal bool SlotEnabledForTest(int index) => (uint)index < SlotCount && _slots[index].Enabled;

    internal string SlotTextForTest(int index) => (uint)index < SlotCount ? _slots[index].Text : string.Empty;

    internal string SlotToolTipForTest(int index) => (uint)index < SlotCount ? _tooltips[index] : string.Empty;

    internal bool SlotHasChromeForTest(int index) =>
        (uint)index < SlotCount && UsesContrastChrome(_slots[index]);

    internal bool SlotHasIconForTest(int index) =>
        (uint)index < SlotCount && _slots[index].Image is not null;

    internal Color SlotBackColorForTest(int index) =>
        (uint)index < SlotCount ? _slots[index].BackColor : Color.Empty;

    internal Color SlotForeColorForTest(int index) =>
        (uint)index < SlotCount ? _slots[index].ForeColor : Color.Empty;

    internal Color SlotBorderColorForTest(int index) =>
        (uint)index < SlotCount ? _slots[index].FlatAppearance.BorderColor : Color.Empty;

    internal static bool UsesContrastChrome(Button button)
    {
        var border = button.Enabled ? UiTheme.AccentGold : UiTheme.AccentGoldDim;
        var text = button.Enabled ? UiTheme.TextPrimary : UiTheme.TextMuted;
        return button.BackColor.ToArgb() == UiTheme.BgSlot.ToArgb()
            && button.FlatAppearance.BorderColor.ToArgb() == border.ToArgb()
            && button.ForeColor.ToArgb() == text.ToArgb()
            && button.FlatAppearance.BorderSize == 1
            && button.BackgroundImage is null;
    }

    public void ActivateSlot(int index)
    {
        if ((uint)index >= SlotCount || !_slots[index].Enabled)
        {
            return;
        }

        SlotActivated?.Invoke(index);
    }

    /// <summary>Flash court du slot mêlée (0) — restaure le chrome DA v2 ensuite.</summary>
    public void FlashMeleeSlot()
    {
        var btn = _slots[0];
        var restoreBack = btn.BackColor;
        var restoreFore = btn.ForeColor;
        btn.BackColor = Color.FromArgb(180, 70, 40);
        btn.ForeColor = UiTheme.TextPrimary;
        var timer = new System.Windows.Forms.Timer { Interval = 120 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            btn.BackColor = restoreBack;
            btn.ForeColor = restoreFore;
        };
        timer.Start();
    }

    private void DrawChrome(object? sender, PaintEventArgs e)
    {
        if (Width < 4 || Height < 4)
        {
            return;
        }

        if (UiPackAssets.TryGetFrameInset(out var inset))
        {
            using var tint = UiTheme.CreatePanelTintAttributes();
            UiPackDraw.NineSlice(e.Graphics, inset, ClientRectangle, UiPackDraw.PanelNineSliceBorder, tint);
        }

        using var gold = new Pen(UiTheme.AccentGold);
        e.Graphics.DrawRectangle(gold, 0, 0, Width - 1, Height - 1);
    }
}
