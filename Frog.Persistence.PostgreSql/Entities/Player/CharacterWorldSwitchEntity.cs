namespace Frog.Persistence.PostgreSql.Entities.Player;

public sealed class CharacterWorldSwitchEntity
{
    public Guid CharacterId { get; set; }

    public string SwitchKey { get; set; } = string.Empty;

    public bool Value { get; set; }

    public CharacterEntity Character { get; set; } = null!;
}
