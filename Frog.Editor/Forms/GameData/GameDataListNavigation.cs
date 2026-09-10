namespace Frog.Editor.Forms.GameData;

/// <summary>Helpers partagés pour la navigation catalogue avec modifications non enregistrées.</summary>
internal static class GameDataListNavigation
{
    public static bool ConfirmDiscardUnsavedChanges(IWin32Window owner, string panelTitle, bool isDirty)
    {
        if (!isDirty)
        {
            return true;
        }

        return GameDataUiMessageBox.Show(
                   owner,
                   "Modifications non enregistrées. Continuer ?",
                   panelTitle,
                   MessageBoxButtons.YesNo,
                   MessageBoxIcon.Warning)
               == DialogResult.Yes;
    }

    public static void RevertListSelection(ListBox list, ref bool suppressList, Guid? currentRecordId, Func<object, Guid> getId)
    {
        if (currentRecordId is not Guid id || id == Guid.Empty)
        {
            return;
        }

        suppressList = true;
        try
        {
            for (var i = 0; i < list.Items.Count; i++)
            {
                if (getId(list.Items[i]!) == id)
                {
                    list.SelectedIndex = i;
                    return;
                }
            }
        }
        finally
        {
            suppressList = false;
        }
    }
}
