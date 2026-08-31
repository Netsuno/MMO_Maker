using System.Windows.Forms;
using Frog.Application.Maps;

/// <summary>Réponse utilisateur aux invites Enregistrer / Ignorer / Annuler.</summary>
public enum EditorPromptChoice
{
    Save = 0,
    Discard = 1,
    Cancel = 2,
}

/// <summary>Service de dialogues injectable (smoke tests, UI).</summary>
public interface IEditorDialogService
{
    EditorPromptChoice PromptSaveDiscardCancel(string message, string title);

    bool ConfirmYesNo(string message, string title);

    void ShowInfo(string message, string title);

    void ShowWarning(string message, string title);

    void ShowError(string message, string title);
}

/// <summary>Implémentation WinForms par défaut.</summary>
public sealed class WinFormsEditorDialogService : IEditorDialogService
{
    private readonly Func<IWin32Window?> _ownerProvider;

    public WinFormsEditorDialogService(Func<IWin32Window?> ownerProvider)
    {
        _ownerProvider = ownerProvider;
    }

    public EditorPromptChoice PromptSaveDiscardCancel(string message, string title)
    {
        var result = MessageBox.Show(
            _ownerProvider(),
            message,
            title,
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
        return result switch
        {
            DialogResult.Yes => EditorPromptChoice.Save,
            DialogResult.No => EditorPromptChoice.Discard,
            _ => EditorPromptChoice.Cancel,
        };
    }

    public bool ConfirmYesNo(string message, string title)
        => MessageBox.Show(_ownerProvider(), message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
           == DialogResult.Yes;

    public void ShowInfo(string message, string title)
        => MessageBox.Show(_ownerProvider(), message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public void ShowWarning(string message, string title)
        => MessageBox.Show(_ownerProvider(), message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public void ShowError(string message, string title)
        => MessageBox.Show(_ownerProvider(), message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
}
