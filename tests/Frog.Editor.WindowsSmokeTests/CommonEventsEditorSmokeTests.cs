using System.Windows.Forms;
using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor;
using Frog.Editor.Forms.Phase8;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Événements communs : liste, déclencheur, interrupteur, commande, brouillon et publication.</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class CommonEventsEditorSmokeTests
{
    [Fact]
    public void CommonEventsEditor_CreateSavePublish_TriggerSwitchAndCommand()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            EditorTestHooks.OverrideMessageBoxResult = DialogResult.OK;
            EditorTestHooks.OverrideNewContentIdFactory = () => Guid.Parse("cccccccc-dddd-eeee-ffff-000000000001");
            var service = new InMemoryPhase8ContentEditorService();

            CommonEventsEditorDialog? dialog = null;
            try
            {
                dialog = new CommonEventsEditorDialog(service);
                dialog.Show();
                PumpUntil(dialog, () => dialog.LifecycleForTest.IsIdle, "init");

                dialog.BtnNewForTest.PerformClick();
                PumpUntil(dialog, () => dialog.LifecycleForTest.IsIdle && dialog.IsDirtyForTest, "new");
                Assert.Equal(1, (int)dialog.AliasForTest.Value);

                dialog.NameForTest.Text = "Ouverture";
                dialog.SelectTriggerForTest(Phase8MapEventTriggerKinds.Autorun);
                dialog.SwitchForTest.Text = "intro_vue";
                dialog.SwitchActiveForTest.Checked = true;
                dialog.FlushHeaderForTest();
                dialog.EditorForTest.PagesPanelForTest.InsertPaletteForTest(MapEventCommandPalette.ShowTextId);

                dialog.BtnSaveForTest.PerformClick();
                PumpUntil(
                    dialog,
                    () => dialog.LifecycleForTest.IsIdle
                          && !dialog.IsDirtyForTest
                          && dialog.CurrentRevisionForTest > 0
                          && dialog.CurrentStatusForTest == ContentPublishStatus.Draft,
                    "save draft");

                dialog.BtnPublishForTest.PerformClick();
                PumpUntil(
                    dialog,
                    () => dialog.LifecycleForTest.IsIdle
                          && dialog.CurrentStatusForTest == ContentPublishStatus.Published
                          && dialog.ListForTest.Items.Count == 1,
                    "publish");

                var line = dialog.ListForTest.Items[0]?.ToString() ?? string.Empty;
                Assert.Contains("001", line, StringComparison.Ordinal);
                Assert.Contains("Ouverture", line, StringComparison.Ordinal);
                Assert.Contains("Publié", line, StringComparison.Ordinal);

                dialog.FilterForTest.Text = "Ouverture";
                Assert.Equal(1, dialog.ListForTest.Items.Count);
                dialog.FilterForTest.Text = "absent";
                Assert.Empty(dialog.ListForTest.Items);
                dialog.FilterForTest.Text = string.Empty;
                Assert.Equal(1, dialog.ListForTest.Items.Count);

                var stored = service.LoadDraftAsync(dialog.CurrentIdForTest).GetAwaiter().GetResult();
                Assert.NotNull(stored);
                Assert.True(
                    Phase8ContentPostgreSqlService.TryDeserialize(stored.PayloadJson, out CommonEventDefinition restored, out var deserError),
                    deserError);
                Assert.True(CommonEventEditorSheet.TryReadPage(restored.Pages, 0, out var trigger, out var switchId, out var active));
                Assert.Equal(Phase8MapEventTriggerKinds.Autorun, trigger);
                Assert.Equal("intro_vue", switchId);
                Assert.True(active);
                Assert.Equal(MapEventCommandDiscriminators.ShowText, restored.Pages[0].Commands[0].Discriminator);
                Assert.Equal(1, dialog.EditorForTest.PagesPanelForTest.CommandsForTest.Items.Count);

                dialog.BtnDuplicateForTest.PerformClick();
                PumpUntil(dialog, () => dialog.LifecycleForTest.IsIdle && dialog.IsDirtyForTest, "duplicate");
                Assert.Equal(2, (int)dialog.AliasForTest.Value);
                Assert.StartsWith("Copie de ", dialog.NameForTest.Text, StringComparison.Ordinal);
            }
            finally
            {
                if (dialog is { IsDisposed: false })
                {
                    EditorTestHooks.OverrideMessageBoxResult = DialogResult.Yes;
                    try
                    {
                        dialog.Close();
                        StaTestRunner.PumpUntil(() => dialog.IsDisposed, EditorSmokeTestAccess.DefaultTimeout);
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

    private static void PumpUntil(CommonEventsEditorDialog dialog, Func<bool> predicate, string step)
    {
        try
        {
            StaTestRunner.PumpUntil(predicate, EditorSmokeTestAccess.DefaultTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException(
                $"Événements communs smoke timed out at '{step}': dirty={dialog.IsDirtyForTest}, " +
                $"idle={dialog.LifecycleForTest.IsIdle}, status={dialog.CurrentStatusForTest}, " +
                $"rev={dialog.CurrentRevisionForTest}, validation='{dialog.ValidationForTest.Text}'. {ex.Message}",
                ex);
        }
    }
}
