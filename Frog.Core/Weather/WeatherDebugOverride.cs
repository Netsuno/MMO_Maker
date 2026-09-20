namespace Frog.Core.Weather;

/// <summary>Toggle debug client (F8). <see cref="Auto"/> = météo publiée / catalogue.</summary>
public enum WeatherDebugOverride : byte
{
    Auto = 0,
    Clear = 1,
    Rain = 2,
    Fog = 3,
}
