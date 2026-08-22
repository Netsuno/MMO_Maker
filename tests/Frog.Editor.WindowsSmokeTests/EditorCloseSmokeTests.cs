using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Frog.Application.Maps;
using Frog.Editor;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

public sealed class EditorCloseSmokeTests
{
    [Fact]
    public void MainWindow_DirtyCloseCancel_KeepsWindowOpen()
    {
        StaTestRunner.Run(() => RunCloseScenario(EditorPromptChoice.Cancel, expectClosed: false, expectDirty: true));
    }

    [Fact]
    public void MainWindow_DirtyCloseDiscard_ClosesWindow()
    {
        StaTestRunner.Run(() => RunCloseScenario(EditorPromptChoice.Discard, expectClosed: true, expectDirty: true));
    }

    [Fact]
    public void MainWindow_DirtyCloseSaveSuccess_ClosesWindow()
    {
        StaTestRunner.Run(() => RunCloseScenario(EditorPromptChoice.Save, expectClosed: true, expectDirty: false, saveSucceeds: true));
    }

    [Fact]
    public void MainWindow_DirtyCloseSaveFailed_KeepsWindowOpenAndDirty()
    {
        StaTestRunner.Run(() => RunCloseScenario(EditorPromptChoice.Save, expectClosed: false, expectDirty: true, saveSucceeds: false));
    }

    private static void RunCloseScenario(
        EditorPromptChoice choice,
        bool expectClosed,
        bool expectDirty,
        bool saveSucceeds = true)
    {
        EditorSmokeTestAccess.ConfigureInMemoryRepository();
        EditorTestHooks.OverrideDialogService = new ScriptedDialogService(choice);

        if (!saveSucceeds)
        {
            EditorTestHooks.OverrideMapRepository = new FailingSaveMapRepository();
        }

        MainWindow? window = null;
        try
        {
            window = EditorSmokeTestAccess.CreateAndShowMainWindow();
            StaTestRunner.PumpUntil(
                () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                EditorSmokeTestAccess.DefaultTimeout);

            window.Dispatcher.Invoke(() =>
            {
                var session = window.EditorForm.GetWorkspaceSessionForTest()!;
                session.CurrentMap!.Name = "Dirty close test";
                session.MarkDirty();
            });

            window.Dispatcher.Invoke(() => window.Close());

            var closed = false;
            StaTestRunner.PumpUntil(
                () =>
                {
                    window.Dispatcher.Invoke(() => closed = !window.IsVisible);
                    return closed == expectClosed;
                },
                EditorSmokeTestAccess.DefaultTimeout);

            if (closed != expectClosed)
            {
                throw new InvalidOperationException($"Expected closed={expectClosed}, got {closed}.");
            }

            if (!closed)
            {
                var dirty = window.Dispatcher.Invoke(() => window.EditorForm.HasUnsavedChangesForTest());
                if (dirty != expectDirty)
                {
                    throw new InvalidOperationException($"Expected dirty={expectDirty}, got {dirty}.");
                }
            }
        }
        finally
        {
            if (window is not null)
            {
                EditorSmokeTestAccess.CloseMainWindow(window);
            }

            if (System.Windows.Application.Current is not null)
            {
                System.Windows.Application.Current.Shutdown();
            }

            EditorTestHooks.OverrideDialogService = null;
            EditorTestHooks.OverrideMapRepository = null;
        }
    }

    private sealed class ScriptedDialogService : IEditorDialogService
    {
        private readonly EditorPromptChoice _choice;

        public ScriptedDialogService(EditorPromptChoice choice) => _choice = choice;

        public EditorPromptChoice PromptSaveDiscardCancel(string message, string title) => _choice;

        public bool ConfirmYesNo(string message, string title) => true;

        public void ShowInfo(string message, string title)
        {
        }

        public void ShowWarning(string message, string title)
        {
        }

        public void ShowError(string message, string title)
        {
        }
    }

    private sealed class FailingSaveMapRepository : IMapRepository
    {
        private readonly InMemoryMapRepository _inner = new(MapRepositoryCapabilities.InMemoryTest);

        public MapRepositoryCapabilities Capabilities => _inner.Capabilities;

        public Task<SaveMapResult> SaveAsync(SaveMapRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<SaveMapResult>(new SaveMapResult.PersistenceFailed("injecté"));

        public Task<StoredMap?> LoadByIdAsync(Guid mapId, CancellationToken cancellationToken = default)
            => _inner.LoadByIdAsync(mapId, cancellationToken);

        public Task<StoredMap?> LoadPublishedByIdAsync(Guid mapId, CancellationToken cancellationToken = default)
            => _inner.LoadPublishedByIdAsync(mapId, cancellationToken);

        public Task<IReadOnlyList<MapCatalogEntry>> ListSummariesAsync(CancellationToken cancellationToken = default)
            => _inner.ListSummariesAsync(cancellationToken);

        public Task<IReadOnlyList<MapPublicationRecord>> ListPublicationHistoryAsync(Guid mapId, CancellationToken cancellationToken = default)
            => _inner.ListPublicationHistoryAsync(mapId, cancellationToken);
    }
}
