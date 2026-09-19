using System.IO;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>P10-3 : playtest/client depuis layouts <c>publish-frog</c> (pas seulement bin/ du dépôt).</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class Phase10PackagedLauncherResolveTests
{
    [Fact]
    public void ResolvesSiblingPublishLayouts_BeforeRepoBin()
    {
        var root = Path.Combine(Path.GetTempPath(), "frog-p10-3-layouts-" + Guid.NewGuid().ToString("N"));
        var editorDir = Path.Combine(root, "editor-win-x64");
        var clientDir = Path.Combine(root, "client-win-x64");
        var serverDir = Path.Combine(root, "server-win-x64");
        Directory.CreateDirectory(editorDir);
        Directory.CreateDirectory(clientDir);
        Directory.CreateDirectory(serverDir);
        var clientExe = Path.Combine(clientDir, "Frog.Client.exe");
        var serverExe = Path.Combine(serverDir, "Frog.Server.exe");
        File.WriteAllBytes(clientExe, [0]);
        File.WriteAllBytes(serverExe, [0]);
        try
        {
            Assert.True(EditorFrogClientLauncher.TryResolveExecutable(editorDir, out var resolvedClient));
            Assert.Equal(Path.GetFullPath(clientExe), Path.GetFullPath(resolvedClient));

            Assert.True(EditorFrogServerLauncher.TryResolveExecutable(editorDir, out var resolvedServer, out var useDll));
            Assert.False(useDll);
            Assert.Equal(Path.GetFullPath(serverExe), Path.GetFullPath(resolvedServer));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // temp
            }
        }
    }

    [Fact]
    public void ResolvesSameDirectoryDeliveredBinaries()
    {
        var dir = Path.Combine(Path.GetTempPath(), "frog-p10-3-flat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var clientExe = Path.Combine(dir, "Frog.Client.exe");
        var serverExe = Path.Combine(dir, "Frog.Server.exe");
        File.WriteAllBytes(clientExe, [0]);
        File.WriteAllBytes(serverExe, [0]);
        try
        {
            Assert.True(EditorFrogClientLauncher.TryResolveExecutable(dir, out var resolvedClient));
            Assert.Equal(Path.GetFullPath(clientExe), Path.GetFullPath(resolvedClient));
            Assert.True(EditorFrogServerLauncher.TryResolveExecutable(dir, out var resolvedServer, out _));
            Assert.Equal(Path.GetFullPath(serverExe), Path.GetFullPath(resolvedServer));
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // temp
            }
        }
    }
}
