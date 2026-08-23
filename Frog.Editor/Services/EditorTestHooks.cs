using System.Drawing;
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
}
