using Frog.Persistence.PostgreSql.Entities.Auth;

namespace Frog.Persistence.PostgreSql.Entities.Player;

public sealed class CharacterEntity
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public Guid ClassId { get; set; }

    public int MapId { get; set; }

    public int PixelX { get; set; }

    public int PixelY { get; set; }

    public int Level { get; set; }

    public long Experience { get; set; }

    public int Hp { get; set; }

    public int MaxHp { get; set; }

    public int Mp { get; set; }

    public int MaxMp { get; set; }

    public int Gold { get; set; }

    public int BankGold { get; set; }

    public bool IsDead { get; set; }

    public int Str { get; set; }

    public int Agi { get; set; }

    public int Vit { get; set; }

    public int Int { get; set; }

    public int Dex { get; set; }

    public int Luck { get; set; }

    public Guid? StartingSpellId { get; set; }

    public Guid? EquippedWeaponItemId { get; set; }

    public Guid? EquippedArmorItemId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public AccountEntity Account { get; set; } = null!;

    public ICollection<InventorySlotEntity> InventorySlots { get; set; } = new List<InventorySlotEntity>();

    public ICollection<BankSlotEntity> BankSlots { get; set; } = new List<BankSlotEntity>();

    public ICollection<CharacterWorldSwitchEntity> WorldSwitches { get; set; } = new List<CharacterWorldSwitchEntity>();

    public ICollection<CharacterWorldVariableEntity> WorldVariables { get; set; } = new List<CharacterWorldVariableEntity>();
}
