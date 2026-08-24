using System.Drawing;
using System.Windows.Forms;
using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Application.Playtest;

namespace Frog.Editor.Services;

/// <summary>Points d’injection pour tests UI (smoke Windows).</summary>
internal static class EditorTestHooks
{
    public static IMapRepository? OverrideMapRepository { get; set; }

    public static ITilesetRepository? OverrideTilesetRepository { get; set; }

    public static INpcRepository? OverrideNpcRepository { get; set; }

    public static IItemRepository? OverrideItemRepository { get; set; }

    public static ISpellRepository? OverrideSpellRepository { get; set; }

    public static IClassRepository? OverrideClassRepository { get; set; }

    public static IShopRepository? OverrideShopRepository { get; set; }

    public static IResourceRepository? OverrideResourceRepository { get; set; }

    public static IResourceSpawnRepository? OverrideResourceSpawnRepository { get; set; }

    public static IEditorDialogService? OverrideDialogService { get; set; }

    public static IPlaytestProcessLauncher? OverridePlaytestProcessLauncher { get; set; }

    /// <summary>Chemin serveur injecté (smoke) — évite la résolution disque.</summary>
    public static string? OverrideServerExePath { get; set; }

    /// <summary>Chemin client injecté (smoke).</summary>
    public static string? OverrideClientExePath { get; set; }

    public static bool SkipMariaDbOnStartup { get; set; }

    /// <summary>Smoke / unit : autorise playtest sur dépôt mémoire test.</summary>
    public static bool AllowNonDurablePlaytest { get; set; }

    /// <summary>Smoke : force la tuile de spawn sans dialogue modal.</summary>
    public static Point? OverrideSpawnTile { get; set; }

    /// <summary>Smoke : ouvre Données de jeu en non-modal pour automatisation UI.</summary>
    public static bool GameDataNonModalForTest { get; set; }

    /// <summary>Smoke : appelé quand la fenêtre Données de jeu est affichée.</summary>
    public static Action<System.Windows.Forms.Form>? OnGameDataFormShown { get; set; }

    /// <summary>Smoke : racine assets injectée pour les aperçus.</summary>
    public static string? OverrideProjectAssetRoot { get; set; }

    /// <summary>Smoke : réponse injectée pour MessageBox Données de jeu.</summary>
    public static DialogResult? OverrideMessageBoxResult { get; set; }
}
