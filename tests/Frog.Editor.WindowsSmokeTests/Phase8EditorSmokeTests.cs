using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor;
using Frog.Editor.Forms.Phase8;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>
/// Phase 8 Windows editor smoke: Contenu Phase 8 structured browse/edit against in-memory service.
/// Screenshots → artifacts/phase-08-editor/.
/// </summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class Phase8EditorSmokeTests
{
    private static readonly object Sync = new();

    private static void RunLocked(Action body)
    {
        StaTestRunner.Run(() =>
        {
            lock (Sync)
            {
                StaTestRunner.ClearCapturedExceptionsForTest();
                body();
            }
        });
    }

    [Fact]
    public void Phase8Editor_DialogueDraftPublishDirtyDiscard_AndCloseDuringPendingOp()
    {
        RunLocked(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;
            EditorTestHooks.OverridePhase8ContentService = new InMemoryPhase8ContentEditorService();

            Phase8ContentBrowseDialog? dialog = null;
            try
            {
                dialog = new Phase8ContentBrowseDialog(EditorTestHooks.OverridePhase8ContentService);
                dialog.Show();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle, "init idle");
                PumpUntil(() => dialog.ActiveEditorForTest is Phase8DialogueEditorPanel, "dialogue editor ready");

                SaveScreenshot(dialog, "01-phase8-content-browse.png");

                dialog.BtnNewForTest.PerformClick();
                PumpUntil(
                    () => dialog.LifecycleForTest.IsIdle
                          && dialog.IsDirtyForTest
                          && dialog.ActiveEditorForTest is Phase8DialogueEditorPanel,
                    "new draft");
                dialog.NameForTest.Text = "Smoke Dialogue";
                Assert.IsType<Phase8DialogueEditorPanel>(dialog.ActiveEditorForTest);
                var dialogue = (Phase8DialogueEditorPanel)dialog.ActiveEditorForTest!;
                Assert.True(dialogue.TryBuildPayload(out _, out var buildError), buildError);

                SaveScreenshot(dialog, "02-dialogue-structured-edit.png");

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;
                dialog.BtnSaveForTest.PerformClick();
                PumpUntil(
                    () => dialog.LifecycleForTest.IsIdle && !dialog.IsDirtyForTest && dialog.CurrentRevisionForTest > 0,
                    "save draft");
                Assert.Equal(ContentPublishStatus.Draft, dialog.CurrentStatusForTest);
                SaveScreenshot(dialog, "03-draft-saved.png");

                dialog.BtnPublishForTest.PerformClick();
                PumpUntil(
                    () => dialog.LifecycleForTest.IsIdle
                          && dialog.CurrentStatusForTest == ContentPublishStatus.Published,
                    "publish");
                SaveScreenshot(dialog, "04-published.png");

                dialog.NameForTest.Text = "Dirty discard name";
                Assert.True(dialog.IsDirtyForTest);
                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                dialog.BtnNewForTest.PerformClick();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle && dialog.IsDirtyForTest, "dirty discard → new");
                SaveScreenshot(dialog, "05-dirty-discard.png");
            }
            finally
            {
                EditorTestHooks.PanelOperationBarrierForTest = null;
                if (dialog is { IsDisposed: false })
                {
                    EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                    try
                    {
                        dialog.Close();
                        PumpUntil(() => dialog.IsDisposed, "teardown close");
                    }
                    catch
                    {
                        // teardown best-effort
                    }
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Theory]
    [InlineData(Phase8ContentKind.Dialogue)]
    [InlineData(Phase8ContentKind.Quest)]
    [InlineData(Phase8ContentKind.CommonEvent)]
    [InlineData(Phase8ContentKind.Profession)]
    [InlineData(Phase8ContentKind.Recipe)]
    [InlineData(Phase8ContentKind.Region)]
    [InlineData(Phase8ContentKind.WeatherProfile)]
    public void Phase8Editor_AllContentKinds_MinimalNewDraft(Phase8ContentKind kind)
    {
        RunLocked(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;
            EditorTestHooks.OverridePhase8ContentService = new InMemoryPhase8ContentEditorService();

            Phase8ContentBrowseDialog? dialog = null;
            try
            {
                dialog = new Phase8ContentBrowseDialog(EditorTestHooks.OverridePhase8ContentService);
                dialog.Show();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle, "init idle");

                SelectKind(dialog, kind);
                PumpUntil(() => dialog.LifecycleForTest.IsIdle, "kind switched");

                dialog.BtnNewForTest.PerformClick();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle && dialog.IsDirtyForTest, "new draft");
                dialog.NameForTest.Text = $"Smoke {kind}";
                Assert.NotNull(dialog.ActiveEditorForTest);
                Assert.Equal(kind, dialog.ActiveEditorForTest!.Kind);
                Assert.True(dialog.ActiveEditorForTest.TryBuildPayload(out _, out var error), error);

                dialog.BtnSaveForTest.PerformClick();
                PumpUntil(
                    () => dialog.LifecycleForTest.IsIdle && !dialog.IsDirtyForTest && dialog.CurrentRevisionForTest > 0,
                    "save draft");
            }
            finally
            {
                if (dialog is { IsDisposed: false })
                {
                    EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                    dialog.Close();
                    PumpUntil(() => dialog.IsDisposed, "dispose");
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Theory]
    [InlineData("reload")]
    [InlineData("save")]
    [InlineData("publish")]
    [InlineData("delete")]
    public void Phase8Editor_RealClose_WhileOperationPending_DrainsThenDisposes(string operationName)
    {
        RunLocked(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var observed = new List<Exception>();
            EditorTestHooks.OnPanelLifecycleExceptionForTest = ex =>
            {
                lock (observed)
                {
                    observed.Add(ex);
                }
            };

            EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;
            EditorTestHooks.OverridePhase8ContentService = new InMemoryPhase8ContentEditorService();

            Phase8ContentBrowseDialog? dialog = null;
            try
            {
                dialog = new Phase8ContentBrowseDialog(EditorTestHooks.OverridePhase8ContentService);
                dialog.Show();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle, "init idle");

                if (operationName is "save" or "publish" or "delete")
                {
                    dialog.BtnNewForTest.PerformClick();
                    PumpUntil(() => dialog.LifecycleForTest.IsIdle && dialog.IsDirtyForTest, "new draft");
                    dialog.NameForTest.Text = $"CloseDuring-{operationName}";
                    if (operationName is "publish" or "delete")
                    {
                        dialog.BtnSaveForTest.PerformClick();
                        PumpUntil(
                            () => dialog.LifecycleForTest.IsIdle && !dialog.IsDirtyForTest,
                            "prerequisite save");
                    }
                }

                PumpUntil(() => dialog.LifecycleForTest.IsIdle, "idle before barrier");

                var barrierEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var sawCancellation = false;

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                EditorTestHooks.PanelOperationBarrierForTest = async (name, ct) =>
                {
                    if (!string.Equals(name, operationName, StringComparison.Ordinal))
                    {
                        return;
                    }

                    barrierEntered.TrySetResult();
                    try
                    {
                        using var reg = ct.Register(() => Volatile.Write(ref sawCancellation, true));
                        await releaseBarrier.Task.WaitAsync(ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        Volatile.Write(ref sawCancellation, true);
                        throw;
                    }
                };

                switch (operationName)
                {
                    case "reload":
                        dialog.BtnNewForTest.PerformClick();
                        PumpUntil(() => dialog.LifecycleForTest.IsIdle && dialog.IsDirtyForTest, "draft for reload");
                        dialog.BtnSaveForTest.PerformClick();
                        PumpUntil(() => dialog.LifecycleForTest.IsIdle && !dialog.IsDirtyForTest, "saved for reload");
                        dialog.BtnReloadForTest.PerformClick();
                        break;
                    case "save":
                        dialog.BtnSaveForTest.PerformClick();
                        break;
                    case "publish":
                        dialog.BtnPublishForTest.PerformClick();
                        break;
                    case "delete":
                        dialog.BtnDeleteForTest.PerformClick();
                        break;
                }

                StaTestRunner.PumpUntil(() => barrierEntered.Task.IsCompleted, TimeSpan.FromSeconds(10));
                Assert.True(dialog.LifecycleForTest.PendingCountForTest > 0);
                Assert.False(dialog.LifecycleForTest.IsIdle);
                Assert.False(dialog.IsDisposed);

                if (operationName == "save")
                {
                    SaveScreenshot(dialog, "06-close-during-pending.png");
                }

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                dialog.Close();

                StaTestRunner.PumpUntil(
                    () => dialog.IsDisposed || Volatile.Read(ref sawCancellation),
                    TimeSpan.FromSeconds(5));
                Assert.False(dialog.IsDisposed);
                Assert.True(Volatile.Read(ref sawCancellation));

                var disposeTimeout = TimeSpan.FromSeconds(60);
                try
                {
                    StaTestRunner.PumpUntil(() => dialog.IsDisposed, disposeTimeout);
                }
                catch (TimeoutException)
                {
                    throw new TimeoutException(
                        $"Phase 8 dialog did not dispose within {disposeTimeout.TotalSeconds:0}s while '{operationName}' was pending. " +
                        $"PendingCount={dialog.LifecycleForTest.PendingCountForTest}, IsIdle={dialog.LifecycleForTest.IsIdle}, sawCancellation={Volatile.Read(ref sawCancellation)}.");
                }

                Assert.True(Volatile.Read(ref sawCancellation));
                Assert.Null(dialog.CloseCleanupExceptionForTest);
                Assert.False(dialog.CloseCleanupFailedForTest);
                Assert.Empty(observed);
                Assert.True(dialog.IsDisposed);

                releaseBarrier.TrySetResult();
            }
            finally
            {
                EditorTestHooks.PanelOperationBarrierForTest = null;
                EditorTestHooks.OnPanelLifecycleExceptionForTest = null;
                if (dialog is { IsDisposed: false })
                {
                    EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                    try
                    {
                        dialog.Close();
                    }
                    catch
                    {
                        // teardown best-effort
                    }
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void Phase8Editor_RealClose_WhileInitializationPending_CancelsAndDisposes()
    {
        RunLocked(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;
            EditorTestHooks.OverridePhase8ContentService = new InMemoryPhase8ContentEditorService();

            var initEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseInit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var sawCancel = false;

            EditorTestHooks.PanelOperationBarrierForTest = async (name, ct) =>
            {
                if (!string.Equals(name, "init", StringComparison.Ordinal))
                {
                    return;
                }

                initEntered.TrySetResult();
                try
                {
                    using var reg = ct.Register(() => Volatile.Write(ref sawCancel, true));
                    await releaseInit.Task.WaitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Volatile.Write(ref sawCancel, true);
                    throw;
                }
            };

            Phase8ContentBrowseDialog? dialog = null;
            try
            {
                dialog = new Phase8ContentBrowseDialog(EditorTestHooks.OverridePhase8ContentService);
                dialog.Show();

                StaTestRunner.PumpUntil(() => initEntered.Task.IsCompleted, EditorSmokeTestAccess.DefaultTimeout);
                Assert.False(dialog.LifecycleForTest.IsIdle);

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                dialog.Close();
                StaTestRunner.PumpUntil(() => dialog.IsDisposed, EditorSmokeTestAccess.DefaultTimeout);

                Assert.True(Volatile.Read(ref sawCancel));
                Assert.False(dialog.CloseCleanupFailedForTest);
                Assert.True(dialog.IsDisposed);
            }
            finally
            {
                EditorTestHooks.PanelOperationBarrierForTest = null;
                releaseInit.TrySetResult();
                if (dialog is { IsDisposed: false })
                {
                    EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                    try
                    {
                        dialog.Close();
                    }
                    catch
                    {
                        // teardown best-effort
                    }
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Fact]
    public void Phase8Editor_NonCooperativeOperation_TimeoutKeepsDialogAlive_ThenRetryCloses()
    {
        RunLocked(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTestHooks.GameDataCloseCleanupTimeoutForTest = TimeSpan.FromMilliseconds(400);
            EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;
            EditorTestHooks.OverridePhase8ContentService = new InMemoryPhase8ContentEditorService();

            Phase8ContentBrowseDialog? dialog = null;
            try
            {
                dialog = new Phase8ContentBrowseDialog(EditorTestHooks.OverridePhase8ContentService);
                dialog.Show();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle, "init idle");

                dialog.BtnNewForTest.PerformClick();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle && dialog.IsDirtyForTest, "new draft");
                dialog.NameForTest.Text = "NonCoop";

                var barrierEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                EditorTestHooks.PanelOperationBarrierForTest = async (name, _) =>
                {
                    if (!string.Equals(name, "save", StringComparison.Ordinal))
                    {
                        return;
                    }

                    barrierEntered.TrySetResult();
                    await releaseBarrier.Task.ConfigureAwait(true);
                };

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;
                dialog.BtnSaveForTest.PerformClick();
                StaTestRunner.PumpUntil(() => barrierEntered.Task.IsCompleted, EditorSmokeTestAccess.DefaultTimeout);
                Assert.False(dialog.LifecycleForTest.IsIdle);

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                dialog.Close();
                StaTestRunner.PumpUntil(() => dialog.CloseCleanupFailedForTest, TimeSpan.FromSeconds(10));

                Assert.False(dialog.IsDisposed);
                Assert.True(dialog.Visible);
                Assert.True(dialog.CloseCleanupFailedForTest);

                releaseBarrier.TrySetResult();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle, "drain after release");

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                dialog.RetryCloseCleanupForTest();
                StaTestRunner.PumpUntil(() => dialog.IsDisposed, EditorSmokeTestAccess.DefaultTimeout);

                Assert.True(dialog.IsDisposed);
            }
            finally
            {
                EditorTestHooks.PanelOperationBarrierForTest = null;
                EditorTestHooks.GameDataCloseCleanupTimeoutForTest = null;
                if (dialog is { IsDisposed: false })
                {
                    EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                    try
                    {
                        dialog.Close();
                    }
                    catch
                    {
                        // teardown best-effort
                    }
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    [Theory]
    [InlineData(Phase8ContentKind.Dialogue)]
    [InlineData(Phase8ContentKind.Quest)]
    [InlineData(Phase8ContentKind.CommonEvent)]
    [InlineData(Phase8ContentKind.Profession)]
    [InlineData(Phase8ContentKind.Recipe)]
    [InlineData(Phase8ContentKind.Region)]
    [InlineData(Phase8ContentKind.WeatherProfile)]
    public void Phase8Editor_AllContentKinds_FullMatrix(Phase8ContentKind kind)
    {
        RunLocked(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;
            var service = new InMemoryPhase8ContentEditorService();
            EditorTestHooks.OverridePhase8ContentService = service;

            Phase8ContentBrowseDialog? dialog = null;
            try
            {
                dialog = new Phase8ContentBrowseDialog(service);
                dialog.Show();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle, "init idle");
                SelectKind(dialog, kind);
                PumpUntil(() => dialog.LifecycleForTest.IsIdle, "kind switched");

                var token = Guid.NewGuid().ToString("N")[..8];
                var originalName = $"R22-{kind}-A-{token}";
                var copyName = $"R22-{kind}-B-{token}";

                dialog.BtnNewForTest.PerformClick();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle && dialog.IsDirtyForTest, "create");
                dialog.NameForTest.Text = originalName;
                ApplyStructuredEdit(dialog, kind, "r22-edit");
                Assert.True(dialog.ActiveEditorForTest!.TryBuildPayload(out _, out var buildErr), buildErr);

                dialog.BtnSaveForTest.PerformClick();
                PumpUntil(
                    () => dialog.LifecycleForTest.IsIdle && !dialog.IsDirtyForTest && dialog.CurrentRevisionForTest > 0,
                    "draft save");
                Assert.Equal(ContentPublishStatus.Draft, dialog.CurrentStatusForTest);
                var savedId = dialog.CurrentIdForTest;
                var revisionAfterCreate = dialog.CurrentRevisionForTest;

                ApplyStructuredEdit(dialog, kind, "r22-resave");
                Assert.True(dialog.IsDirtyForTest);
                dialog.BtnSaveForTest.PerformClick();
                PumpUntil(
                    () => dialog.LifecycleForTest.IsIdle
                          && !dialog.IsDirtyForTest
                          && dialog.CurrentRevisionForTest > revisionAfterCreate,
                    "edit resave");
                AssertStructuredEdit(dialog, kind, "r22-resave");

                dialog.BtnPublishForTest.PerformClick();
                PumpUntil(
                    () => dialog.LifecycleForTest.IsIdle && dialog.CurrentStatusForTest == ContentPublishStatus.Published,
                    "publish");
                Assert.Equal(ContentPublishStatus.Published, dialog.CurrentStatusForTest);

                dialog.BtnDuplicateForTest.PerformClick();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle && dialog.IsDirtyForTest, "duplicate");
                dialog.NameForTest.Text = copyName;
                dialog.BtnSaveForTest.PerformClick();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle && !dialog.IsDirtyForTest, "duplicate save");
                var copyId = dialog.CurrentIdForTest;
                Assert.NotEqual(savedId, copyId);

                dialog.NameForTest.Text = string.Empty;
                Assert.True(dialog.IsDirtyForTest);
                dialog.BtnPublishForTest.PerformClick();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle, "invalid publish attempt");
                Assert.True(dialog.IsDirtyForTest || dialog.CurrentStatusForTest != ContentPublishStatus.Published);
                dialog.NameForTest.Text = copyName;
                dialog.BtnSaveForTest.PerformClick();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle && !dialog.IsDirtyForTest, "restore copy name");

                dialog.FilterForTest.Text = originalName;
                PumpUntil(
                    () => dialog.ItemsForTest.Items.Count == 1
                          && dialog.ItemsForTest.Items[0].Text.Equals(savedId.ToString("D"), StringComparison.OrdinalIgnoreCase),
                    "filter original");
                dialog.FilterForTest.Text = string.Empty;
                PumpUntil(() => dialog.ItemsForTest.Items.Count >= 2, "clear filter");

                SelectListItemById(dialog, copyId);
                PumpUntil(
                    () => dialog.LifecycleForTest.IsIdle && dialog.CurrentIdForTest == copyId,
                    "select copy");
                dialog.NameForTest.Text = copyName + "-dirty";
                Assert.True(dialog.IsDirtyForTest);
                EditorTestHooks.OverrideMessageBoxResult = DialogResult.No;
                SelectListItemById(dialog, savedId);
                PumpUntil(
                    () => dialog.LifecycleForTest.IsIdle && dialog.CurrentIdForTest == copyId && dialog.IsDirtyForTest,
                    "dirty nav cancel");
                Assert.Equal(copyId, dialog.CurrentIdForTest);

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                SelectListItemById(dialog, savedId);
                PumpUntil(
                    () => dialog.LifecycleForTest.IsIdle && dialog.CurrentIdForTest == savedId,
                    "dirty nav discard to original");
                AssertStructuredEdit(dialog, kind, "r22-resave");

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                dialog.Close();
                PumpUntil(() => dialog.IsDisposed, "close for reopen");

                dialog = new Phase8ContentBrowseDialog(service);
                dialog.Show();
                PumpUntil(() => dialog.LifecycleForTest.IsIdle, "reopen idle");
                SelectKind(dialog, kind);
                PumpUntil(() => dialog.LifecycleForTest.IsIdle, "reopen kind");
                SelectListItemById(dialog, savedId);
                PumpUntil(
                    () => dialog.LifecycleForTest.IsIdle && dialog.CurrentIdForTest == savedId,
                    "reopen select original");
                Assert.Equal(originalName, dialog.NameForTest.Text);
                AssertStructuredEdit(dialog, kind, "r22-resave");

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                dialog.BtnDeleteForTest.PerformClick();
                PumpUntil(
                    () => dialog.LifecycleForTest.IsIdle && dialog.CurrentIdForTest == Guid.Empty,
                    "delete original");
                Assert.Equal(Guid.Empty, dialog.CurrentIdForTest);
                Assert.False(ListContainsId(dialog, savedId));
                Assert.True(ListContainsId(dialog, copyId));
            }
            finally
            {
                if (dialog is { IsDisposed: false })
                {
                    EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                    dialog.Close();
                    PumpUntil(() => dialog.IsDisposed, "dispose");
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }

    private static void SelectKind(Phase8ContentBrowseDialog dialog, Phase8ContentKind kind)
    {
        dialog.KindComboForTest.SelectedIndex = (int)kind - 1;
    }

    private static bool ListContainsId(Phase8ContentBrowseDialog dialog, Guid id)
    {
        foreach (ListViewItem item in dialog.ItemsForTest.Items)
        {
            if (Guid.TryParse(item.Text, out var g) && g == id)
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyStructuredEdit(Phase8ContentBrowseDialog dialog, Phase8ContentKind kind, string marker)
    {
        var resave = string.Equals(marker, "r22-resave", StringComparison.Ordinal);
        switch (kind)
        {
            case Phase8ContentKind.Dialogue:
                var dialogue = Assert.IsType<Phase8DialogueEditorPanel>(dialog.ActiveEditorForTest);
                Assert.True(dialogue.LinesForTest.Rows.Count > 0);
                dialogue.LinesForTest.Rows[0].Cells[1].Value = marker;
                break;
            case Phase8ContentKind.Quest:
                var quest = Assert.IsType<Phase8QuestEditorPanel>(dialog.ActiveEditorForTest);
                quest.RepeatableForTest.Checked = true;
                quest.RewardGoldForTest.Value = resave ? 25 : 10;
                break;
            case Phase8ContentKind.CommonEvent:
                var common = Assert.IsType<Phase8CommonEventEditorPanel>(dialog.ActiveEditorForTest);
                var waitMs = resave ? 2500 : 1500;
                common.PagesPanelForTest.LoadPages(
                [
                    new MapEventPageDefinition
                    {
                        PageOrder = 0,
                        TriggerKind = Phase8MapEventTriggerKinds.Action,
                        Commands =
                        [
                            new MapEventCommandDefinition
                            {
                                Discriminator = MapEventCommandDiscriminators.Wait,
                                SchemaVersion = 1,
                                ParameterJson = $$"""{"milliseconds":{{waitMs}}}""",
                            },
                        ],
                    },
                ]);
                var waitField = Assert.IsType<NumericUpDown>(common.PagesPanelForTest.CommandParamsForTest.FieldForTest("milliseconds"));
                waitField.Value = waitMs;
                break;
            case Phase8ContentKind.Profession:
                var profession = Assert.IsType<Phase8ProfessionEditorPanel>(dialog.ActiveEditorForTest);
                profession.MaxLevelForTest.Value = resave ? 77 : 55;
                break;
            case Phase8ContentKind.Recipe:
                var recipe = Assert.IsType<Phase8RecipeEditorPanel>(dialog.ActiveEditorForTest);
                recipe.RequiredLevelForTest.Value = resave ? 12 : 6;
                break;
            case Phase8ContentKind.Region:
                var region = Assert.IsType<Phase8RegionEditorPanel>(dialog.ActiveEditorForTest);
                region.MapIdForTest.Value = resave ? 42 : 21;
                break;
            case Phase8ContentKind.WeatherProfile:
                var weather = Assert.IsType<Phase8WeatherEditorPanel>(dialog.ActiveEditorForTest);
                weather.WeatherKindForTest.Text = marker;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown content kind.");
        }
    }

    private static void AssertStructuredEdit(Phase8ContentBrowseDialog dialog, Phase8ContentKind kind, string marker)
    {
        var resave = string.Equals(marker, "r22-resave", StringComparison.Ordinal);
        switch (kind)
        {
            case Phase8ContentKind.Dialogue:
                var dialogue = Assert.IsType<Phase8DialogueEditorPanel>(dialog.ActiveEditorForTest);
                Assert.Equal(marker, Convert.ToString(dialogue.LinesForTest.Rows[0].Cells[1].Value));
                break;
            case Phase8ContentKind.Quest:
                var quest = Assert.IsType<Phase8QuestEditorPanel>(dialog.ActiveEditorForTest);
                Assert.True(quest.RepeatableForTest.Checked);
                Assert.Equal(resave ? 25 : 10, quest.RewardGoldForTest.Value);
                break;
            case Phase8ContentKind.CommonEvent:
                var common = Assert.IsType<Phase8CommonEventEditorPanel>(dialog.ActiveEditorForTest);
                var waitField = Assert.IsType<NumericUpDown>(common.PagesPanelForTest.CommandParamsForTest.FieldForTest("milliseconds"));
                Assert.Equal(resave ? 2500 : 1500, waitField.Value);
                break;
            case Phase8ContentKind.Profession:
                var profession = Assert.IsType<Phase8ProfessionEditorPanel>(dialog.ActiveEditorForTest);
                Assert.Equal(resave ? 77 : 55, profession.MaxLevelForTest.Value);
                break;
            case Phase8ContentKind.Recipe:
                var recipe = Assert.IsType<Phase8RecipeEditorPanel>(dialog.ActiveEditorForTest);
                Assert.Equal(resave ? 12 : 6, recipe.RequiredLevelForTest.Value);
                break;
            case Phase8ContentKind.Region:
                var region = Assert.IsType<Phase8RegionEditorPanel>(dialog.ActiveEditorForTest);
                Assert.Equal(resave ? 42 : 21, region.MapIdForTest.Value);
                break;
            case Phase8ContentKind.WeatherProfile:
                var weather = Assert.IsType<Phase8WeatherEditorPanel>(dialog.ActiveEditorForTest);
                Assert.Equal(marker, weather.WeatherKindForTest.Text);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown content kind.");
        }
    }

    private static void SelectListItemById(Phase8ContentBrowseDialog dialog, Guid id)
    {
        foreach (ListViewItem item in dialog.ItemsForTest.Items)
        {
            item.Selected = false;
        }

        foreach (ListViewItem item in dialog.ItemsForTest.Items)
        {
            if (Guid.TryParse(item.Text, out var g) && g == id)
            {
                item.Selected = true;
                item.Focused = true;
                return;
            }
        }
    }

    private static void PumpUntil(Func<bool> predicate, string step)
    {
        try
        {
            StaTestRunner.PumpUntil(predicate, EditorSmokeTestAccess.DefaultTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"Phase 8 editor smoke timed out at '{step}': {ex.Message}", ex);
        }
    }

    private static void SaveScreenshot(Form form, string fileName)
    {
        var root = EditorSmokeTestAccess.FindRepositoryRootForTest();
        var directory = Path.Combine(root, "artifacts", "phase-08-editor");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using var bitmap = new Bitmap(Math.Max(1, form.Width), Math.Max(1, form.Height));
        form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        bitmap.Save(path, ImageFormat.Png);
        if (new FileInfo(path).Length == 0)
        {
            throw new InvalidOperationException($"Empty screenshot: {path}");
        }
    }
}
