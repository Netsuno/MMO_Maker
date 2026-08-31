using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Caractérisation des fixtures .fcc (Phase 2). Le reader complet arrive en Task 9.
/// </summary>
public sealed class LegacyFccMapCharacterizationTests
{
    private static string FixturesMapsDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "fixtures", "legacy", "maps");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("fixtures/legacy/maps introuvable.");
        }
    }

    [Theory]
    [InlineData("map1.fcc", "Carte de test", 22, 2)]
    [InlineData("map2.fcc", "Carte 2", null, null)]
    [InlineData("map3.fcc", "", null, null)]
    public void FccHeader_MatchesObservedLayout(string fileName, string expectedNamePrefix, int? expectedRevision, int? expectedDown)
    {
        var path = Path.Combine(FixturesMapsDir, fileName);
        Assert.True(File.Exists(path), path);
        var data = File.ReadAllBytes(path);
        Assert.Equal(85_190, data.Length);

        var name = Encoding.Latin1.GetString(data.AsSpan(0, 40)).TrimEnd();
        Assert.StartsWith(expectedNamePrefix.TrimEnd(), name, StringComparison.Ordinal);

        if (expectedRevision is int rev)
        {
            Assert.Equal(rev, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(40, 4)));
        }

        if (expectedDown is int down)
        {
            Assert.Equal(down, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(49, 4)));
        }

        // Descripteur tableau 2D VB : 31×31 (0..30)
        Assert.Equal(31, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(76, 4)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(80, 4)));
        Assert.Equal(31, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(84, 4)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(88, 4)));
    }

    [Fact]
    public void TruncatedFixture_IsShorterThanValidMap()
    {
        var path = Path.Combine(FixturesMapsDir, "map1_truncated.fcc");
        var data = File.ReadAllBytes(path);
        Assert.True(data.Length < 85_190);
        Assert.Equal(200, data.Length);
    }

    [Fact]
    public void Map1_Sha256_IsStable()
    {
        var path = Path.Combine(FixturesMapsDir, "map1.fcc");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        Assert.Equal("95e09b2b794b4d3bdb8903b83fdd41abe7769204f00556a3240b13506e781f3b", hash);
    }

}
