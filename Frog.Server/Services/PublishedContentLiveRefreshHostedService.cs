using Frog.Application.Content;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Services;

/// <summary>
/// Watches published-content stamps and refreshes already-connected sessions when
/// the editor republishes (separate DbContext / process from this host).
/// </summary>
public sealed class PublishedContentLiveRefreshHostedService(
    IPublishedContentRevisionStamp stamp,
    PublishedContentLiveRefreshCoordinator coordinator,
    ILogger<PublishedContentLiveRefreshHostedService> logger) : BackgroundService
{
    internal static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private long? _lastStamp;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var current = await stamp.GetStampAsync(stoppingToken).ConfigureAwait(false);
                if (_lastStamp is null)
                {
                    _lastStamp = current;
                }
                else if (current != _lastStamp.Value)
                {
                    _lastStamp = current;
                    await coordinator.RefreshConnectedSessionsAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Published-content live-refresh poll failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
