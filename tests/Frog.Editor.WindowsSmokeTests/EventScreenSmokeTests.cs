using System.Drawing;
using System.IO;
using Frog.Client;
using Frog.Client.Config;
using Frog.Core.Constants;
using Frog.Core.Events;
using Frog.Core.Maps;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EventScreenSmokeTests
{
    [Fact]
    public void FadeAndTint_PaintOverTheMap_TextStillOpens()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-event-screen-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "client-settings.json");
            var previous = Environment.GetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable);
            Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, path);
            MainShellForm? form = null;
            try
            {
                Assert.Equal((ushort)11, FrogWireProtocol.Version);
                form = ClientSmokeTestAccess.CreateAndShowMainShell();
                var map = MapSamples.StarterMeadow(Guid.Empty);
                var (focusX, focusY) = WorldMetrics.TileCenterToPixels(2, 3);
                form.ShowOfflineMapViewportForTest(map, focusX, focusY);
                form.LayoutGameHudForTest();

                var picture = MapEventPictureOp.ForShow(
                    1,
                    MapEventPicture.DefaultAsset,
                    8,
                    8,
                    255,
                    MapEventPicture.BlendNormal);
                var message = MapEventScreenWire.Compose(
                    [
                        MapEventVisualOp.ForScreen(MapEventScreenOp.ForFadeOut(0)),
                        MapEventVisualOp.ForPicture(picture),
                    ],
                    null,
                    "Bonjour.",
                    null);

                form.ApplyInteractResultForTest(true, message);
                Assert.Equal(MapEventScreen.MaxChannel, form.ScreenFadeForTest);
                Assert.Equal(0, form.ScreenTintOpacityForTest);
                Assert.False(form.ScreenTonePlayingForTest);
                Assert.Equal(1, form.EventPictureCountForTest);
                Assert.True(form.EventMessageOpenForTest);
                Assert.Equal("Bonjour.", form.EventMessageTextForTest);

                var bmp = Assert.IsType<Bitmap>(form.MapPictureForTest.Image);
                Assert.Equal(Color.FromArgb(255, 0, 0, 0), bmp.GetPixel(4, 4));

                form.ApplyInteractResultForTest(
                    true,
                    MapEventScreenWire.Compose(
                        [MapEventVisualOp.ForScreen(MapEventScreenOp.ForFadeIn(0))],
                        null,
                        null,
                        null));
                Assert.Equal(0, form.ScreenFadeForTest);
                Assert.True(form.EventMessageOpenForTest);
                var opened = Assert.IsType<Bitmap>(form.MapPictureForTest.Image);
                Assert.NotEqual(Color.FromArgb(255, 0, 0, 0), opened.GetPixel(4, 4));

                form.ApplyInteractResultForTest(
                    true,
                    MapEventScreenWire.Compose(
                        [MapEventVisualOp.ForScreen(MapEventScreenOp.ForTint(255, 0, 0, 255, 0))],
                        null,
                        null,
                        null));
                Assert.Equal(255, form.ScreenTintRedForTest);
                Assert.Equal(0, form.ScreenTintGreenForTest);
                Assert.Equal(0, form.ScreenTintBlueForTest);
                Assert.Equal(255, form.ScreenTintOpacityForTest);
                var tinted = Assert.IsType<Bitmap>(form.MapPictureForTest.Image);
                Assert.Equal(Color.FromArgb(255, 255, 0, 0), tinted.GetPixel(4, 4));

                form.ApplyInteractResultForTest(
                    true,
                    MapEventScreenWire.Compose(
                        [MapEventVisualOp.ForScreen(MapEventScreenOp.ForTint(0, 0, 0, 0, 0))],
                        null,
                        null,
                        null));
                Assert.Equal(0, form.ScreenTintOpacityForTest);
                Assert.Equal(0, form.ScreenFadeForTest);
                Assert.Equal(1, form.EventPictureCountForTest);
            }
            finally
            {
                if (form is not null)
                {
                    form.Close();
                    form.Dispose();
                }

                Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, previous);
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch (IOException)
                {
                    // Le fichier de réglages peut encore être tenu.
                }
            }
        });
    }
}
