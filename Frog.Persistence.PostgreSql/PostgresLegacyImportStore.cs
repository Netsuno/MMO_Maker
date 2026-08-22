using Frog.Application.LegacyImport;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresLegacyImportStore : ILegacyImportStore
{
    private readonly FrogDbContext _db;
    private readonly TimeProvider _clock;

    public PostgresLegacyImportStore(FrogDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<RecordLegacyImportResult> RecordAsync(LegacyImportRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var existing = await _db.LegacyImports
            .SingleOrDefaultAsync(
                x => x.Sha256Hex == record.Sha256Hex && x.FormatType == record.FormatType,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return new RecordLegacyImportResult.AlreadyPresent(existing.Id);
        }

        var entity = new Entities.LegacyImportEntity
        {
            Id = Guid.NewGuid(),
            SourcePath = record.SourcePath,
            Sha256Hex = record.Sha256Hex,
            FormatType = record.FormatType,
            Result = record.Result,
            ReportJson = record.ReportJson,
            ImportedAtUtc = _clock.GetUtcNow(),
        };
        _db.LegacyImports.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new RecordLegacyImportResult.Created(entity.Id);
    }
}
