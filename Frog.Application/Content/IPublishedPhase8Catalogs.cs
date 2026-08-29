using Frog.Core.Models;

namespace Frog.Application.Content;

public interface IPublishedDialogueCatalog
{
    Task<IReadOnlyList<DialogueDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);

    Task<DialogueDefinition?> TryGetPublishedByIdAsync(Guid dialogueId, CancellationToken cancellationToken = default);

    Task<DialogueDefinition?> TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken = default);
}

public interface IPublishedQuestCatalog
{
    Task<IReadOnlyList<QuestDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);

    Task<QuestDefinition?> TryGetPublishedByIdAsync(Guid questId, CancellationToken cancellationToken = default);

    Task<QuestDefinition?> TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken = default);
}

public interface IPublishedCommonEventCatalog
{
    Task<IReadOnlyList<CommonEventDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);

    Task<CommonEventDefinition?> TryGetPublishedByIdAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<CommonEventDefinition?> TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken = default);
}

public interface IPublishedProfessionCatalog
{
    Task<IReadOnlyList<ProfessionDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);

    Task<ProfessionDefinition?> TryGetPublishedByIdAsync(Guid professionId, CancellationToken cancellationToken = default);
}

public interface IPublishedRecipeCatalog
{
    Task<IReadOnlyList<RecipeDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);

    Task<RecipeDefinition?> TryGetPublishedByIdAsync(Guid recipeId, CancellationToken cancellationToken = default);
}

public interface IPublishedRegionCatalog
{
    Task<IReadOnlyList<RegionDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);

    Task<RegionDefinition?> TryGetRegionForTileAsync(int mapId, int tileX, int tileY, CancellationToken cancellationToken = default);
}

public interface IPublishedWeatherCatalog
{
    Task<WeatherProfileDefinition?> TryGetPublishedByIdAsync(Guid profileId, CancellationToken cancellationToken = default);
}
