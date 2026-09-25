#nullable enable
using System.Drawing;
using System.Windows.Forms;
using Frog.Client.Config;

namespace Frog.Client.UI;

/// <summary>
/// Applique l’échelle joueur aux polices d’interface.
/// Ne touche pas les <see cref="PictureBox"/> (carte 32 px, miniature).
/// Ne change pas la police du formulaire : <c>AutoScaleMode.Font</c> resterait
/// alors recalculé et doublerait le DPI Windows.
/// </summary>
internal static class UiScaleApplicator
{
    private sealed class FontDesign
    {
        public float Em;
        public FontStyle Style;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, FontDesign> Designs = new();

    public static void ApplyDesignFont(Control control, float designEm, FontStyle style)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (!Designs.TryGetValue(control, out var design))
        {
            design = new FontDesign
            {
                Em = designEm,
                Style = style,
            };
            Designs.Add(control, design);
        }

        Assign(control, design, ClientUiScale.ActivePercent);
    }

    public static void ApplyFonts(Control root, int percent)
    {
        ArgumentNullException.ThrowIfNull(root);
        var clamped = ClientUiScale.ClampPercent(percent);
        var controls = new List<Control>();
        Collect(root, controls);
        foreach (var control in controls)
        {
            Assign(control, GetOrCapture(control), clamped);
        }
    }

    private static void Assign(Control control, FontDesign design, int percent)
    {
        var em = ClientUiScale.ScaleEm(design.Em, percent);
        var current = control.Font;
        if (current is not null
            && Math.Abs(current.SizeInPoints - em) <= 0.05f
            && current.Style == design.Style)
        {
            return;
        }

        control.Font = UiTheme.UiFont(em, design.Style);
    }

    private static void Collect(Control control, List<Control> controls)
    {
        if (control is PictureBox)
        {
            return;
        }

        if (control is not Form)
        {
            controls.Add(control);
        }

        foreach (Control child in control.Controls)
        {
            Collect(child, controls);
        }
    }

    private static FontDesign GetOrCapture(Control control)
    {
        if (Designs.TryGetValue(control, out var design))
        {
            return design;
        }

        var font = control.Font;
        design = new FontDesign
        {
            Em = font.SizeInPoints,
            Style = font.Style,
        };
        Designs.Add(control, design);
        return design;
    }
}
