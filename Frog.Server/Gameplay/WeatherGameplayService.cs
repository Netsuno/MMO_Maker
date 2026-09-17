using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Models;

namespace Frog.Server.Gameplay;

/// <summary>Météo et éclairage par région (P8-5).</summary>
public sealed class WeatherGameplayService(
    IPublishedRegionCatalog regions,
    IPublishedWeatherCatalog weather)
{
    private readonly IPublishedRegionCatalog _regions = regions;
    private readonly IPublishedWeatherCatalog _weather = weather;

    public async Task<WeatherSnapshot> GetWeatherForSessionAsync(
        int mapId,
        int tileX,
        int tileY,
        CancellationToken cancellationToken = default)
    {
        var region = await _regions.TryGetRegionForTileAsync(mapId, tileX, tileY, cancellationToken)
            .ConfigureAwait(false);
        if (region is null)
        {
            return new WeatherSnapshot();
        }

        var profile = await _weather.TryGetPublishedByIdAsync(region.WeatherProfileId, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            return new WeatherSnapshot { RegionId = region.Id };
        }

        return new WeatherSnapshot
        {
            WeatherKind = profile.WeatherKind,
            LightingFactor = profile.LightingFactor,
            RegionId = region.Id,
            WeatherProfileId = profile.Id,
        };
    }
}
