using System.IO;
using System.Windows.Forms;
using Frog.Application.Assets;
using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor.Dialogs;
using Frog.Editor.Forms.GameData;
using Frog.Editor.Forms.Phase8;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class AudioResourceBrowserSmokeTests
{
    [Fact]
    public void Browser_ListsFrenchChooser_AndWritesRelativePathIntoWiredFields()
    {
        StaTestRunner.Run(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "frog-audio-browser-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Touch(root, "Audio/BGM/titre.ogg");
            Touch(root, "Audio/SE/pas.wav");
            Touch(root, "Audio/BGM/readme.txt");
            Touch(root, "Frog.Client/Assets/Audio/music-loop.wav");

            EditorTestHooks.OverrideMapAudioPickPath = null;
            EditorTestHooks.OverrideAudioResourceRoots = [root];
            EditorTestHooks.OverrideAudioResourceIndex = null;
            try
            {
                var bgm = AudioResourceCatalog.List(AudioResourceKind.Bgm, [root]);
                var se = AudioResourceCatalog.List(AudioResourceKind.Se, [root]);
                Assert.Equal(
                    ["Assets/Audio/music-loop.wav", "Audio/BGM/titre.ogg"],
                    bgm.Select(entry => entry.StoredAsset).ToArray());
                Assert.Equal(
                    ["Assets/Audio/music-loop.wav", "Audio/SE/pas.wav"],
                    se.Select(entry => entry.StoredAsset).ToArray());
                Assert.DoesNotContain(bgm, entry => entry.StoredAsset == "Audio/SE/pas.wav");

                var dialog = new AudioResourceBrowserDialog("Choisir la musique (BGM)", AudioResourceKind.Bgm, bgm);
                try
                {
                    Assert.Equal("Choisir la musique (BGM)", dialog.Text);
                    Assert.Equal("Musiques du projet : Audio/BGM et Assets/Audio.", dialog.HintForTest);
                    var labels = dialog.JoinedLabelsForTest;
                    Assert.Contains("Choisir", labels, StringComparison.Ordinal);
                    Assert.Contains("Annuler", labels, StringComparison.Ordinal);
                    var index = dialog.IndexOfStoredForTest("Audio/BGM/titre.ogg");
                    Assert.True(index >= 0);
                    dialog.SelectIndexForTest(index);
                    Assert.Equal("Audio/BGM/titre.ogg", dialog.SelectedStoredForTest);
                    Assert.Contains("Audio/BGM/titre.ogg", dialog.JoinedLabelsForTest, StringComparison.Ordinal);
                    dialog.AcceptForTest();
                    Assert.Equal("Audio/BGM/titre.ogg", dialog.AcceptedAsset);
                    Assert.Equal(DialogResult.OK, dialog.DialogResult);
                }
                finally
                {
                    dialog.Dispose();
                }

                using (var empty = new AudioResourceBrowserDialog("Choisir le son (SE)", AudioResourceKind.Se, []))
                {
                    Assert.Contains("Sons du projet : Audio/SE et Assets/Audio.", empty.HintForTest, StringComparison.Ordinal);
                    Assert.Contains(AudioResourceBrowserDialog.EmptyHint, empty.HintForTest, StringComparison.Ordinal);
                    Assert.Contains("Choisir", empty.JoinedLabelsForTest, StringComparison.Ordinal);
                }

                var map = DemoMapFactory.CreateStarter("Bois", 4, 4);
                using var props = new MapPropertiesDialog(map, null);
                Assert.Contains("Parcourir…", props.JoinedLabelsForTest, StringComparison.Ordinal);
                EditorTestHooks.OverrideAudioResourceIndex = IndexOf(bgm, "Audio/BGM/titre.ogg");
                props.ClickBgmBrowseForTest();
                Assert.Equal("Audio/BGM/titre.ogg", props.BgmAssetForTest);
                EditorTestHooks.OverrideAudioResourceIndex = IndexOf(se, "Audio/SE/pas.wav");
                props.ClickSeBrowseForTest();
                Assert.Equal("Audio/SE/pas.wav", props.SeAssetForTest);
                Assert.True(MapEditOperations.TryApplyProperties(map, props.PendingEdit, out var applyError), applyError);
                Assert.Equal("Audio/BGM/titre.ogg", map.Bgm.Asset);
                Assert.Equal("Audio/SE/pas.wav", map.Se.Asset);
                Assert.False(Path.IsPathRooted(map.Bgm.Asset));

                using var host = new Form { Width = 720, Height = 480 };
                var panel = new MapEventCommandParameterPanel { Dock = DockStyle.Fill };
                host.Controls.Add(panel);
                host.Show();
                panel.DiscriminatorForTest.SelectedItem = MapEventCommandDiscriminators.PlayBgm;
                Assert.Equal("Parcourir…", panel.AudioBrowseForTest?.Text);
                EditorTestHooks.OverrideAudioResourceIndex = IndexOf(bgm, "Audio/BGM/titre.ogg");
                panel.ClickAudioBrowseForTest();
                Assert.Equal("Audio/BGM/titre.ogg", Assert.IsType<TextBox>(panel.FieldForTest("asset")).Text);

                panel.DiscriminatorForTest.SelectedItem = MapEventCommandDiscriminators.PlaySe;
                EditorTestHooks.OverrideAudioResourceIndex = IndexOf(se, "Audio/SE/pas.wav");
                panel.ClickAudioBrowseForTest();
                Assert.True(panel.TryBuildCommand(out var command, out var buildError), buildError);
                Assert.Equal(MapEventCommandDiscriminators.PlaySe, command.Discriminator);
                Assert.True(
                    MapEventParameterSchemas.TryParsePlayAudio(
                        command.ParameterJson,
                        MapEventCommandDiscriminators.PlaySe,
                        out var track,
                        out var parseError),
                    parseError);
                Assert.Equal("Audio/SE/pas.wav", track.Asset);
                host.Close();

                var actors = new EmptyPublishedActors();
                var maps = new InMemoryMapRepository();
                using var system = new SystemEditorPanel(
                    new SystemFlagWorkspaceSession(new InMemorySystemFlagRepository()),
                    new SystemSettingsWorkspaceSession(new InMemorySystemSettingsRepository(actors, maps)),
                    actors,
                    maps,
                    ContentRepositoryCapabilities.InMemoryTest,
                    ContentRepositoryCapabilities.InMemoryTest);
                EditorTestHooks.OverrideAudioResourceIndex = IndexOf(bgm, "Audio/BGM/titre.ogg");
                system.ClickTitleBgmBrowseForTest();
                system.ClickStartBgmBrowseForTest();
                Assert.Equal("Audio/BGM/titre.ogg", system.TitleBgmAssetForTest.Text);
                Assert.Equal("Audio/BGM/titre.ogg", system.StartBgmAssetForTest.Text);
            }
            finally
            {
                EditorTestHooks.OverrideAudioResourceRoots = null;
                EditorTestHooks.OverrideAudioResourceIndex = null;
                EditorTestHooks.OverrideMapAudioPickPath = null;
                try
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, recursive: true);
                    }
                }
                catch (IOException)
                {
                    // Le nettoyage ne doit pas masquer l’échec du test.
                }
            }
        });
    }

    private static int IndexOf(IReadOnlyList<AudioResourceEntry> entries, string stored)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].StoredAsset, stored, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new InvalidOperationException("Fichier absent de la liste : " + stored);
    }

    private static void Touch(string root, string relative)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1, 2, 3, 4]);
    }

    private sealed class EmptyPublishedActors : IPublishedActorCatalog
    {
        public Task<IReadOnlyList<ActorDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ActorDefinition>>([]);
    }
}
