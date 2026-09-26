using Frog.Application.Content;
using Frog.Core.Models;

namespace Frog.Editor.Services;

/// <summary>Boutique GameData proposée dans la commande « Ouvrir boutique » (guid + nom).</summary>
internal sealed record MapEventShopChoice(Guid Id, string Name)
{
    public override string ToString()
    {
        var id = Id.ToString("D");
        return string.IsNullOrWhiteSpace(Name) ? id : Name.Trim() + " — " + id;
    }
}

/// <summary>
/// Liste brouillon + publié pour le sélecteur. Sans Postgres, la liste reste vide
/// et l'identifiant se saisit comme les autres Guid.
/// </summary>
internal static class MapEventShopChoiceSource
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _cached;

    public static IReadOnlyList<MapEventShopChoice> Current { get; private set; } = [];

    public static event Action? Changed;

    public static async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_cached && EditorTestHooks.OverrideShopRepository is null)
        {
            return;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<MapEventShopChoice>? next = null;
        try
        {
            if (_cached && EditorTestHooks.OverrideShopRepository is null)
            {
                return;
            }

            var loaded = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (loaded is null)
            {
                return;
            }

            next = loaded;
            Current = loaded;
            if (EditorTestHooks.OverrideShopRepository is null)
            {
                _cached = true;
            }
        }
        finally
        {
            Gate.Release();
        }

        if (next is not null)
        {
            Changed?.Invoke();
        }
    }

    private static async Task<IReadOnlyList<MapEventShopChoice>?> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            IShopRepository repository;
            if (EditorTestHooks.OverrideShopRepository is { } injected)
            {
                repository = injected;
            }
            else if (string.Equals(
                         Environment.GetEnvironmentVariable(EditorMapRepositoryFactory.EnvForceInMemory),
                         "1",
                         StringComparison.Ordinal)
                     || string.IsNullOrWhiteSpace(EditorMapRepositoryFactory.ResolveConnectionString()))
            {
                return [];
            }
            else
            {
                repository = EditorShopRepositoryFactory.CreateBundle(EmptyPublishedItems.Instance).Repository;
            }

            var rows = await repository.ListSummariesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return rows
                .Where(row => row.ShopId != Guid.Empty)
                .Select(row => new MapEventShopChoice(row.ShopId, row.Name))
                .ToArray();
        }
        catch
        {
            return null;
        }
    }

    private sealed class EmptyPublishedItems : IPublishedItemCatalog
    {
        public static readonly EmptyPublishedItems Instance = new();

        public Task<IReadOnlyList<ItemDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ItemDefinition>>(Array.Empty<ItemDefinition>());

        public Task<ItemDefinition?> LoadPublishedByIdAsync(Guid itemId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ItemDefinition?>(null);
    }
}
