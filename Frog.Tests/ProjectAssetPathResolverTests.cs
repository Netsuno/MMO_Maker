using System;
using System.IO;
using Frog.Application.Assets;
using Xunit;

namespace Frog.Tests;

public sealed class ProjectAssetPathResolverTests
{
    [Fact]
    public void TryResolve_ValidPng_ReturnsSuccess()
    {
        var root = CreateRoot();
        try
        {
            var png = Path.Combine(root, "icons", "item.png");
            Directory.CreateDirectory(Path.GetDirectoryName(png)!);
            File.WriteAllBytes(png, [0x89, 0x50, 0x4E, 0x47]);

            var result = ProjectAssetPathResolver.TryResolve(root, "icons/item.png");
            Assert.Equal(ProjectAssetPathResolver.ResolveStatus.Success, result.Status);
            Assert.Equal(png, result.AbsolutePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryResolve_MissingFile_ReturnsNotFound()
    {
        var root = CreateRoot();
        try
        {
            var result = ProjectAssetPathResolver.TryResolve(root, "missing.png");
            Assert.Equal(ProjectAssetPathResolver.ResolveStatus.NotFound, result.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryResolve_PathTraversal_IsRejected()
    {
        var root = CreateRoot();
        try
        {
            var result = ProjectAssetPathResolver.TryResolve(root, "../secret.png");
            Assert.Equal(ProjectAssetPathResolver.ResolveStatus.TraversalRejected, result.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryResolve_AbsolutePath_IsRejected()
    {
        var root = CreateRoot();
        try
        {
            var result = ProjectAssetPathResolver.TryResolve(root, "/etc/passwd");
            Assert.Equal(ProjectAssetPathResolver.ResolveStatus.TraversalRejected, result.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"frog-resolver-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
