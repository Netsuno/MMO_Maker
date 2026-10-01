namespace Frog.Editor.Ui;

/// <summary>
/// Premier affichage de la coque WPF : le handle du rail est créé dans
/// <c>WindowsFormsHost.BuildWindowCore</c>. Le formulaire hôte n’a pas de handle
/// (<c>TopLevel = false</c>), donc l’ancien <c>IsHandleCreated ? BeginInvoke : Apply</c>
/// appliquait <c>Panel2Collapsed</c> et <c>SplitterDistance</c> sur cette pile.
/// L’<c>ElementHost</c> mesure alors le dispatcher WPF, qui attend la fin de
/// <c>BuildWindowCore</c> : pompe bloquée, CPU plat, <c>IsHungAppWindow</c>.
/// </summary>
internal static class ShellLayoutDeferral
{
    /// <summary>
    /// Vrai une fois le split visible dans la coque WPF. Le constructeur (handle encore absent)
    /// reste synchrone : aucun HWND, donc pas de <c>SendMessage</c> vers le dispatcher.
    /// </summary>
    public static bool DeferWhileHostHandleExists(bool embedAsWpfChild, bool splitHandleCreated)
        => embedAsWpfChild && splitHandleCreated;
}
