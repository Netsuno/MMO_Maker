namespace Frog.Application.Gameplay;

public sealed record GroundItemRecord(
    Guid Id,
    int MapId,
    int PixelX,
    int PixelY,
    Guid ItemId,
    int Quantity,
    Guid? OwnerCharacterId,
    DateTimeOffset CreatedAtUtc);

public enum GroundItemMutationStatus
{
    Ok,
    NotFound,
    OutOfRange,
    AlreadyTaken,
    MapFull,
    InvalidQuantity,
}

public sealed record GroundItemMutationResult(
    GroundItemMutationStatus Status,
    GroundItemRecord? Item = null,
    string? ErrorMessage = null);

public interface IGroundItemRepository
{
    Task<IReadOnlyList<GroundItemRecord>> ListOnMapAsync(int mapId, CancellationToken cancellationToken = default);

    Task<GroundItemMutationResult> DropAsync(
        int mapId,
        int pixelX,
        int pixelY,
        Guid itemId,
        int quantity,
        Guid? ownerCharacterId,
        CancellationToken cancellationToken = default);

    /// <summary>Ramassage atomique : exactement un gagnant sous concurrence.</summary>
    Task<GroundItemMutationResult> TryPickupAsync(
        Guid groundItemId,
        Guid pickerCharacterId,
        int pickerPixelX,
        int pickerPixelY,
        int rangePixels,
        CancellationToken cancellationToken = default);
}
