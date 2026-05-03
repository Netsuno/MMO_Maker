#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace Frog.Editor.Ui;

/// <summary>Chrome sombre type RPG Maker MZ : rails latéraux, zone centrale carte, accents bleus/cyan.</summary>
internal static class EditorChrome
{
    public static readonly Font BodyFont = new("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font CaptionFont = new("Segoe UI", 8.75f, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font SectionFont = new("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Point);

    /// <summary>Fond global de la fenêtre (périphérie autour des splits).</summary>
    public static readonly Color WorkspaceBg = Color.FromArgb(28, 30, 36);

    /// <summary>Zone centrale autour du canevas carte (légèrement plus claire que le fond global).</summary>
    public static readonly Color WorkspaceCenter = Color.FromArgb(34, 36, 42);

    public static readonly Color SidebarBg = Color.FromArgb(40, 43, 50);
    public static readonly Color SidebarElevated = Color.FromArgb(50, 53, 62);
    public static readonly Color CanvasInset = Color.FromArgb(46, 49, 58);
    public static readonly Color RibbonBg = Color.FromArgb(32, 34, 40);
    public static readonly Color RibbonAccent = Color.FromArgb(100, 190, 255);
    public static readonly Color RibbonAccentDim = Color.FromArgb(70, 130, 200);
    public static readonly Color LabelPrimary = Color.FromArgb(235, 238, 245);
    public static readonly Color LabelMuted = Color.FromArgb(148, 155, 170);
    public static readonly Color PrimaryButtonBg = Color.FromArgb(92, 130, 255);
    public static readonly Color PrimaryButtonHover = Color.FromArgb(112, 150, 255);

    /// <summary>Accent « action principale » (type bouton Enregistrer / Playtest).</summary>
    public static readonly Color SaveActionGreen = Color.FromArgb(72, 196, 118);
    public static readonly Color SaveActionGreenHover = Color.FromArgb(92, 216, 138);

    public static readonly Color MapCanvasBg = Color.FromArgb(26, 28, 34);
    public static readonly Color PaletteStripBg = Color.FromArgb(48, 51, 60);

    /// <summary>Barres d’outils et menus contextuels assortis au thème sombre.</summary>
    public static void ApplyGlobalToolstripTheme()
        => ToolStripManager.Renderer = new EditorProfessionalRenderer();

    public static void ApplyFormChrome(Form form)
    {
        form.Font = BodyFont;
        form.BackColor = WorkspaceBg;
        form.ForeColor = LabelPrimary;
    }

    public static void StyleMainMenu(MenuStrip menu)
    {
        menu.BackColor = RibbonBg;
        menu.ForeColor = LabelPrimary;
        menu.Padding = new Padding(6, 2, 6, 2);
    }

    public static void StyleTabControlMaps(TabControl tabs)
    {
        tabs.Appearance = TabAppearance.FlatButtons;
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.ItemSize = new Size(36, 26);
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.Padding = new Point(4, 4);
        tabs.BackColor = SidebarBg;
        tabs.ForeColor = LabelPrimary;
        tabs.DrawItem += (_, e) =>
        {
            var selected = (e.State & DrawItemState.Selected) != 0;
            var bg = selected ? Color.FromArgb(65, 110, 165) : SidebarElevated;
            using var b = new SolidBrush(bg);
            e.Graphics.FillRectangle(b, e.Bounds);
            using var pen = new Pen(selected ? RibbonAccent : Color.FromArgb(70, 74, 86));
            e.Graphics.DrawRectangle(pen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            TextRenderer.DrawText(
                e.Graphics,
                tabs.TabPages[e.Index].Text,
                CaptionFont,
                e.Bounds,
                LabelPrimary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
    }

    public static void StyleMapsTree(TreeView tree)
    {
        tree.BackColor = SidebarElevated;
        tree.ForeColor = LabelPrimary;
        tree.BorderStyle = BorderStyle.FixedSingle;
        tree.LineColor = RibbonAccentDim;
        tree.HideSelection = false;
        tree.FullRowSelect = true;
        tree.ShowLines = true;
        tree.ShowPlusMinus = true;
        tree.ShowRootLines = true;
        tree.Font = BodyFont;
    }

    /// <summary>Bandeau de zone (titre d’outil / d’éditeur) façon onglet RPG Maker.</summary>
    public static Panel BuildZoneBanner(string title)
    {
        var p = new Panel
        {
            Height = 30,
            Dock = DockStyle.Top,
            BackColor = RibbonBg,
            Padding = new Padding(12, 0, 8, 0),
        };
        var accent = new Panel
        {
            Height = 3,
            Dock = DockStyle.Bottom,
            BackColor = RibbonAccent,
        };
        var lbl = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = LabelPrimary,
            Font = SectionFont,
            BackColor = Color.Transparent,
        };
        p.Controls.Add(lbl);
        p.Controls.Add(accent);
        accent.BringToFront();
        return p;
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
        strip.BackColor = RibbonBg;
        strip.Padding = new Padding(10, 4, 10, 4);
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
