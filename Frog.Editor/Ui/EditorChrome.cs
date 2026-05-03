#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace Frog.Editor.Ui;

/// <summary>Inspiration visuelle type RPG Maker MZ : colonnes latérales sombres, carte au centre dans un léger cadre « document ».</summary>
internal static class EditorChrome
{
    public static readonly Font BodyFont = new("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font CaptionFont = new("Segoe UI", 8.75f, FontStyle.Bold, GraphicsUnit.Point);

    public static readonly Color SidebarBg = Color.FromArgb(44, 47, 55);
    public static readonly Color SidebarElevated = Color.FromArgb(56, 60, 70);
    public static readonly Color WorkspaceBg = Color.FromArgb(219, 222, 231);
    public static readonly Color CanvasInset = Color.FromArgb(208, 211, 220);
    public static readonly Color RibbonBg = Color.FromArgb(50, 52, 60);
    public static readonly Color RibbonAccent = Color.FromArgb(120, 168, 255);
    public static readonly Color LabelPrimary = Color.FromArgb(239, 242, 246);
    public static readonly Color LabelMuted = Color.FromArgb(160, 167, 180);
    public static readonly Color PrimaryButtonBg = Color.FromArgb(92, 130, 255);
    public static readonly Color PrimaryButtonHover = Color.FromArgb(112, 150, 255);

    public static readonly Color MapCanvasBg = Color.FromArgb(38, 40, 48);
    public static readonly Color PaletteStripBg = Color.FromArgb(52, 55, 64);

    /// <summary>Barres d’outils et menus contextuels assortis au thème sombre.</summary>
    public static void ApplyGlobalToolstripTheme()
        => ToolStripManager.Renderer = new EditorProfessionalRenderer();

    public static void ApplyFormChrome(Form form)
    {
        form.Font = BodyFont;
        form.BackColor = WorkspaceBg;
        form.ForeColor = Color.FromArgb(28, 30, 38);
    }

    public static void StyleDialogButton(Button b, bool primary)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.Font = CaptionFont;
        if (primary)
        {
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = PrimaryButtonBg;
            b.ForeColor = Color.White;
            b.Cursor = Cursors.Hand;
            b.MouseEnter += (_, _) => b.BackColor = PrimaryButtonHover;
            b.MouseLeave += (_, _) => b.BackColor = PrimaryButtonBg;
        }
        else
        {
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(88, 92, 103);
            b.BackColor = SidebarElevated;
            b.ForeColor = LabelPrimary;
            b.Cursor = Cursors.Default;
        }
    }

    public static void StylePrimaryButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = PrimaryButtonBg;
        b.ForeColor = Color.White;
        b.Cursor = Cursors.Hand;
        b.Font = CaptionFont;
        b.Margin = new Padding(0, 0, 0, 4);
        b.MouseEnter += (_, _) => b.BackColor = PrimaryButtonHover;
        b.MouseLeave += (_, _) => b.BackColor = PrimaryButtonBg;
    }

    public static void StyleSidebarRadio(RadioButton r)
    {
        r.FlatStyle = FlatStyle.Flat;
        r.BackColor = Color.Transparent;
        r.ForeColor = LabelPrimary;
        r.Margin = new Padding(0, 3, 12, 0);
        r.AutoSize = true;
        r.UseCompatibleTextRendering = false;
        r.Cursor = Cursors.Default;
        r.Padding = Padding.Empty;
    }

    public static Control BuildSectionCaption(string uppercaseTitle)
        => new Label
        {
            Text = uppercaseTitle.ToUpperInvariant(),
            Font = CaptionFont,
            ForeColor = LabelMuted,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 8),
            BackColor = Color.Transparent,
        };

    public static void StyleSidebarList(ListBox lb)
    {
        lb.BackColor = SidebarElevated;
        lb.ForeColor = LabelPrimary;
        lb.BorderStyle = BorderStyle.FixedSingle;
        lb.IntegralHeight = false;
        lb.Margin = Padding.Empty;
    }

    public static void StyleSidebarListView(ListView lv)
    {
        lv.BorderStyle = BorderStyle.FixedSingle;
        lv.FullRowSelect = true;
        lv.HideSelection = false;
        lv.BackColor = SidebarElevated;
        lv.ForeColor = LabelPrimary;
        lv.Margin = Padding.Empty;
    }

    public static void StylePropertyGrid(PropertyGrid grid)
    {
        grid.LineColor = Color.FromArgb(70, 73, 83);
        grid.CategorySplitterColor = SidebarBg;
        grid.BackColor = SidebarBg;
        grid.CategoryForeColor = LabelMuted;
        grid.ViewForeColor = LabelPrimary;
        grid.ViewBackColor = SidebarElevated;
        grid.HelpForeColor = LabelMuted;
        grid.HelpBackColor = SidebarBg;
        grid.CommandsForeColor = LabelPrimary;
        grid.CommandsActiveLinkColor = RibbonAccent;
    }

    public static ToolStrip StripToolbar(ToolStrip strip)
    {
        strip.Padding = new Padding(8, 2, 8, 2);
        strip.Text = strip.Text;
        return strip;
    }

    private sealed class EditorColorTable : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin => RibbonBg;
        public override Color ToolStripGradientMiddle => RibbonBg;
        public override Color ToolStripGradientEnd => RibbonBg;

        public override Color ToolStripBorder => Color.FromArgb(65, 68, 78);

        public override Color SeparatorDark => Color.FromArgb(70, 73, 83);
        public override Color SeparatorLight => Color.FromArgb(70, 73, 83);

        public override Color ImageMarginGradientBegin => RibbonBg;
        public override Color ImageMarginGradientMiddle => RibbonBg;
        public override Color ImageMarginGradientEnd => RibbonBg;

        public override Color ButtonSelectedHighlightBorder => RibbonAccent;

        public override Color MenuBorder => SidebarElevated;
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(80, 100, 180);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(80, 100, 180);
        public override Color MenuItemSelectedGradientBegin => SidebarElevated;
        public override Color MenuItemSelectedGradientEnd => SidebarElevated;
        public override Color ToolStripDropDownBackground => SidebarBg;
        public override Color MenuItemBorder => SidebarElevated;
    }

    private sealed class EditorProfessionalRenderer : ToolStripProfessionalRenderer
    {
        public EditorProfessionalRenderer() : base(new EditorColorTable())
        {
            RoundedEdges = false;
        }
    }
}
