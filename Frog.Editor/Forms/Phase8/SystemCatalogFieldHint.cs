using Frog.Editor.Services;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Affiche le libellé Système à côté d’un identifiant d’interrupteur ou de variable.</summary>
internal static class SystemCatalogFieldHint
{
    public const string Tag = "system-catalog-hint";

    public static void Attach(Control row, string key, Control valueControl)
    {
        if (valueControl is not TextBox text || key is not ("switchId" or "variableId"))
        {
            return;
        }

        var hint = new Label
        {
            AutoSize = true,
            Tag = Tag,
            ForeColor = Color.DimGray,
            Margin = new Padding(8, 6, 0, 0),
        };

        void Refresh()
        {
            var id = text.Text.Trim();
            hint.Text = key == "switchId"
                ? EditorSystemNameCatalog.FormatSwitch(id)
                : EditorSystemNameCatalog.FormatVariable(id);
        }

        text.TextChanged += (_, _) => Refresh();
        Refresh();
        row.Controls.Add(hint);
    }

    public static string Read(Control? valueControl)
    {
        if (valueControl?.Parent is null)
        {
            return string.Empty;
        }

        foreach (Control child in valueControl.Parent.Controls)
        {
            if (child is Label label && Equals(label.Tag, Tag))
            {
                return label.Text;
            }
        }

        return string.Empty;
    }
}
