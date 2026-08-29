using System.Linq;
using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Editor.Services;

public readonly record struct PgEventCatalogRow(
    Guid EventId,
    string Slug,
    string DisplayName,
    int? EditorAliasId,
    long Revision,
    ContentPublishStatus Status,
    int PageCount);

public readonly record struct PgMapEventPlacementRow(
    Guid Id,
    Guid MapId,
    Guid EventDefinitionId,
    int TileX,
    int TileY,
    string Slug,
    string DisplayName,
    string TriggerKind);

/// <summary>Catalogue et placements d'événements carte via PostgreSQL (Phase 8).</summary>
public sealed class MapEventsPostgreSqlService : IDisposable
{
    private readonly IMapEventRepository _repository;
    private readonly FrogDbContextGate _gate;
    private readonly bool _ownsGate;

    public MapEventsPostgreSqlService(IMapEventRepository repository, FrogDbContextGate gate, bool ownsGate = false)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _ownsGate = ownsGate;
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public bool IsAvailable => Capabilities.IsDurablePersistence;

    public IReadOnlyList<PgEventCatalogRow> LoadCatalog()
    {
        var entries = _repository.ListSummariesAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        return entries
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => new PgEventCatalogRow(
                e.EventId,
                e.CatalogSlug ?? e.Name,
                e.Name,
                e.EditorAliasId,
                e.Revision,
                e.Status,
                e.PageCount))
            .ToList();
    }

    public IReadOnlyList<PgMapEventPlacementRow> LoadPlacementsForMap(Guid mapId)
    {
        if (mapId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(mapId));
        }

        return _gate.ExecuteAsync(async (db, ct) =>
        {
            var rows = await (
                    from p in db.MapEventPlacements.AsNoTracking()
                    where p.MapId == mapId
                    join e in db.MapEventDefinitions.AsNoTracking()
                        on p.EventDefinitionId equals e.Id
                    orderby p.TileY, p.TileX, p.Id
                    select new
                    {
                        p.Id,
                        p.MapId,
                        p.EventDefinitionId,
                        p.TileX,
                        p.TileY,
                        p.TriggerKind,
                        Slug = e.CatalogSlug ?? e.Name,
                        e.Name,
                    })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return rows.Select(r => new PgMapEventPlacementRow(
                r.Id,
                r.MapId,
                r.EventDefinitionId,
                r.TileX,
                r.TileY,
                r.Slug,
                r.Name,
                NormalizePhase8TriggerKind(r.TriggerKind))).ToList();
        }).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public static IReadOnlyList<MapEventMarkerView> ToMarkerViews(IReadOnlyList<PgMapEventPlacementRow> rows)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<MapEventMarkerView>();
        }

        return rows
            .GroupBy(r => (r.TileX, r.TileY))
            .Select(g =>
            {
                var ordered = g.OrderBy(x => x.Id).ToList();
                var first = ordered[0];
                return new MapEventMarkerView(
                    first.TileX,
                    first.TileY,
                    ordered.Count,
                    first.Slug,
                    NormalizePhase8TriggerKind(first.TriggerKind));
            })
            .OrderBy(m => m.TileY)
            .ThenBy(m => m.TileX)
            .ToList();
    }

    public bool TryInsertCatalog(string slug, string displayName, out Guid newEventId, out string errorMessage)
    {
        newEventId = Guid.Empty;
        errorMessage = string.Empty;

        var normalizedSlug = MapEventCatalogNormalization.TryNormalizeSlug(slug);
        var normalizedName = MapEventCatalogNormalization.TryNormalizeDisplayName(displayName);
        if (normalizedSlug is null)
        {
            errorMessage = "Slug invalide (lettres minuscules, chiffres, _ ; ex. pnj_marchand).";
            return false;
        }

        if (normalizedName is null)
        {
            errorMessage = "Nom affiché invalide.";
            return false;
        }

        var definition = new MapEventDefinition
        {
            Name = normalizedName,
            CatalogSlug = normalizedSlug,
            Pages =
            [
                new MapEventPageDefinition
                {
                    PageOrder = 0,
                    TriggerKind = Phase8MapEventTriggerKinds.Action,
                },
            ],
        };

        var save = _repository.SaveAsync(new SaveMapEventRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
        }).ConfigureAwait(false).GetAwaiter().GetResult();

        return save switch
        {
            SaveMapEventResult.Success success =>
                AssignSuccess(success.EventId, out newEventId),
            SaveMapEventResult.ValidationFailed failed =>
                Fail(failed.Error, out errorMessage),
            SaveMapEventResult.Conflict =>
                Fail("Conflit de révision lors de la création.", out errorMessage),
            SaveMapEventResult.PersistenceFailed failed =>
                Fail(failed.Error, out errorMessage),
            SaveMapEventResult.Referenced failed =>
                Fail(failed.Error, out errorMessage),
            _ => Fail("Création catalogue impossible.", out errorMessage),
        };

        static bool AssignSuccess(Guid id, out Guid newId)
        {
            newId = id;
            return id != Guid.Empty;
        }

        static bool Fail(string message, out string errorMessage)
        {
            errorMessage = message;
            return false;
        }
    }

    public bool TryDeleteCatalogById(Guid eventId, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (eventId == Guid.Empty)
        {
            errorMessage = "Identifiant catalogue invalide.";
            return false;
        }

        var delete = _repository.DeleteAsync(eventId).ConfigureAwait(false).GetAwaiter().GetResult();
        return delete switch
        {
            DeleteMapEventResult.Success => true,
            DeleteMapEventResult.NotFound => Fail("Aucune entrée catalogue supprimée (id inconnu).", out errorMessage),
            DeleteMapEventResult.Referenced referenced => Fail(referenced.Error, out errorMessage),
            DeleteMapEventResult.PersistenceFailed failed => Fail(failed.Error, out errorMessage),
            _ => Fail("Suppression catalogue impossible.", out errorMessage),
        };

        static bool Fail(string message, out string errorMessage)
        {
            errorMessage = message;
            return false;
        }
    }

    public bool TryInsertPlacement(
        Guid mapId,
        Guid eventDefinitionId,
        int tileX,
        int tileY,
        string? triggerKind,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (mapId == Guid.Empty || eventDefinitionId == Guid.Empty)
        {
            errorMessage = "map_id et event_id doivent être valides.";
            return false;
        }

        var tk = NormalizePhase8TriggerKind(triggerKind);
        if (!Phase8MapEventTriggerKinds.IsSupported(tk))
        {
            errorMessage = $"Déclencheur invalide: {tk}.";
            return false;
        }

        try
        {
            var failure = string.Empty;
            var ok = _gate.ExecuteAsync(async (db, ct) =>
            {
                var mapExists = await db.Maps.AsNoTracking().AnyAsync(m => m.Id == mapId, ct).ConfigureAwait(false);
                if (!mapExists)
                {
                    failure = "Carte introuvable dans le catalogue PostgreSQL.";
                    return false;
                }

                var eventExists = await db.MapEventDefinitions.AsNoTracking()
                    .AnyAsync(e => e.Id == eventDefinitionId, ct)
                    .ConfigureAwait(false);
                if (!eventExists)
                {
                    failure = "Événement catalogue introuvable.";
                    return false;
                }

                var duplicate = await db.MapEventPlacements.AsNoTracking()
                    .AnyAsync(
                        p => p.MapId == mapId
                             && p.TileX == tileX
                             && p.TileY == tileY
                             && p.EventDefinitionId == eventDefinitionId,
                        ct)
                    .ConfigureAwait(false);
                if (duplicate)
                {
                    failure = "Placement déjà présent pour cette carte, tuile et type.";
                    return false;
                }

                db.MapEventPlacements.Add(new MapEventPlacementEntity
                {
                    Id = Guid.NewGuid(),
                    MapId = mapId,
                    EventDefinitionId = eventDefinitionId,
                    TileX = tileX,
                    TileY = tileY,
                    TriggerKind = tk,
                    MovementKind = MapEventMovementKinds.Fixed,
                    RouteWaypointsJson = "[]",
                });
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return true;
            }).ConfigureAwait(false).GetAwaiter().GetResult();
            if (!ok)
            {
                errorMessage = failure;
            }

            return ok;
        }
        catch (DbUpdateException ex)
        {
            errorMessage = ex.InnerException?.Message ?? ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool TryUpdatePlacementTriggerKind(
        Guid placementId,
        Guid mapId,
        string? triggerKind,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (mapId == Guid.Empty || placementId == Guid.Empty)
        {
            errorMessage = "Identifiants invalides.";
            return false;
        }

        var tk = NormalizePhase8TriggerKind(triggerKind);
        if (!Phase8MapEventTriggerKinds.IsSupported(tk))
        {
            errorMessage = $"Déclencheur invalide: {tk}.";
            return false;
        }

        try
        {
            var failure = string.Empty;
            var ok = _gate.ExecuteAsync(async (db, ct) =>
            {
                var entity = await db.MapEventPlacements
                    .FirstOrDefaultAsync(p => p.Id == placementId && p.MapId == mapId, ct)
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    failure = "Aucune ligne mise à jour (id ou carte incorrect).";
                    return false;
                }

                entity.TriggerKind = tk;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return true;
            }).ConfigureAwait(false).GetAwaiter().GetResult();
            if (!ok)
            {
                errorMessage = failure;
            }

            return ok;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool TryDeletePlacement(Guid placementId, Guid mapId, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (mapId == Guid.Empty || placementId == Guid.Empty)
        {
            errorMessage = "Identifiants invalides.";
            return false;
        }

        try
        {
            var failure = string.Empty;
            var ok = _gate.ExecuteAsync(async (db, ct) =>
            {
                var n = await db.MapEventPlacements
                    .Where(p => p.Id == placementId && p.MapId == mapId)
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false);
                if (n == 0)
                {
                    failure = "Aucune ligne supprimée (id ou carte incorrect).";
                    return false;
                }

                return true;
            }).ConfigureAwait(false).GetAwaiter().GetResult();
            if (!ok)
            {
                errorMessage = failure;
            }

            return ok;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public async Task<string?> LoadPagesJsonAsync(Guid eventId)
    {
        var stored = await _repository.LoadByIdAsync(eventId).ConfigureAwait(false);
        if (stored is null)
        {
            return null;
        }

        return MapEventPagesCodec.SerializePages(stored.Definition.Pages);
    }

    public async Task<bool> TrySavePagesAsync(Guid eventId, string pagesJson, bool publish)
    {
        var stored = await _repository.LoadByIdAsync(eventId).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        if (!MapEventPagesCodec.TryDeserializePages(pagesJson, out var pages, out _))
        {
            return false;
        }

        var definition = stored.Definition;
        definition.Pages = pages;
        if (!definition.Validate(out _))
        {
            return false;
        }

        var save = await _repository.SaveAsync(new SaveMapEventRequest
        {
            EventId = eventId,
            Definition = definition,
            ExpectedRevision = stored.Revision,
            Intent = publish ? SaveContentIntent.Publish : SaveContentIntent.SaveDraft,
        }).ConfigureAwait(false);

        return save is SaveMapEventResult.Success;
    }

    public void Dispose()
    {
        if (_ownsGate)
        {
            _gate.Dispose();
        }
    }

    private static string NormalizePhase8TriggerKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Phase8MapEventTriggerKinds.Action;
        }

        var trimmed = raw.Trim();
        if (Phase8MapEventTriggerKinds.IsSupported(trimmed))
        {
            return trimmed;
        }

        return Phase8MapEventTriggerKinds.FromWireTriggerKind(
            MapEventTriggerNormalization.NormalizeTriggerKind(trimmed));
    }
}
