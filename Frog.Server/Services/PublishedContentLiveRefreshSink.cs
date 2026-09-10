using Frog.Server.Gameplay;
using Frog.Server.Models;
using Frog.Server.Network;

namespace Frog.Server.Services;

/// <summary>
/// Catalog + map-event placements + environment. Visit/quest side-effects are skipped
/// so a republish cannot mutate objective progress.
/// </summary>
public sealed class PublishedContentLiveRefreshSink(
    PublishedCatalogService catalog,
    Phase8GameplayHandlers phase8,
    PacketSender sender) : IPublishedContentLiveRefreshSink
{
    public async Task PushToSessionAsync(
        ClientSession client,
        Session session,
        CancellationToken cancellationToken)
    {
        var catalogJson = await catalog.BuildJsonAsync(cancellationToken).ConfigureAwait(false);
        await sender.SendPublishedCatalogResultAsync(client, catalogJson, cancellationToken)
            .ConfigureAwait(false);

        var mapJson = await phase8.BuildMapEventsWireJsonAsync(
                session.CurrentMapId,
                cancellationToken,
                session.CharacterGuid)
            .ConfigureAwait(false);
        await sender.SendMapEventsResultAsync(client, session.CurrentMapId, mapJson, cancellationToken)
            .ConfigureAwait(false);

        await phase8.SendEnvironmentStatePushOnlyAsync(client, session, cancellationToken)
            .ConfigureAwait(false);
    }
}
