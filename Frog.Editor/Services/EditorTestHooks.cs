using Frog.Application.Maps;

namespace Frog.Editor.Services;

/// <summary>Points d’injection pour tests UI (smoke Windows).</summary>
internal static class EditorTestHooks
{
    public static IMapRepository? OverrideMapRepository { get; set; }

    public static IEditorDialogService? OverrideDialogService { get; set; }

    public static bool SkipMariaDbOnStartup { get; set; }
}
