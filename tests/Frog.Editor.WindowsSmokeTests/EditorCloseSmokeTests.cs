using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Frog.Application.Maps;
using Frog.Editor;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
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
        StaTestRunner.Run(() => RunCloseScenario(EditorPromptChoice.Discard, expectClosed: true, expectDirty: false));
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
            EditorTestHooks.OverrideMapRepository = new SeedThenFailMapRepository();
        }

        MainWindow? window = null;
        var closed = false;
        try
        {
            window = EditorSmokeTestAccess.CreateAndShowMainWindow();
            window.Closed += (_, _) => closed = true;

            StaTestRunner.PumpUntil(
                () => window.EditorForm.WorkspaceInitializationTask.IsCompleted,
                EditorSmokeTestAccess.DefaultTimeout);

            if (window.EditorForm.WorkspaceInitializationTask.IsFaulted)
            {
                throw window.EditorForm.WorkspaceInitializationTask.Exception?.GetBaseException()
                      ?? new InvalidOperationException("Workspace initialization failed.");
            }

            var session = window.EditorForm.GetWorkspaceSessionForTest()!;
            session.CurrentMap!.Name = "Dirty close test";
            session.MarkDirty();

            window.Close();

            // Leave Closing cancel + ApplicationIdle prompt finish.
            window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, static () => { });
            window.Dispatcher.Invoke(DispatcherPriority.Background, static () => { });

            if (expectClosed)
            {
                StaTestRunner.PumpUntil(() => closed, EditorSmokeTestAccess.DefaultTimeout);
            }
            else
            {
                StaTestRunner.PumpUntil(
                    () => window.IsVisible && !window.EditorForm.IsSaveInProgressForTest(),
                    EditorSmokeTestAccess.DefaultTimeout);
            }

            if (closed != expectClosed)
            {
                throw new InvalidOperationException($"Expected closed={expectClosed}, got {closed}.");
            }

            if (!closed)
            {
                var dirty = window.EditorForm.HasUnsavedChangesForTest();
                if (dirty != expectDirty)
                {
                    throw new InvalidOperationException($"Expected dirty={expectDirty}, got {dirty}.");
                }
            }
        }
        finally
        {
            if (window is not null && !closed)
            {
                EditorSmokeTestAccess.ForceCloseMainWindow(window);
                StaTestRunner.PumpUntil(() => closed || !window.IsVisible, TimeSpan.FromSeconds(5));
            }

            EditorSmokeTestAccess.ResetHooks();
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

    /// <summary>Autorise le seed initial, échoue les sauvegardes suivantes.</summary>
    private sealed class SeedThenFailMapRepository : IMapRepository
    {
        private readonly InMemoryMapRepository _inner = new(MapRepositoryCapabilities.InMemoryTest);
        private int _saveCount;

        public MapRepositoryCapabilities Capabilities => _inner.Capabilities;

        public Task<SaveMapResult> SaveAsync(SaveMapRequest request, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _saveCount) == 1)
            {
                return _inner.SaveAsync(request, cancellationToken);
            }

            return Task.FromResult<SaveMapResult>(new SaveMapResult.PersistenceFailed("injecté"));
        }

        public Task<StoredMap?> LoadByIdAsync(Guid mapId, CancellationToken cancellationToken = default)
            => _inner.LoadByIdAsync(mapId, cancellationToken);

        public Task<StoredMap?> LoadPublishedByIdAsync(Guid mapId, CancellationToken cancellationToken = default)
            => _inner.LoadPublishedByIdAsync(mapId, cancellationToken);

        public Task<StoredMap?> LoadPublishedByIdAndRevisionAsync(
            Guid mapId,
            long publishedRevision,
            CancellationToken cancellationToken = default)
            => _inner.LoadPublishedByIdAndRevisionAsync(mapId, publishedRevision, cancellationToken);

        public Task<IReadOnlyList<MapCatalogEntry>> ListSummariesAsync(CancellationToken cancellationToken = default)
            => _inner.ListSummariesAsync(cancellationToken);

        public Task<IReadOnlyList<MapPublicationRecord>> ListPublicationHistoryAsync(
            Guid mapId,
            CancellationToken cancellationToken = default)
            => _inner.ListPublicationHistoryAsync(mapId, cancellationToken);
    }
}
