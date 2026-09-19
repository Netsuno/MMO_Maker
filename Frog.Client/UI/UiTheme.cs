#nullable enable
using System.Drawing;
using System.Windows.Forms;
using Frog.Client.Controls;

namespace Frog.Client.UI;

/// <summary>
/// Jetons DA ([TOKENS-DA.md]) + application unique (pas sur le timer 16 ms).
/// Fallback panel = solid (WinForms / perf). Ne peint pas la carte.
/// </summary>
public static class UiTheme
{
    public static readonly Color BgApp = Color.FromArgb(0x0E, 0x12, 0x18);
    public static readonly Color BgPanel = Color.FromArgb(0x16, 0x1C, 0x28);
    public static readonly Color BgPanelHeader = Color.FromArgb(0x1A, 0x22, 0x30);
    public static readonly Color BgInput = Color.FromArgb(0x0A, 0x0E, 0x14);
    public static readonly Color BgRowSelected = Color.FromArgb(0x24, 0x34, 0x4A);
    public static readonly Color AccentGold = Color.FromArgb(0xC9, 0xA2, 0x27);
    public static readonly Color AccentGoldHi = Color.FromArgb(0xE8, 0xC5, 0x47);
    public static readonly Color AccentGoldDim = Color.FromArgb(0x8A, 0x70, 0x18);
    public static readonly Color TextPrimary = Color.FromArgb(0xF2, 0xF4, 0xF8);
    public static readonly Color TextSecondary = Color.FromArgb(0xA8, 0xB0, 0xC0);
    public static readonly Color TextMuted = Color.FromArgb(0x6B, 0x73, 0x85);
    public static readonly Color TextGold = Color.FromArgb(0xE8, 0xC5, 0x47);
    public static readonly Color TextDanger = Color.FromArgb(0xE8, 0x5D, 0x5D);
    public static readonly Color BarHp = Color.FromArgb(0xC6, 0x28, 0x28);
    public static readonly Color BarMp = Color.FromArgb(0x15, 0x65, 0xC0);
    public static readonly Color BarXp = Color.FromArgb(0x2E, 0x7D, 0x32);
    public static readonly Color StateError = Color.FromArgb(0xB7, 0x1C, 0x1C);

    /// <summary>Panels Phase 8 encore gated exact-sha — ne pas les recolorer en E0.</summary>
    public static bool IsPhase8ExactShaSurface(Control control) =>
        control is DialoguePanel or QuestJournalPanel or EnvironmentPanel;

    public static Font UiFont(float emSize, FontStyle style = FontStyle.Regular)
    {
        var family = SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif;
        return new Font(family, emSize, style);
    }

    public static void StyleButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = AccentGold;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = BgRowSelected;
        button.BackColor = BgPanelHeader;
        button.ForeColor = TextPrimary;
        button.UseVisualStyleBackColor = false;
        if (button.Font.Name.Contains("Pixel", StringComparison.OrdinalIgnoreCase))
        {
            button.Font = UiFont(button.Font.SizeInPoints, button.Font.Style);
        }
    }

    public static void StyleInput(Control control)
    {
        control.BackColor = BgInput;
        control.ForeColor = TextPrimary;
        if (control is TextBoxBase box)
        {
            box.BorderStyle = BorderStyle.FixedSingle;
        }
    }

    /// <summary>Thème récursif, une fois à la construction. Ignore PictureBox (carte) et surfaces SHA Phase 8.</summary>
    public static void Apply(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        ApplyCore(root, skipShaSurfaces: true);
    }

    public static void ApplyIncludingShaSurfaces(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        ApplyCore(root, skipShaSurfaces: false);
    }

    private static void ApplyCore(Control control, bool skipShaSurfaces)
    {
        if (control is PictureBox)
        {
            return;
        }

        if (skipShaSurfaces && IsPhase8ExactShaSurface(control))
        {
            return;
        }

        switch (control)
        {
            case Button button:
                StyleButton(button);
                break;
            case TextBoxBase:
            case NumericUpDown:
            case ComboBox:
                StyleInput(control);
                if (control is ComboBox combo)
                {
                    combo.FlatStyle = FlatStyle.Flat;
                }

                break;
            case ListBox list:
                list.BackColor = BgInput;
                list.ForeColor = TextPrimary;
                list.BorderStyle = BorderStyle.FixedSingle;
                break;
            case TabControl tabs:
                tabs.BackColor = BgPanel;
                foreach (TabPage page in tabs.TabPages)
                {
                    page.BackColor = BgPanel;
                    page.ForeColor = TextPrimary;
                }

                break;
            case TabPage page:
                page.BackColor = BgPanel;
                page.ForeColor = TextPrimary;
                break;
            case Label label:
                label.ForeColor = label.Font.Bold && label.Font.SizeInPoints >= 13f
                    ? TextGold
                    : TextPrimary;
                label.BackColor = Color.Transparent;
                break;
            case CheckBox check:
                check.ForeColor = TextPrimary;
                check.BackColor = Color.Transparent;
                break;
            case Form form:
                form.BackColor = BgApp;
                form.ForeColor = TextPrimary;
                break;
            case Panel or FlowLayoutPanel or TableLayoutPanel or UserControl:
                control.BackColor = control.Parent is Form ? BgApp : BgPanel;
                control.ForeColor = TextPrimary;
                break;
            default:
                control.ForeColor = TextPrimary;
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyCore(child, skipShaSurfaces);
        }
    }
}
