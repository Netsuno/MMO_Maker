namespace Frog.Editor.Forms.GameData;

/// <summary>Point unique pour les boîtes de dialogue UI Données de jeu (injectable en smoke test).</summary>
internal static class GameDataUiMessageBox
{
    public static DialogResult Show(IWin32Window owner, string text)
        => Show(owner, text, string.Empty);

    public static DialogResult Show(IWin32Window owner, string text, string caption)
        => Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);

    public static DialogResult Show(
        IWin32Window owner,
        string text,
        string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.None)
    {
        if (Services.EditorTestHooks.OverrideMessageBoxResult is { } injected)
        {
            return injected;
        }

        return MessageBox.Show(owner, text, caption, buttons, icon);
    }
}
