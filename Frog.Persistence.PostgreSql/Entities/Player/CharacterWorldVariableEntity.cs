namespace Frog.Persistence.PostgreSql.Entities.Player;

public sealed class CharacterWorldVariableEntity
{
    public Guid CharacterId { get; set; }

    public string VariableKey { get; set; } = string.Empty;

    public int Value { get; set; }
}
