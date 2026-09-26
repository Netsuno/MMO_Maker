using Frog.Application.Content;
using Frog.Core.Events;

namespace Frog.Editor.Services;

public sealed class QuickEventPresetResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public Guid EventId { get; init; }

    public QuickEventPresetKind Kind { get; init; }

    public string TriggerKind { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string SwitchKey { get; init; } = string.Empty;

    public string? CounterKey { get; init; }
}

/// <summary>
/// Publie un type d'événement catalogue (coffre, porte, auberge) via le même
/// <c>TryInsertCatalog</c> / <c>TrySavePagesAsync</c> que les éditeurs Phase 8.
/// Le placement tuile reste <c>TryInsertPlacement</c>.
/// </summary>
public static class QuickEventPresetPublisher
{
    public static async Task<QuickEventPresetResult> PublishAsync(
        MapEventsPostgreSqlService mapEvents,
        string name,
        QuickEventPresetKind kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapEvents);

        if (!QuickEventPresetDraft.TryCreate(name, kind, out var draft, out var error) || draft is null)
        {
            return Fail(error ?? "Événement invalide.");
        }

        Guid eventId = Guid.Empty;
        var slugUsed = draft.Slug;
        var inserted = false;
        for (var attempt = 1; attempt <= QuickEventPresetDraft.MaxSlugAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            slugUsed = QuickEventPresetDraft.WithAttemptSuffix(draft.Slug, attempt);
            if (mapEvents.TryInsertCatalog(slugUsed, draft.DisplayName, out eventId, out var insertError))
            {
                inserted = true;
                break;
            }

            if (!IsSlugConflict(insertError))
            {
                return Fail(string.IsNullOrWhiteSpace(insertError) ? "Création du type d'événement impossible." : insertError);
            }
        }

        if (!inserted || eventId == Guid.Empty)
        {
            return Fail("Impossible de créer un slug catalogue unique pour ce nom.");
        }

        var pages = slugUsed == draft.Slug
            ? draft.Pages
            : QuickEventPresetDraft.BuildPages(kind, slugUsed);
        var pagesJson = MapEventPagesCodec.SerializePages(pages);
        var pagesPublished = await mapEvents.TrySavePagesAsync(eventId, pagesJson, publish: true).ConfigureAwait(false);
        if (!pagesPublished)
        {
            mapEvents.TryDeleteCatalogById(eventId, out _);
            return Fail("Publication des pages de l'événement impossible.");
        }

        return new QuickEventPresetResult
        {
            Success = true,
            EventId = eventId,
            Kind = kind,
            TriggerKind = draft.TriggerKind,
            DisplayName = draft.DisplayName,
            Slug = slugUsed,
            SwitchKey = QuickEventPresetDraft.StateKey(slugUsed, QuickEventPresetDraft.SwitchSuffix(kind)),
            CounterKey = QuickEventPresetDraft.CounterSuffix(kind) is { } counter
                ? QuickEventPresetDraft.StateKey(slugUsed, counter)
                : null,
        };
    }

    private static bool IsSlugConflict(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return false;
        }

        return error.Contains("déjà utilisé", StringComparison.OrdinalIgnoreCase)
            || error.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || error.Contains("23505", StringComparison.Ordinal)
            || error.Contains("catalog_slug", StringComparison.OrdinalIgnoreCase)
            || error.Contains("unique", StringComparison.OrdinalIgnoreCase);
    }

    private static QuickEventPresetResult Fail(string error) =>
        new()
        {
            Success = false,
            Error = error,
        };
}

/// <summary>Textes du raccourci coffre / porte / auberge.</summary>
public static class QuickEventPresetMessages
{
    public static string Title(QuickEventPresetKind kind) => kind switch
    {
        QuickEventPresetKind.Chest => "Coffre rapide",
        QuickEventPresetKind.Door => "Porte rapide",
        QuickEventPresetKind.Inn => "Auberge rapide",
        _ => "Événement rapide",
    };

    public static string MenuItem(QuickEventPresetKind kind) => kind switch
    {
        QuickEventPresetKind.Chest => "Coffre…",
        QuickEventPresetKind.Door => "Porte…",
        QuickEventPresetKind.Inn => "Auberge…",
        _ => "Événement…",
    };

    public static string ToolTip(QuickEventPresetKind kind) => kind switch
    {
        QuickEventPresetKind.Chest => "Coffre bloquant : texte, interrupteur, butin +1, puis coffre vide.",
        QuickEventPresetKind.Door => "Porte bloquante : texte et interrupteur d'ouverture. La case reste solide.",
        QuickEventPresetKind.Inn => "Auberge bloquante : repos, puis branche sur le nombre de nuits.",
        _ => "Événement rapide.",
    };

    public static string PostgresRequired(QuickEventPresetKind kind) =>
        $"{Title(kind)} nécessite PostgreSQL (FROG_POSTGRES_CONNECTION_STRING ou appsettings.Local.json) pour les événements carte.";

    public static string CreatedPlacementPrompt(string name, QuickEventPresetKind kind) =>
        $"{Title(kind)} « {name} » créé (pages publiées).\n\n"
        + QuickEventPresetDraft.Describe(kind) + "\n\n"
        + "Cliquez sur la carte pour le placer.\n"
        + "Échap annule le placement.\n\n"
        + "Ensuite : Fichier → Publier (PostgreSQL) pour la carte.";

    public static string StatusPlacementPrompt(string name) =>
        $"Cliquez pour placer « {name} » (Échap annule) · puis Fichier → Publier";

    public static string Placed(string name, int tileX, int tileY) =>
        $"« {name} » placé en ({tileX}, {tileY}). Fichier → Publier (PostgreSQL) pour la carte.";

    public static string NeedsCatalogMap(string name) =>
        $"« {name} » créé (pages publiées).\n\n"
        + "Aucune carte catalogue n'est ouverte : enregistrez la carte, puis placez l'événement via Carte → Événements carte.\n\n"
        + "Ensuite : Fichier → Publier (PostgreSQL).";

    public static string NeedsCatalogStatus(string name) =>
        $"« {name} » créé. Enregistrez la carte au catalogue avant de le placer.";

    public const string PlacementCancelled =
        "Placement annulé. L'événement reste dans le catalogue.";
}
