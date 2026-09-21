using Frog.Core.Protocol;

namespace Frog.Application.Prefabs;

/// <summary>Consommation serveur : prefabs publiés (catalogue + placements par carte).</summary>
public sealed class PublishedPrefabCatalogBundle
{
    public IReadOnlyList<PublishedPrefabWireEntry> Prefabs { get; init; } =
        Array.Empty<PublishedPrefabWireEntry>();

    public IReadOnlyList<PublishedPrefabMapWireEntry> PrefabMaps { get; init; } =
        Array.Empty<PublishedPrefabMapWireEntry>();
}

public interface IPublishedPrefabCatalog
{
    Task<PublishedPrefabCatalogBundle> LoadPublishedAsync(CancellationToken cancellationToken = default);
}

public sealed class EmptyPublishedPrefabCatalog : IPublishedPrefabCatalog
{
    public static readonly EmptyPublishedPrefabCatalog Instance = new();

    public Task<PublishedPrefabCatalogBundle> LoadPublishedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PublishedPrefabCatalogBundle());
}
