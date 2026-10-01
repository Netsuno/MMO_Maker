using System.IO;
using System.Net.Sockets;
using System.Windows.Threading;
using Frog.Editor;
using Frog.Editor.Services;
using Frog.Editor.Ui;
using Frog.Persistence.PostgreSql;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Distances du rail : point fixe, sans WinForms.</summary>
public sealed class RightRailLayoutMathTests
{
    [Fact]
    public void TilesetDistance_IsStable_CollapsedAndExpanded()
    {
        Assert.True(RightRailLayoutMath.TryTilesetDistance(900, 6, inspectorExpanded: false, layersSplitterWidth: 4, out var collapsed));
        Assert.Equal(900 - 6 - RightRailLayoutMath.LayersDockHeight, collapsed);
        Assert.True(RightRailLayoutMath.TryTilesetDistance(900, 6, inspectorExpanded: false, layersSplitterWidth: 4, out var again));
        Assert.Equal(collapsed, again);

        Assert.True(RightRailLayoutMath.TryTilesetDistance(900, 6, inspectorExpanded: true, layersSplitterWidth: 4, out var expanded));
        Assert.Equal(
            900 - 6 - (RightRailLayoutMath.LayersDockHeight + RightRailLayoutMath.InspectorExpandedHeight + 4),
            expanded);
        Assert.True(expanded < collapsed);

        Assert.False(RightRailLayoutMath.TryTilesetDistance(40, 6, inspectorExpanded: false, layersSplitterWidth: 4, out _));
    }

    [Fact]
    public void LayersDistance_ClampsInspectorBand()
    {
        Assert.True(RightRailLayoutMath.TryLayersDistance(332, 4, panel1Min: 96, panel2Min: 120, out var distance));
        Assert.Equal(332 - 4 - RightRailLayoutMath.InspectorExpandedHeight, distance);
        Assert.True(RightRailLayoutMath.TryLayersDistance(332, 4, panel1Min: 96, panel2Min: 120, out var again));
        Assert.Equal(distance, again);
        Assert.False(RightRailLayoutMath.TryLayersDistance(20, 4, panel1Min: 96, panel2Min: 120, out _));
    }

    [Fact]
    public void UnavailableDatabase_IsConnectionFailure_NotForcedMigrate()
    {
        Assert.True(EditorDatabaseAvailability.IsUnavailable(new SocketException((int)SocketError.ConnectionRefused)));
        Assert.True(EditorDatabaseAvailability.IsUnavailable(new TimeoutException("connect")));
        Assert.True(EditorDatabaseAvailability.IsUnavailable(new IOException("reset", new SocketException((int)SocketError.ConnectionReset))));
        Assert.False(EditorDatabaseAvailability.IsUnavailable(new OperationCanceledException()));
        Assert.False(EditorDatabaseAvailability.IsUnavailable(new InvalidOperationException("forced migrate failure")));

        var budget = FrogDbContextOptions.WithEditorConnectTimeout(
            "Host=127.0.0.1;Port=5432;Database=frog;Username=u;Password=p;Timeout=15");
        Assert.Contains("Timeout=5", budget, StringComparison.Ordinal);
        Assert.DoesNotContain("Timeout=15", budget, StringComparison.Ordinal);

        var kept = FrogDbContextOptions.WithEditorConnectTimeout(
            "Host=127.0.0.1;Port=5432;Database=frog;Username=u;Password=p;Timeout=3");
        Assert.Contains("Timeout=3", kept, StringComparison.Ordinal);
    }
}

/// <summary>La coque WPF reste pompable, et Postgres injoignable ouvre un brouillon local.</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EditorStartupHangSmokeTests
{
    [Fact]
    public void WpfShell_StaysResponsive_AfterLayout()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ConfigureInMemoryRepository();
            MainWindow? window = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                var form = window.EditorForm;
                StaTestRunner.PumpUntil(
                    () => form.WorkspaceInitializationTask.IsCompleted && form.InspectorCollapsedForTest,
                    TimeSpan.FromSeconds(8));
                if (form.WorkspaceInitializationTask.IsFaulted)
                {
                    throw form.WorkspaceInitializationTask.Exception!.GetBaseException();
                }

                var before = form.RightRailLayoutInvocationsForTest;
                var pumped = false;
                window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => pumped = true));
                var started = Environment.TickCount64;
                StaTestRunner.PumpUntil(
                    () => pumped && Environment.TickCount64 - started >= 300,
                    TimeSpan.FromSeconds(4));
                Assert.True(pumped);
                Assert.InRange(form.RightRailLayoutInvocationsForTest - before, 0, 12);
            }
            finally
            {
                if (window is not null)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void Workspace_PostgresUnreachable_OpensInMemoryDemo()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            Environment.SetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory, null);
            EditorTestHooks.SkipMariaDbOnStartup = true;
            EditorTestHooks.OverridePostgreSqlConnectionString =
                "Host=127.0.0.1;Port=1;Database=frog;Username=u;Password=p";
            EditorTestHooks.OverridePostgreSqlMigrateForTest = _ =>
                throw new SocketException((int)SocketError.ConnectionRefused);

            MainWindow? window = null;
            try
            {
                window = EditorSmokeTestAccess.CreateAndShowMainWindow();
                var form = window.EditorForm;
                StaTestRunner.PumpUntil(
                    () => form.WorkspaceInitializationTask.IsCompleted,
                    TimeSpan.FromSeconds(8));
                if (form.WorkspaceInitializationTask.IsFaulted)
                {
                    throw form.WorkspaceInitializationTask.Exception!.GetBaseException();
                }

                Assert.Contains("PostgreSQL injoignable", form.StatusTextForTest, StringComparison.Ordinal);
                Assert.Contains("mémoire (démo", form.StatusTextForTest, StringComparison.Ordinal);
            }
            finally
            {
                if (window is not null)
                {
                    EditorSmokeTestAccess.ForceCloseMainWindow(window);
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }
}
