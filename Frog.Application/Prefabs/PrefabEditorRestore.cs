namespace Frog.Application.Prefabs;

/// <summary>
/// Source des placements à restaurer dans l’éditeur.
/// Les édits non enregistrés (workstate + dirty) priment sur le paquet persisté.
/// </summary>
public static class PrefabEditorRestore
{
    public enum Source
    {
        UnsavedWorkstate,
        PersistedPackage,
        DiskWorkstate,
        Empty,
    }

    public static Source Choose(bool workspaceDirty, bool workstatePresent, bool persistedPackagePresent)
    {
        if (workspaceDirty && workstatePresent)
        {
            return Source.UnsavedWorkstate;
        }

        if (persistedPackagePresent)
        {
            return Source.PersistedPackage;
        }

        return workstatePresent ? Source.DiskWorkstate : Source.Empty;
    }
}
