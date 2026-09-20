using System;
using System.IO;
using Frog.Application.Assets;
using Xunit;

namespace Frog.Tests;

public sealed class ProjectAssetImporterTests
{
    [Fact]
    public void Import_CopiesPng_ComputesShaAndIhdr()
    {
        var root = CreateRoot();
        var source = Path.Combine(Path.GetTempPath(), $"frog-import-src-{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(source, SamplePng.Solid32Coral);
            var result = ProjectAssetImporter.Import(source, root, ProjectAssetKind.Tiles, "solid32.png");
            Assert.True(result.Success, result.Error);
            Assert.Equal("tiles/solid32.png", result.LogicalPath);
            Assert.True(File.Exists(result.AbsolutePath));
            Assert.Equal(64, result.Sha256Hex!.Length);
            Assert.Equal(32, result.WidthPixels);
            Assert.Equal(32, result.HeightPixels);

            var resolved = ProjectAssetPathResolver.TryResolve(root, result.LogicalPath);
            Assert.Equal(ProjectAssetPathResolver.ResolveStatus.Success, resolved.Status);
        }
        finally
        {
            Cleanup(root, source);
        }
    }

    [Fact]
    public void Import_RejectsTraversalPreferredName()
    {
        var root = CreateRoot();
        var source = Path.Combine(Path.GetTempPath(), $"frog-import-src-{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(source, SamplePng.Solid32Coral);
            var result = ProjectAssetImporter.Import(source, root, ProjectAssetKind.Sprites, "../evil.png");
            Assert.True(result.Success, result.Error);
            Assert.StartsWith("sprites/", result.LogicalPath, StringComparison.Ordinal);
            Assert.DoesNotContain("..", result.LogicalPath, StringComparison.Ordinal);
        }
        finally
        {
            Cleanup(root, source);
        }
    }

    [Fact]
    public void Import_RejectsUnknownKindAndMissingFile()
    {
        var root = CreateRoot();
        try
        {
            var missing = ProjectAssetImporter.Import("/no/such/file.png", root, ProjectAssetKind.Tiles);
            Assert.False(missing.Success);

            var source = Path.Combine(root, "x.png");
            File.WriteAllBytes(source, SamplePng.Solid32Coral);
            var badKind = ProjectAssetImporter.Import(source, root, "folklore");
            Assert.False(badKind.Success);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Import_UniqueNameOnCollision()
    {
        var root = CreateRoot();
        var source = Path.Combine(Path.GetTempPath(), $"frog-import-src-{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(source, SamplePng.Solid32Coral);
            var first = ProjectAssetImporter.Import(source, root, ProjectAssetKind.Icons, "slot.png");
            var second = ProjectAssetImporter.Import(source, root, ProjectAssetKind.Icons, "slot.png");
            Assert.True(first.Success, first.Error);
            Assert.True(second.Success, second.Error);
            Assert.Equal("icons/slot.png", first.LogicalPath);
            Assert.Equal("icons/slot-2.png", second.LogicalPath);
        }
        finally
        {
            Cleanup(root, source);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"frog-import-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Cleanup(string root, string? extra = null)
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch
        {
            // best-effort
        }

        if (!string.IsNullOrWhiteSpace(extra))
        {
            try
            {
                File.Delete(extra);
            }
            catch
            {
                // best-effort
            }
        }
    }
}
