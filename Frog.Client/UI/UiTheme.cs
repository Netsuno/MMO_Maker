#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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

    /// <summary>
    /// ColorMatrix vers <see cref="BgPanel"/> (#161C28). Réutilise les jetons DA, pas un nouveau thème.
    /// </summary>
    public static ImageAttributes CreatePanelTintAttributes()
    {
        var matrix = new ColorMatrix(new float[][]
        {
            new float[] { 0.22f, 0.00f, 0.00f, 0f, 0f },
            new float[] { 0.00f, 0.24f, 0.00f, 0f, 0f },
            new float[] { 0.00f, 0.00f, 0.30f, 0f, 0f },
            new float[] { 0f, 0f, 0f, 1f, 0f },
            new float[] { 0.06f, 0.08f, 0.12f, 0f, 1f },
        });
        var attrs = new ImageAttributes();
        attrs.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        return attrs;
    }

    /// <summary>
    /// ColorMatrix or <see cref="AccentGold"/> (#C9A227) pour icônes blanches game-icons.
    /// </summary>
    public static ImageAttributes CreateGoldTintAttributes()
    {
        var r = AccentGold.R / 255f;
        var g = AccentGold.G / 255f;
        var b = AccentGold.B / 255f;
        var matrix = new ColorMatrix(new float[][]
        {
            new float[] { r, 0f, 0f, 0f, 0f },
            new float[] { 0f, g, 0f, 0f, 0f },
            new float[] { 0f, 0f, b, 0f, 0f },
            new float[] { 0f, 0f, 0f, 1f, 0f },
            new float[] { 0f, 0f, 0f, 0f, 1f },
        });
        var attrs = new ImageAttributes();
        attrs.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        return attrs;
    }

    public static Bitmap TintCopy(Image source, Size size, ImageAttributes attributes)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(attributes);
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var dest = new Rectangle(0, 0, size.Width, size.Height);
        g.DrawImage(source, dest, 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        return bmp;
    }

    /// <summary>Skin CTA Kenney (chat send). Ne touche pas les surfaces SHA Phase 8.</summary>
    public static void StyleCta(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);
        StyleButton(button);
        var cta = UiPackAssets.CloneCta();
        if (cta is null)
        {
            return;
        }

        button.BackgroundImage?.Dispose();
        button.BackgroundImage = cta;
        button.BackgroundImageLayout = ImageLayout.Stretch;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = AccentGold;
    }

    /// <summary>Double filet or (E6) — ne pas appeler sur surfaces SHA Phase 8.</summary>
    public static void PaintDoubleGoldFrame(Control control, PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(e);
        if (control.Width < 6 || control.Height < 6)
        {
            return;
        }

        using var dim = new Pen(AccentGoldDim);
        using var gold = new Pen(AccentGold);
        e.Graphics.DrawRectangle(dim, 0, 0, control.Width - 1, control.Height - 1);
        e.Graphics.DrawRectangle(gold, 1, 1, control.Width - 3, control.Height - 3);
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
        IsolatePhase8ExactShaSurfaces(root);
    }

    /// <summary>
    /// ForeColor/BackColor are ambient. Theming the parent TabPage/TLP would
    /// otherwise recolor skipped SHA panels (exact-sha 02–04) without visiting them.
    /// Pin system colors on the SHA roots only — do not restyle their chrome.
    /// </summary>
    public static void IsolatePhase8ExactShaSurfaces(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (IsPhase8ExactShaSurface(root))
        {
            root.ForeColor = SystemColors.ControlText;
            root.BackColor = SystemColors.Control;
            return;
        }

        foreach (Control child in root.Controls)
        {
            IsolatePhase8ExactShaSurfaces(child);
        }
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
