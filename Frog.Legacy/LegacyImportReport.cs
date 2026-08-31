namespace Frog.Legacy;

public sealed class LegacyImportReport
{
    public required string SourcePath { get; init; }
    public required string Sha256Hex { get; init; }
    public bool Success { get; set; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Unsupported { get; } = new();
}

public sealed class LegacyFccMapReadResult
{
    public required LegacyImportReport Report { get; init; }
    public Frog.Core.Models.Map? Map { get; init; }
    public int Revision { get; init; }
    public byte Moral { get; init; }
    public int Up { get; init; }
    public int Down { get; init; }
    public int Left { get; init; }
    public int Right { get; init; }
}
