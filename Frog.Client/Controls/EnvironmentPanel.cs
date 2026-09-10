using System;
using System.Windows.Forms;
using Frog.Core.Protocol;

namespace Frog.Client.Controls;

/// <summary>Affichage météo / éclairage (<see cref="Frog.Core.Enums.PacketId.EnvironmentStatePush"/>).</summary>
public sealed class EnvironmentPanel : UserControl
{
    private readonly Label _lblMap = new() { AutoSize = true, Text = "Carte: —" };
    private readonly Label _lblRegion = new() { AutoSize = true, Text = "Région: —" };
    private readonly Label _lblWeather = new() { AutoSize = true, Text = "Météo: —" };
    private readonly Label _lblLighting = new() { AutoSize = true, Text = "Éclairage: —" };
    private Func<Guid, string> _weatherLookup = static id => id.ToString("N")[..8];

    public EnvironmentPanel()
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(4),
        };
        flow.Controls.Add(_lblMap);
        flow.Controls.Add(_lblRegion);
        flow.Controls.Add(_lblWeather);
        flow.Controls.Add(_lblLighting);
        Controls.Add(flow);
    }

    public Func<Guid, string> WeatherNameLookup
    {
        get => _weatherLookup;
        set => _weatherLookup = value ?? (static id => id.ToString("N")[..8]);
    }

    public void ApplyState(EnvironmentStateWire? state)
    {
        if (state is null)
        {
            _lblMap.Text = "Carte: —";
            _lblRegion.Text = "Région: —";
            _lblWeather.Text = "Météo: —";
            _lblLighting.Text = "Éclairage: —";
            return;
        }

        _lblMap.Text = $"Carte: {state.MapId}";
        _lblRegion.Text = state.RegionId is Guid region
            ? $"Région: {region.ToString("N")[..8]}"
            : "Région: —";
        _lblWeather.Text = state.WeatherProfileId is Guid weather
            ? $"Météo: {_weatherLookup(weather)}"
            : "Météo: —";
        _lblLighting.Text = $"Éclairage: {state.LightingLevel}/255 ({state.LightingLevel * 100 / 255}%)";
    }

    internal string MapLabelTextForTest => _lblMap.Text;

    internal string LightingLabelTextForTest => _lblLighting.Text;
}
