using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor;

/// <summary>API interne pour le smoke test Windows (assembly test via InternalsVisibleTo).</summary>
internal static class EditorSmokeTestAccess
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(45);

    public static void ConfigureInMemoryRepository()
    {
        EditorTestHooks.OverrideMapRepository = new InMemoryMapRepository();
        EditorTestHooks.SkipMariaDbOnStartup = true;
        Environment.SetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory, "1");
    }

    public static MainWindow CreateAndShowMainWindow()
    {
        var window = new MainWindow();
        window.Show();
        window.UpdateLayout();
        return window;
    }

    public static void AssertShellReady(MainWindow window)
    {
        if (window.EditorForm.GetCanvasMapForTest() is not Map map)
        {
            throw new InvalidOperationException("Canvas map is null after workspace init.");
        }

        if (!string.Equals(map.Name, DemoMapFactory.DefaultName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected demo map name '{DemoMapFactory.DefaultName}', got '{map.Name}'.");
        }

        if (map.Width != 20 || map.Height != 15)
        {
            throw new InvalidOperationException($"Unexpected demo dimensions: {map.Width}x{map.Height}.");
        }

        if (map.Layers.Count < 3)
        {
            throw new InvalidOperationException($"Expected at least 3 demo layers, got {map.Layers.Count}.");
        }

        var layerTypes = map.Layers.Select(l => l.LayerType).ToArray();
        if (!layerTypes.Contains(LayerType.Ground)
            || !layerTypes.Contains(LayerType.Fringe)
            || !layerTypes.Contains(LayerType.Attributes))
        {
            throw new InvalidOperationException("Demo map missing expected layer types.");
        }

        var session = window.EditorForm.GetWorkspaceSessionForTest();
        if (session?.CurrentMapId != DemoMapFactory.DefaultMapId)
        {
            throw new InvalidOperationException("Current map id does not match demo catalog entry.");
        }

        if (session.Catalog.All(e => e.MapId != DemoMapFactory.DefaultMapId))
        {
            throw new InvalidOperationException("Demo map missing from catalog.");
        }

        if (!window.EditorForm.AreShellHostsReadyForTest())
        {
            throw new InvalidOperationException("Left/center/right shell hosts are not ready.");
        }
    }

    public static void CloseMainWindow(MainWindow window)
    {
        window.Dispatcher.Invoke(() => window.Close());
    }
}
