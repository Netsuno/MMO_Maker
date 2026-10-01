using System.IO;
using System.Net.Sockets;
using System.Windows.Threading;
using Frog.Core.Constants;
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
    public void WpfEmbed_DefersRightRailOnceTheHostHandleExists()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

        // Constructeur : pas de HWND, le repli de l’inspecteur reste synchrone.
        Assert.False(ShellLayoutDeferral.DeferWhileHostHandleExists(embedAsWpfChild: true, splitHandleCreated: false));
        // Premier show : handle créé dans WindowsFormsHost.BuildWindowCore — poster, ne pas appliquer.
        Assert.True(ShellLayoutDeferral.DeferWhileHostHandleExists(embedAsWpfChild: true, splitHandleCreated: true));
        Assert.False(ShellLayoutDeferral.DeferWhileHostHandleExists(embedAsWpfChild: false, splitHandleCreated: true));
        Assert.False(ShellLayoutDeferral.DeferWhileHostHandleExists(embedAsWpfChild: false, splitHandleCreated: false));
    }

    [Fact]
    public void PaletteChrome_ConvertsDips_AndStopsChasingHeight()
    {
        // 150 % : 160 DIP = 240 px. L’ancien SizeChanged écrivait Ceiling(DesiredSize)
        // dans Control.Height, donc 160 px, puis la bande grandissait sans fin.
        Assert.Equal(240, PaletteChromeHeight.ToPixels(160, 1.5));
        Assert.Equal(160, PaletteChromeHeight.ToPixels(160, 1));
        Assert.NotEqual(PaletteChromeHeight.ToPixels(160, 1.5), (int)Math.Ceiling(160d));
        Assert.Equal(0, PaletteChromeHeight.ToPixels(double.PositiveInfinity, 1.5));
        Assert.Equal(0, PaletteChromeHeight.ToPixels(double.NaN, 1));
        Assert.Equal(160, PaletteChromeHeight.ToPixels(160, double.NaN));

        Assert.False(PaletteChromeHeight.ShouldApply(240, 240, appliesSoFar: 0));
        Assert.False(PaletteChromeHeight.ShouldApply(240, 242, appliesSoFar: 0));
        Assert.True(PaletteChromeHeight.ShouldApply(240, 248, appliesSoFar: 0));
        Assert.False(PaletteChromeHeight.ShouldApply(240, 248, PaletteChromeHeight.ApplyBudget));
        Assert.False(PaletteChromeHeight.ShouldApply(240, 20, appliesSoFar: 0));
        Assert.False(PaletteChromeHeight.ShouldApply(240, 900, appliesSoFar: 0));
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

/// <summary>
/// La coque WPF reste pompable après le premier affichage, et Postgres injoignable ouvre un brouillon local.
/// Le layout du rail ne doit pas tourner sur la pile HandleCreated, et la bande d’outils ne doit pas
/// être <c>AutoSize</c> (PerformLayout depuis SizeChanged, après le premier paint).
/// </summary>
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

                Assert.Equal(0, form.RightRailLayoutsInsideHostCallbackForTest);
                Assert.Equal(0, form.PaletteChromeInsideHostCallbackForTest);
                Assert.False(form.LeftToolsHostAutoSizeForTest);
                Assert.True(form.RightRailLayoutInvocationsForTest > 0);
                Assert.InRange(form.PaletteChromeHeightAppliesForTest, 0, PaletteChromeHeight.ApplyBudget);
                var before = form.RightRailLayoutInvocationsForTest;
                var pumped = false;
                window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => pumped = true));
                var started = Environment.TickCount64;
                StaTestRunner.PumpUntil(
                    () => pumped && Environment.TickCount64 - started >= 300,
                    TimeSpan.FromSeconds(4));
                Assert.True(pumped);
                Assert.Equal(0, form.RightRailLayoutsInsideHostCallbackForTest);
                Assert.Equal(0, form.PaletteChromeInsideHostCallbackForTest);
                Assert.False(form.LeftToolsHostAutoSizeForTest);
                Assert.InRange(form.PaletteChromeHeightAppliesForTest, 0, PaletteChromeHeight.ApplyBudget);
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
