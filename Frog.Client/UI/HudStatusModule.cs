#nullable enable
using System.Drawing;
using System.Windows.Forms;
using Frog.Core.Protocol;

namespace Frog.Client.UI;

/// <summary>Status HG — barres HP/MP/XP depuis <see cref="CombatStateWire"/> uniquement.</summary>
public sealed class HudStatusModule : HudModulePanel
{
    private readonly Label _name = new() { AutoSize = true, ForeColor = UiTheme.TextPrimary };
    private readonly Label _meta = new() { AutoSize = true, ForeColor = UiTheme.TextSecondary };
    private readonly Panel _hpTrack = new() { Height = 10, Dock = DockStyle.Top, BackColor = UiTheme.BgInput };
    private readonly Panel _hpFill = new() { Height = 10, BackColor = UiTheme.BarHp };
    private readonly Panel _mpTrack = new() { Height = 10, Dock = DockStyle.Top, BackColor = UiTheme.BgInput, Margin = new Padding(0, 3, 0, 0) };
    private readonly Panel _mpFill = new() { Height = 10, BackColor = UiTheme.BarMp };
    private readonly Panel _xpTrack = new() { Height = 6, Dock = DockStyle.Top, BackColor = UiTheme.BgInput, Margin = new Padding(0, 3, 0, 0) };
    private readonly Panel _xpFill = new() { Height = 6, BackColor = UiTheme.BarXp };
    private readonly Label _dead = new()
    {
        AutoSize = true,
        Text = "Mort — Respawn",
        ForeColor = UiTheme.TextDanger,
        Visible = false,
    };
    private CombatStateWire? _state;

    public HudStatusModule()
        : base("Statut")
    {
        Size = new Size(280, 96);
        MinimumSize = new Size(220, 88);
        _hpTrack.Controls.Add(_hpFill);
        _mpTrack.Controls.Add(_mpFill);
        _xpTrack.Controls.Add(_xpFill);
        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0),
        };
        body.Controls.Add(_name);
        body.Controls.Add(_meta);
        body.Controls.Add(_hpTrack);
        body.Controls.Add(_mpTrack);
        body.Controls.Add(_xpTrack);
        body.Controls.Add(_dead);
        Controls.Add(body);
        body.BringToFront();
        ApplyCombat(null, null);
        _hpTrack.Resize += (_, _) => LayoutBars();
        _mpTrack.Resize += (_, _) => LayoutBars();
        _xpTrack.Resize += (_, _) => LayoutBars();
    }

    internal string NameTextForTest => _name.Text;

    internal string MetaTextForTest => _meta.Text;

    internal bool IsDeadVisibleForTest => _dead.Visible;

    internal int HpFillWidthForTest => _hpFill.Width;

    public void ApplyCombat(CombatStateWire? state, string? playerName)
    {
        _state = state;
        _name.Text = string.IsNullOrWhiteSpace(playerName) ? "—" : playerName.Trim();
        if (state is null)
        {
            _meta.Text = "Lv —  ·  HP —  ·  MP —";
            _dead.Visible = false;
            LayoutBars();
            return;
        }

        _meta.Text = $"Lv {state.Level}  ·  HP {state.Hp}/{state.MaxHp}  ·  MP {state.Mp}/{state.MaxMp}  ·  XP {state.Experience}";
        _dead.Visible = state.IsDead;
        LayoutBars();
    }

    private void LayoutBars()
    {
        var state = _state;
        SetFill(_hpTrack, _hpFill, state?.Hp ?? 0, state?.MaxHp ?? 0);
        SetFill(_mpTrack, _mpFill, state?.Mp ?? 0, state?.MaxMp ?? 0);
        // Pas de max XP filaire : barre informative seulement (vide si XP inconnue / 0).
        SetFill(_xpTrack, _xpFill, state is { Experience: > 0 } ? 1 : 0, state is { Experience: > 0 } ? 1 : 0);
    }

    private static void SetFill(Panel track, Panel fill, int value, int max)
    {
        var ratio = max <= 0 ? 0d : Math.Clamp(value / (double)max, 0d, 1d);
        fill.Width = Math.Max(0, (int)Math.Round(track.Width * ratio));
        fill.Height = track.Height;
        fill.Location = new Point(0, 0);
    }
}
