using System;

namespace Frog.Tests;

/// <summary>PNG 32×32 RGBA uni (IHDR valide) pour tests sans System.Drawing.</summary>
internal static class SamplePng
{
    public static readonly byte[] Solid32Coral = Convert.FromHexString(
        "89504e470d0a1a0a0000000d4948445200000020000000200806000000737a7af40000002f4944415478daedce210100000803b0c779ff14b482189889f965dafd14010101010101010101010101010181efc00179385c7992f053410000000049454e44ae426082");
}
