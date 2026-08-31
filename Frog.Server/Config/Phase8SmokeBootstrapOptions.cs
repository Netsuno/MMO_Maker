namespace Frog.Server.Config;

/// <summary>Options smoke client Phase 8 (bootstrap dialogue/craft sur entrée en jeu).</summary>
public sealed class Phase8SmokeBootstrapOptions
{
    public const string SectionName = "Phase8Smoke";

    public bool Enabled { get; set; }

    public Guid DialogueId { get; set; } = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000001");

    public Guid QuestId { get; set; } = Guid.Parse("aaaaaaaa-0002-4000-8000-000000000001");

    public Guid ProfessionId { get; set; } = Guid.Parse("aaaaaaaa-0003-4000-8000-000000000001");

    public Guid RecipeId { get; set; } = Guid.Parse("aaaaaaaa-0004-4000-8000-000000000001");

    public Guid RegionId { get; set; } = Guid.Parse("aaaaaaaa-0005-4000-8000-000000000001");

    public Guid WeatherProfileId { get; set; } = Guid.Parse("aaaaaaaa-0006-4000-8000-000000000001");
}
