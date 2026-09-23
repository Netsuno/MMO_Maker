using System.Collections.Generic;
using Frog.Core.Constants;
using Frog.Core.Events;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventInteractHintTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(31, 31, 0, 0)]
    [InlineData(32, 0, 1, 0)]
    [InlineData(80, 112, 2, 3)]
    public void TileOfCenter_matches_server_integer_division(int px, int py, int tileX, int tileY)
    {
        Assert.Equal((tileX, tileY), MapEventInteractHint.TileOfCenter(px, py));
        var center = WorldMetrics.TileCenterToPixels(tileX, tileY);
        if (px == center.PixelX && py == center.PixelY)
        {
            Assert.Equal((tileX, tileY), MapEventInteractHint.TileOfCenter(center.PixelX, center.PixelY));
        }
    }

    [Fact]
    public void Resolve_shows_french_talk_cue_for_named_action_event_on_player_tile()
    {
        var events = new[]
        {
            Event(placementId: 9, catalogId: 3, tileX: 2, tileY: 3, trigger: MapEventTriggerKinds.StepOn, name: "Piège"),
            Event(placementId: 4, catalogId: 2, tileX: 2, tileY: 3, trigger: Phase8MapEventTriggerKinds.Action, name: "Gardien"),
            Event(placementId: 1, catalogId: 8, tileX: 2, tileY: 3, trigger: MapEventTriggerKinds.Interact, name: "Autre"),
            Event(placementId: 1, catalogId: 2, tileX: 3, tileY: 3, trigger: MapEventTriggerKinds.Interact, name: "Voisin"),
        };

        var cue = MapEventInteractHint.Resolve(2, 3, events, "E", playing: true, dialogueOpen: false, inputBlocked: false);

        Assert.Equal("[E] Parler", cue);
        Assert.Equal(4, MapEventInteractHint.Select(2, 3, events)!.PlacementId);
    }

    [Fact]
    public void Resolve_uses_interagir_when_display_name_missing_and_honors_rebind()
    {
        var events = new[]
        {
            Event(placementId: 2, catalogId: 1, tileX: 1, tileY: 1, trigger: MapEventTriggerKinds.Interact, name: "  "),
        };

        Assert.Equal(
            "[F] Interagir",
            MapEventInteractHint.Resolve(1, 1, events, "F", playing: true, dialogueOpen: false, inputBlocked: false));
    }

    [Theory]
    [InlineData(MapEventTriggerKinds.StepOn)]
    [InlineData(MapEventTriggerKinds.Page)]
    [InlineData(MapEventTriggerKinds.AutoTile)]
    [InlineData(Phase8MapEventTriggerKinds.Autorun)]
    [InlineData(Phase8MapEventTriggerKinds.Parallel)]
    public void Resolve_hides_for_non_action_triggers(string trigger)
    {
        var events = new[]
        {
            Event(placementId: 1, catalogId: 1, tileX: 0, tileY: 0, trigger: trigger, name: "PNJ"),
        };

        Assert.Null(MapEventInteractHint.Resolve(0, 0, events, "E", playing: true, dialogueOpen: false, inputBlocked: false));
    }

    [Fact]
    public void Resolve_hides_when_not_playing_dialogue_open_input_blocked_or_away()
    {
        var events = new[]
        {
            Event(placementId: 1, catalogId: 1, tileX: 4, tileY: 5, trigger: MapEventTriggerKinds.Interact, name: "Gardien"),
        };

        Assert.Null(MapEventInteractHint.Resolve(4, 5, events, "E", playing: false, dialogueOpen: false, inputBlocked: false));
        Assert.Null(MapEventInteractHint.Resolve(4, 5, events, "E", playing: true, dialogueOpen: true, inputBlocked: false));
        Assert.Null(MapEventInteractHint.Resolve(4, 5, events, "E", playing: true, dialogueOpen: false, inputBlocked: true));
        Assert.Null(MapEventInteractHint.Resolve(4, 6, events, "E", playing: true, dialogueOpen: false, inputBlocked: false));
        Assert.Null(MapEventInteractHint.Resolve(4, 5, new List<MapEventWireEntry>(), "E", playing: true, dialogueOpen: false, inputBlocked: false));
    }

    [Fact]
    public void Action_wire_is_interact_and_player_contact_wire_is_step_on()
    {
        Assert.Equal(
            MapEventTriggerKinds.Interact,
            Phase8MapEventTriggerKinds.ToWireTriggerKind(Phase8MapEventTriggerKinds.Action));
        Assert.Equal(
            MapEventTriggerKinds.StepOn,
            Phase8MapEventTriggerKinds.ToWireTriggerKind(Phase8MapEventTriggerKinds.PlayerContact));
        Assert.True(MapEventInteractHint.IsActionTrigger(Phase8MapEventTriggerKinds.Action));
        Assert.True(MapEventInteractHint.IsActionTrigger(MapEventTriggerKinds.Interact));
        Assert.False(MapEventInteractHint.IsActionTrigger(MapEventTriggerKinds.StepOn));
    }

    [Fact]
    public void Select_breaks_ties_on_placement_id()
    {
        var events = new[]
        {
            Event(placementId: 20, catalogId: 5, tileX: 0, tileY: 0, trigger: "interact", name: "B"),
            Event(placementId: 7, catalogId: 5, tileX: 0, tileY: 0, trigger: "INTERACT", name: "A"),
        };

        Assert.Equal(7, MapEventInteractHint.Select(0, 0, events)!.PlacementId);
        Assert.Equal("[E] Parler", MapEventInteractHint.FormatCue("E", events[1]));
    }

    private static MapEventWireEntry Event(
        long placementId,
        int catalogId,
        int tileX,
        int tileY,
        string trigger,
        string name) =>
        new()
        {
            PlacementId = placementId,
            CatalogId = catalogId,
            TileX = tileX,
            TileY = tileY,
            TriggerKind = trigger,
            DisplayName = name,
            Slug = "evt",
        };
}
