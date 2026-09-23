using Frog.Editor.Panels;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

public sealed class PrefabListFilterTests
{
    [Theory]
    [InlineData(null, "sofa", "Canapé", true)]
    [InlineData("   ", "sofa", "Canapé", true)]
    [InlineData("can", "sofa", "Canapé", true)]
    [InlineData("CANAPE", "sofa", "Canapé", true)]
    [InlineData("sofa", "sofa", "Canapé", true)]
    [InlineData("cloture", "fence-post", "Poteau de clôture", true)]
    [InlineData("zzz", "sofa", "Canapé", false)]
    [InlineData("table", "sofa", "Canapé", false)]
    public void Matches_NameOrId_IgnoresAccents(string? filter, string id, string name, bool expected)
    {
        Assert.Equal(expected, PrefabListFilter.Matches(filter, id, name));
    }
}
