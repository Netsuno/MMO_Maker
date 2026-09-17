namespace Frog.Persistence.PostgreSql.Entities.Player;

public sealed class GroundItemEntity
{
    public Guid Id { get; set; }

    public int MapId { get; set; }

    public int PixelX { get; set; }

    public int PixelY { get; set; }

    public Guid ItemId { get; set; }

    public int Quantity { get; set; }

    public Guid? OwnerCharacterId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? TakenAtUtc { get; set; }
}
