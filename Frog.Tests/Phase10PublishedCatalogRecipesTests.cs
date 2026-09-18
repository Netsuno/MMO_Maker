using System;
using System.Text.Json;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

/// <summary>P10-3a : recettes additif JSON (fil inchangé, champ recipes optionnel).</summary>
public sealed class Phase10PublishedCatalogRecipesTests
{
    [Fact]
    public void PublishedCatalogWire_RecipesAdditive_OldPayloadStillDeserializes()
    {
        const string withoutRecipes = "{\"classes\":[],\"items\":[],\"spells\":[],\"shops\":[],\"npcs\":[]}";
        var old = JsonSerializer.Deserialize<PublishedCatalogWire>(withoutRecipes);
        Assert.NotNull(old);
        Assert.Empty(old!.Recipes);

        const string recipeId = "aaaaaaaa-0004-4000-8000-000000000001";
        var withRecipes =
            "{\"classes\":[],\"items\":[],\"spells\":[],\"shops\":[],\"npcs\":[],\"recipes\":[{\"id\":\""
            + recipeId
            + "\",\"name\":\"Smoke Potion\"}]}";
        var parsed = JsonSerializer.Deserialize<PublishedCatalogWire>(withRecipes);
        Assert.NotNull(parsed);
        Assert.Single(parsed!.Recipes);
        Assert.Equal(recipeId, parsed.Recipes[0].Id);
        Assert.Equal("Smoke Potion", parsed.Recipes[0].Name);
        Assert.DoesNotContain("guid", parsed.Recipes[0].Name, StringComparison.OrdinalIgnoreCase);
    }
}
