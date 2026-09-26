using System.Drawing;
using System.IO;
using Frog.Client;
using Frog.Client.Config;
using Frog.Client.UI;
using Frog.Core.Constants;
using Frog.Core.Events;
using Frog.Core.Maps;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EventPictureSmokeTests
{
    [Fact]
    public void ShowPicture_PaintsOnTheMap_EraseRemovesIt_TextStillOpens()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-event-picture-" + Guid.NewGuid().ToString("N"));
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

                var host = form.WorldHostForTest.ClientSize;
                var screenX = Math.Clamp(host.Width / 2, 0, 4000);
                var screenY = Math.Clamp(host.Height / 2, 0, 4000);
                var show = MapEventPictureOp.ForShow(
                    1,
                    MapEventPicture.DefaultAsset,
                    screenX,
                    screenY,
                    255,
                    MapEventPicture.BlendNormal);
                var message = MapEventPictureWire.Compose(new[] { show }, null, "Bonjour.", null);

                form.ApplyInteractResultForTest(true, message);
                Assert.Equal(1, form.EventPictureCountForTest);
                Assert.True(form.TryGetEventPictureForTest(1, out var x, out var y, out var opacity, out var blend));
                Assert.Equal(screenX, x);
                Assert.Equal(screenY, y);
                Assert.Equal(255, opacity);
                Assert.Equal(MapEventPicture.BlendNormal, blend);
                Assert.True(form.EventMessageOpenForTest);
                Assert.Equal("Bonjour.", form.EventMessageTextForTest);

                var bmp = Assert.IsType<Bitmap>(form.MapPictureForTest.Image);
                var loc = form.MapPictureLocationForTest;
                var px = screenX + 4 - loc.X;
                var py = screenY + 4 - loc.Y;
                Assert.InRange(px, 0, bmp.Width - 1);
                Assert.InRange(py, 0, bmp.Height - 1);
                Assert.Equal(EventPictureDraw.PlaceholderColor.ToArgb(), bmp.GetPixel(px, py).ToArgb());

                var shift = 48;
                var movedX = screenX + shift;
                var movedPxGuess = px + shift;
                if (movedPxGuess < 0 || movedPxGuess >= bmp.Width)
                {
                    shift = EventPictureDraw.PlaceholderWidth + 8;
                    movedX = screenX - shift;
                }
                var move = MapEventPictureWire.Compose(
                    new[] { MapEventPictureOp.ForMove(1, movedX, screenY, 255, MapEventPicture.BlendNormal) },
                    null,
                    null,
                    null);
                form.ApplyInteractResultForTest(true, move);
                Assert.Equal(1, form.EventPictureCountForTest);
                Assert.True(form.TryGetEventPictureForTest(1, out var mx, out var my, out var mOpacity, out var mBlend));
                Assert.Equal(movedX, mx);
                Assert.Equal(screenY, my);
                Assert.Equal(255, mOpacity);
                Assert.Equal(MapEventPicture.BlendNormal, mBlend);
                var movedBmp = Assert.IsType<Bitmap>(form.MapPictureForTest.Image);
                var movedPx = movedX + 4 - loc.X;
                Assert.InRange(movedPx, 0, movedBmp.Width - 1);
                Assert.NotEqual(EventPictureDraw.PlaceholderColor.ToArgb(), movedBmp.GetPixel(px, py).ToArgb());
                Assert.Equal(EventPictureDraw.PlaceholderColor.ToArgb(), movedBmp.GetPixel(movedPx, py).ToArgb());

                var tint = MapEventPictureWire.Compose(
                    new[] { MapEventPictureOp.ForTint(1, 255, 0, 0, 255) },
                    null,
                    null,
                    null);
                form.ApplyInteractResultForTest(true, tint);
                Assert.True(form.TryGetEventPictureTintForTest(1, out var red, out var green, out var blue, out var tintOpacity));
                Assert.Equal(255, red);
                Assert.Equal(0, green);
                Assert.Equal(0, blue);
                Assert.Equal(255, tintOpacity);
                var tinted = Assert.IsType<Bitmap>(form.MapPictureForTest.Image);
                var tintedPixel = tinted.GetPixel(movedPx, py);
                Assert.True(tintedPixel.R > 200, $"R={tintedPixel.R}");
                Assert.True(tintedPixel.G < 40, $"G={tintedPixel.G}");
                Assert.True(tintedPixel.B < 40, $"B={tintedPixel.B}");

                var erase = MapEventPictureWire.Compose(
                    new[] { MapEventPictureOp.ForErase(1) },
                    null,
                    null,
                    null);
                form.ApplyInteractResultForTest(true, erase);
                Assert.Equal(0, form.EventPictureCountForTest);
                Assert.True(form.EventMessageOpenForTest);
                Assert.Equal("Bonjour.", form.EventMessageTextForTest);
                var after = Assert.IsType<Bitmap>(form.MapPictureForTest.Image);
                Assert.NotEqual(EventPictureDraw.PlaceholderColor.ToArgb(), after.GetPixel(px, py).ToArgb());
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
