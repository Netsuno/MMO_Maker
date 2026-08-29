using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Frog.Application.Content;
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
    [Fact]
    public void Phase8Editor_DialogueDraftPublishDirtyDiscard_AndCloseDuringPendingOp()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.EnsureWinFormsInitialized();
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

                EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                dialog.NameForTest.Text = "Pending Close";
                var barrierEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                EditorTestHooks.PanelOperationBarrierForTest = async (name, ct) =>
                {
                    if (!string.Equals(name, "save", StringComparison.Ordinal))
                    {
                        return;
                    }

                    barrierEntered.TrySetResult();
                    using var reg = ct.Register(() => { });
                    await releaseBarrier.Task.WaitAsync(ct).ConfigureAwait(false);
                };

                dialog.BtnSaveForTest.PerformClick();
                PumpUntil(() => barrierEntered.Task.IsCompleted, "barrier entered");
                Assert.True(dialog.LifecycleForTest.PendingCountForTest > 0);
                SaveScreenshot(dialog, "06-close-during-pending.png");

                dialog.Close();
                releaseBarrier.TrySetResult();
                PumpUntil(() => dialog.IsDisposed, "disposed after pending close");
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
