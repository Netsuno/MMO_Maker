namespace Frog.Persistence.PostgreSql.Entities.Player;

public sealed class ShopStockEntity
{
    public Guid ShopId { get; set; }

    public Guid ItemId { get; set; }

    public int Remaining { get; set; }
}
