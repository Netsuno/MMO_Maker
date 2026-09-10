namespace Frog.Application.LegacyImport;

public sealed class LegacyImportRecord
{
    public required string SourcePath { get; init; }
    public required string Sha256Hex { get; init; }
    public required string FormatType { get; init; }
    public required string Result { get; init; }
    public required string ReportJson { get; init; }
}

public abstract record RecordLegacyImportResult
{
    public sealed record Created(Guid Id) : RecordLegacyImportResult;
    public sealed record AlreadyPresent(Guid Id) : RecordLegacyImportResult;
}

public interface ILegacyImportStore
{
    Task<RecordLegacyImportResult> RecordAsync(LegacyImportRecord record, CancellationToken cancellationToken = default);
}
