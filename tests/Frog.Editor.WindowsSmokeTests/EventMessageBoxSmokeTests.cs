using System.IO;
using System.Windows.Forms;
using Frog.Client;
using Frog.Client.Config;
using Frog.Core.Constants;
using Frog.Core.Maps;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EventMessageBoxSmokeTests
{
    [Fact]
    public void ShowText_OpensAboveHotbar_AndDismissesOnEnterOrClick()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-event-message-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "client-settings.json");
            var previous = Environment.GetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable);
            Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, path);
            MainShellForm? form = null;
            try
            {
                form = ClientSmokeTestAccess.CreateAndShowMainShell();
                var map = MapSamples.StarterMeadow(Guid.Empty);
                var (focusX, focusY) = WorldMetrics.TileCenterToPixels(2, 3);
                form.ShowOfflineMapViewportForTest(map, focusX, focusY);
                form.FocusMapSurfaceForTest();
                form.SetMapEventsForTest(
                [
                    new MapEventWireEntry
                    {
                        PlacementId = 4,
                        CatalogId = 2,
                        TileX = 2,
                        TileY = 3,
                        DisplayName = "Gardien",
                        Slug = "gardien",
                    },
                ]);

                Assert.False(form.PresentInteractForTest(true, "Gardien (gardien)"));
                Assert.False(form.EventMessageOpenForTest);
                Assert.False(form.PresentInteractForTest(false, "Rien a interagir ici."));
                Assert.False(form.EventMessageOpenForTest);

                Assert.True(form.PresentInteractForTest(true, "Bonjour."));
                Assert.True(form.EventMessageOpenForTest);
                Assert.Equal("Bonjour.", form.EventMessageTextForTest);
                var box = form.EventMessageBoundsForTest;
                var hotbar = form.HotbarForTest.Bounds;
                Assert.True(box.Bottom <= hotbar.Top + 1, $"message {box} should sit above hotbar {hotbar}");
                var hostMid = form.WorldHostForTest.ClientSize.Width / 2;
                var boxMid = box.Left + (box.Width / 2);
                Assert.InRange(boxMid, hostMid - 120, hostMid + 120);

                form.PressPlayingKeyForTest(Keys.Enter);
                Assert.False(form.EventMessageOpenForTest);

                form.PushDialogueForTest(new DialogueStateWire
                {
                    Speaker = "Gardien",
                    Text = "Bonjour.",
                    Choices = [new DialogueChoiceWire { ChoiceId = "ok", Label = "Ok" }],
                });
                Assert.False(form.PresentInteractForTest(true, "Gardien: Bonjour."));
                Assert.False(form.EventMessageOpenForTest);
                Assert.True(form.PresentInteractForTest(true, "Porte ouverte."));
                Assert.Equal("Porte ouverte.", form.EventMessageTextForTest);

                form.ClickWorldForTest();
                Assert.False(form.EventMessageOpenForTest);
            }
            finally
            {
                if (form is not null)
                {
                    ClientSmokeTestAccess.CloseMainShell(form);
                }

                Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, previous);
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // ignore
                }
            }
        });
    }
}
