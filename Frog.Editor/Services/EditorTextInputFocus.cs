using System.Windows.Forms;

namespace Frog.Editor.Services;

/// <summary>
/// Détecte une saisie texte dans le shell hybride (TextBox, PropertyGrid et éditeurs imbriqués).
/// </summary>
public static class EditorTextInputFocus
{
    public static bool ShouldIgnoreToolHotkeys(Control? activeControl)
    {
        for (var current = activeControl; current is not null; current = current.Parent)
        {
            if (IsTextLike(current))
            {
                return true;
            }
        }

        return activeControl is not null && HasFocusedTextLikeDescendant(activeControl);
    }

    public static bool IsTextLike(Control? control) =>
        control is TextBoxBase or PropertyGrid or UpDownBase
        || control is ComboBox { DropDownStyle: not ComboBoxStyle.DropDownList };

    private static bool HasFocusedTextLikeDescendant(Control root)
    {
        if (!root.ContainsFocus)
        {
            return false;
        }

        if (root.Focused && IsTextLike(root))
        {
            return true;
        }

        foreach (Control child in root.Controls)
        {
            if (HasFocusedTextLikeDescendant(child))
            {
                return true;
            }
        }

        return false;
    }
}
