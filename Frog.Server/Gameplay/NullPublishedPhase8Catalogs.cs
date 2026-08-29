using Frog.Application.Content;
using Frog.Core.Models;

namespace Frog.Server.Gameplay;

public sealed class NullPublishedDialogueCatalog : IPublishedDialogueCatalog
{
    public static NullPublishedDialogueCatalog Instance { get; } = new();

    public Task<IReadOnlyList<DialogueDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DialogueDefinition>>(Array.Empty<DialogueDefinition>());

    public Task<DialogueDefinition?> TryGetPublishedByIdAsync(Guid dialogueId, CancellationToken cancellationToken = default) =>
        Task.FromResult<DialogueDefinition?>(null);

    public Task<DialogueDefinition?> TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken = default) =>
        Task.FromResult<DialogueDefinition?>(null);
}

public sealed class NullPublishedQuestCatalog : IPublishedQuestCatalog
{
    public static NullPublishedQuestCatalog Instance { get; } = new();

    public Task<IReadOnlyList<QuestDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<QuestDefinition>>(Array.Empty<QuestDefinition>());

    public Task<QuestDefinition?> TryGetPublishedByIdAsync(Guid questId, CancellationToken cancellationToken = default) =>
        Task.FromResult<QuestDefinition?>(null);

    public Task<QuestDefinition?> TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken = default) =>
        Task.FromResult<QuestDefinition?>(null);
}

public sealed class NullPublishedCommonEventCatalog : IPublishedCommonEventCatalog
{
    public static NullPublishedCommonEventCatalog Instance { get; } = new();

    public Task<IReadOnlyList<CommonEventDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CommonEventDefinition>>(Array.Empty<CommonEventDefinition>());

    public Task<CommonEventDefinition?> TryGetPublishedByIdAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult<CommonEventDefinition?>(null);

    public Task<CommonEventDefinition?> TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken = default) =>
        Task.FromResult<CommonEventDefinition?>(null);
}

public sealed class NullPublishedProfessionCatalog : IPublishedProfessionCatalog
{
    public static NullPublishedProfessionCatalog Instance { get; } = new();

    public Task<IReadOnlyList<ProfessionDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProfessionDefinition>>(Array.Empty<ProfessionDefinition>());

    public Task<ProfessionDefinition?> TryGetPublishedByIdAsync(Guid professionId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ProfessionDefinition?>(null);
}

public sealed class NullPublishedRecipeCatalog : IPublishedRecipeCatalog
{
    public static NullPublishedRecipeCatalog Instance { get; } = new();

    public Task<IReadOnlyList<RecipeDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RecipeDefinition>>(Array.Empty<RecipeDefinition>());

    public Task<RecipeDefinition?> TryGetPublishedByIdAsync(Guid recipeId, CancellationToken cancellationToken = default) =>
        Task.FromResult<RecipeDefinition?>(null);
}

public sealed class NullPublishedRegionCatalog : IPublishedRegionCatalog
{
    public static NullPublishedRegionCatalog Instance { get; } = new();

    public Task<IReadOnlyList<RegionDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RegionDefinition>>(Array.Empty<RegionDefinition>());

    public Task<RegionDefinition?> TryGetRegionForTileAsync(
        int mapId,
        int tileX,
        int tileY,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<RegionDefinition?>(null);
}

public sealed class NullPublishedWeatherCatalog : IPublishedWeatherCatalog
{
    public static NullPublishedWeatherCatalog Instance { get; } = new();

    public Task<WeatherProfileDefinition?> TryGetPublishedByIdAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        Task.FromResult<WeatherProfileDefinition?>(null);
}
