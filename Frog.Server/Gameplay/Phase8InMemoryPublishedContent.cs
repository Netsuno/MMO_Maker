using Frog.Application.Content;
using Frog.Core.Models;

namespace Frog.Server.Gameplay;

/// <summary>Contenu Phase 8 publié en mémoire (playtest + tests unitaires).</summary>
public sealed class Phase8InMemoryPublishedContent
    : IPublishedDialogueCatalog,
        IPublishedQuestCatalog,
        IPublishedCommonEventCatalog,
        IPublishedProfessionCatalog,
        IPublishedRecipeCatalog,
        IPublishedRegionCatalog,
        IPublishedWeatherCatalog
{
    private readonly List<DialogueDefinition> _dialogues = new();
    private readonly List<QuestDefinition> _quests = new();
    private readonly List<CommonEventDefinition> _commonEvents = new();
    private readonly List<ProfessionDefinition> _professions = new();
    private readonly List<RecipeDefinition> _recipes = new();
    private readonly List<RegionDefinition> _regions = new();
    private readonly List<WeatherProfileDefinition> _weather = new();

    public void RegisterDialogue(DialogueDefinition definition) => _dialogues.Add(definition);

    public void RegisterQuest(QuestDefinition definition) => _quests.Add(definition);

    public void RegisterCommonEvent(CommonEventDefinition definition) => _commonEvents.Add(definition);

    public void RegisterProfession(ProfessionDefinition definition) => _professions.Add(definition);

    public void RegisterRecipe(RecipeDefinition definition) => _recipes.Add(definition);

    public void RegisterRegion(RegionDefinition definition) => _regions.Add(definition);

    public void RegisterWeather(WeatherProfileDefinition definition) => _weather.Add(definition);

    Task<IReadOnlyList<DialogueDefinition>> IPublishedDialogueCatalog.ListPublishedAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DialogueDefinition>>(_dialogues.ToList());

    Task<DialogueDefinition?> IPublishedDialogueCatalog.TryGetPublishedByIdAsync(Guid dialogueId, CancellationToken cancellationToken) =>
        Task.FromResult(_dialogues.FirstOrDefault(d => d.Id == dialogueId));

    Task<DialogueDefinition?> IPublishedDialogueCatalog.TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken) =>
        Task.FromResult(_dialogues.FirstOrDefault(d => d.EditorAliasId == editorAliasId));

    Task<long?> IPublishedDialogueCatalog.TryGetPublishedRevisionByIdAsync(Guid dialogueId, CancellationToken cancellationToken) =>
        Task.FromResult<long?>(_dialogues.Any(d => d.Id == dialogueId) ? 1L : null);

    Task<IReadOnlyList<QuestDefinition>> IPublishedQuestCatalog.ListPublishedAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QuestDefinition>>(_quests.ToList());

    Task<QuestDefinition?> IPublishedQuestCatalog.TryGetPublishedByIdAsync(Guid questId, CancellationToken cancellationToken) =>
        Task.FromResult(_quests.FirstOrDefault(q => q.Id == questId));

    Task<QuestDefinition?> IPublishedQuestCatalog.TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken) =>
        Task.FromResult(_quests.FirstOrDefault(q => q.EditorAliasId == editorAliasId));

    Task<IReadOnlyList<CommonEventDefinition>> IPublishedCommonEventCatalog.ListPublishedAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CommonEventDefinition>>(_commonEvents.ToList());

    Task<CommonEventDefinition?> IPublishedCommonEventCatalog.TryGetPublishedByIdAsync(Guid eventId, CancellationToken cancellationToken) =>
        Task.FromResult(_commonEvents.FirstOrDefault(e => e.Id == eventId));

    Task<CommonEventDefinition?> IPublishedCommonEventCatalog.TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken) =>
        Task.FromResult(_commonEvents.FirstOrDefault(e => e.EditorAliasId == editorAliasId));

    Task<IReadOnlyList<ProfessionDefinition>> IPublishedProfessionCatalog.ListPublishedAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProfessionDefinition>>(_professions.ToList());

    Task<ProfessionDefinition?> IPublishedProfessionCatalog.TryGetPublishedByIdAsync(Guid professionId, CancellationToken cancellationToken) =>
        Task.FromResult(_professions.FirstOrDefault(p => p.Id == professionId));

    Task<IReadOnlyList<RecipeDefinition>> IPublishedRecipeCatalog.ListPublishedAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RecipeDefinition>>(_recipes.ToList());

    Task<RecipeDefinition?> IPublishedRecipeCatalog.TryGetPublishedByIdAsync(Guid recipeId, CancellationToken cancellationToken) =>
        Task.FromResult(_recipes.FirstOrDefault(r => r.Id == recipeId));

    Task<IReadOnlyList<RegionDefinition>> IPublishedRegionCatalog.ListPublishedAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RegionDefinition>>(_regions.ToList());

    Task<RegionDefinition?> IPublishedRegionCatalog.TryGetRegionForTileAsync(
        int mapId,
        int tileX,
        int tileY,
        CancellationToken cancellationToken)
    {
        var match = _regions.FirstOrDefault(r => r.MapId == mapId && r.ContainsTile(tileX, tileY));
        return Task.FromResult(match);
    }

    Task<WeatherProfileDefinition?> IPublishedWeatherCatalog.TryGetPublishedByIdAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(_weather.FirstOrDefault(w => w.Id == profileId));
}
