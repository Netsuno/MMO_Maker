using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Server.Config;
using Frog.Server.Gameplay;
using Microsoft.Extensions.Options;

namespace Frog.Tests;

internal static class Phase7TestHelpers
{
    internal static readonly IOptions<Phase7ContentOptions> SyntheticContentOptions =
        Options.Create(new Phase7ContentOptions
        {
            AllowSyntheticContentFallback = true,
            RequirePublishedWorld = false,
        });

    internal static CharacterGameplayService CreateCharacterService(
        InMemoryCharacterRepository characters,
        Phase7PublishedContent content,
        IInventoryRepository? inventory = null)
        => new(
            characters,
            content,
            inventory ?? new InMemoryInventoryRepository(),
            NullPublishedWorldCatalog.Instance,
            SyntheticContentOptions);

    internal static CombatGameplayService CreateCombatService(
        InMemoryCharacterRepository characters,
        Phase7PublishedContent content,
        IInventoryRepository? inventory = null)
    {
        var charSvc = CreateCharacterService(characters, content, inventory);
        return new CombatGameplayService(
            content,
            content,
            content,
            characters,
            charSvc,
            new CombatMutationRepository(),
            new CharacterMutationCoordinator(),
            new InMemoryMonsterKillRewardRepository(characters));
    }
}
