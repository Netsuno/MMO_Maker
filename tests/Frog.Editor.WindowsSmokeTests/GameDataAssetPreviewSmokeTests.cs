using System.IO;
using Frog.Application.Assets;
using Frog.Editor;
using Frog.Editor.Forms.GameData;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class GameDataAssetPreviewSmokeTests
{
    [Fact]
    public void AssetPreview_ValidMissingCorruptTraversalAndRefresh()
    {
        StaTestRunner.Run(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), $"frog-preview-smoke-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var validPath = Path.Combine(root, "tiles", "valid.png");
            Directory.CreateDirectory(Path.GetDirectoryName(validPath)!);
            using (var bmp = new System.Drawing.Bitmap(16, 16))
            {
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.Clear(System.Drawing.Color.Coral);
                bmp.Save(validPath, System.Drawing.Imaging.ImageFormat.Png);
            }

            var corruptPath = Path.Combine(root, "tiles", "corrupt.png");
            File.WriteAllText(corruptPath, "not-a-png");

            AssetPreviewControl? preview = null;
            try
            {
                preview = new AssetPreviewControl { AssetRoot = root };

                preview.LogicalPath = "tiles/valid.png";
                Assert.Equal(AssetPreviewState.Loaded, preview.PreviewState);

                preview.LogicalPath = "tiles/missing.png";
                Assert.Equal(AssetPreviewState.Missing, preview.PreviewState);

                preview.LogicalPath = "tiles/corrupt.png";
                Assert.Equal(AssetPreviewState.Corrupt, preview.PreviewState);

                preview.LogicalPath = "../outside.png";
                Assert.Equal(AssetPreviewState.Rejected, preview.PreviewState);

                preview.LogicalPath = "tiles/valid.png";
                Assert.Equal(AssetPreviewState.Loaded, preview.PreviewState);

                var traversal = ProjectAssetPathResolver.TryResolve(root, "../outside.png");
                Assert.Equal(ProjectAssetPathResolver.ResolveStatus.TraversalRejected, traversal.Status);
            }
            finally
            {
                preview?.Dispose();
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch
                {
                    // best-effort
                }
            }
        });
    }
}
