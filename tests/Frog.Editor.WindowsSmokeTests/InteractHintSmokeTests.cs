using System;
using System.IO;
using Frog.Client;
using Frog.Client.Config;
using Frog.Core.Constants;
using Frog.Core.Maps;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Indice HUD [E] Parler / Interagir sur la tuile du joueur.</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class InteractHintSmokeTests
{
    [Fact]
    public void Hint_ShowsOnActionTile_HidesWhenAway_Dialogue_Menu_OrChatFocus()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-interact-hint-" + Guid.NewGuid().ToString("N"));
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

                form.SetMapEventsForTest(new[]
                {
                    new MapEventWireEntry
                    {
                        PlacementId = 4,
                        CatalogId = 2,
                        TileX = 2,
                        TileY = 3,
                        TriggerKind = MapEventTriggerKinds.Interact,
                        DisplayName = "Gardien",
                        Slug = "gardien",
                    },
                    new MapEventWireEntry
                    {
                        PlacementId = 1,
                        CatalogId = 1,
                        TileX = 4,
                        TileY = 4,
                        TriggerKind = MapEventTriggerKinds.StepOn,
                        DisplayName = "Piège",
                        Slug = "piege",
                    },
                });

                form.LayoutGameHudForTest();
                Assert.True(form.InteractHintVisibleForTest);
                Assert.Equal("[E] Parler", form.InteractHintTextForTest);
                var hint = form.InteractHintBoundsForTest;
                var hotbar = form.HotbarForTest.Bounds;
                Assert.True(hint.Bottom <= hotbar.Top + 1, $"hint {hint} should sit above hotbar {hotbar}");
                var hostMid = form.WorldHostForTest.ClientSize.Width / 2;
                var hintMid = hint.Left + (hint.Width / 2);
                Assert.InRange(hintMid, hostMid - 80, hostMid + 80);

                form.SetInteractBindingForTest("F");
                Assert.Equal("[F] Parler", form.InteractHintTextForTest);

                form.SetInteractBindingForTest("E");
                form.SetWindowLayerVisibleForTest(true);
                Assert.Equal(string.Empty, form.InteractHintTextForTest);
                form.SetWindowLayerVisibleForTest(false);
                form.FocusMapSurfaceForTest();
                Assert.Equal("[E] Parler", form.InteractHintTextForTest);

                form.ChatTextBoxForTest.Focus();
                form.RefreshInteractHintForTest();
                Assert.True(form.ChatTextBoxForTest.Focused, "chat box should take focus");
                Assert.Equal(string.Empty, form.InteractHintTextForTest);

                form.FocusMapSurfaceForTest();
                form.RefreshInteractHintForTest();
                Assert.Equal("[E] Parler", form.InteractHintTextForTest);

                form.PushDialogueForTest(new DialogueStateWire
                {
                    Speaker = "Gardien",
                    Text = "Bonjour.",
                    Choices = new[] { new DialogueChoiceWire { ChoiceId = "ok", Label = "Ok" } },
                });
                Assert.Equal(string.Empty, form.InteractHintTextForTest);

                var away = WorldMetrics.TileCenterToPixels(4, 4);
                form.ShowOfflineMapViewportForTest(map, away.PixelX, away.PixelY);
                form.FocusMapSurfaceForTest();
                Assert.Equal(string.Empty, form.InteractHintTextForTest);

                form.ShowOfflineMapViewportForTest(map, focusX, focusY);
                form.FocusMapSurfaceForTest();
                Assert.Equal("[E] Parler", form.InteractHintTextForTest);

                form.SetMapEventsForTest(new[]
                {
                    new MapEventWireEntry
                    {
                        PlacementId = 1,
                        CatalogId = 1,
                        TileX = 2,
                        TileY = 3,
                        TriggerKind = MapEventTriggerKinds.StepOn,
                        DisplayName = "Piège",
                        Slug = "piege",
                    },
                });
                Assert.Equal(string.Empty, form.InteractHintTextForTest);
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
