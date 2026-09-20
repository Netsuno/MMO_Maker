namespace Frog.Application.Playtest;

/// <summary>
/// Écrit les sidecars tileset dans le workspace playtest (et éventuellement le répertoire client)
/// avant le lancement des processus — même layout que <c>ClientTilesetLoader</c>.
/// </summary>
public interface IPlaytestAssetSidecar
{
    void Write(PlaytestLaunchPlan plan, string? clientExecutablePath);
}
