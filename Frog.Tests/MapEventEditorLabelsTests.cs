using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventEditorLabelsTests
{
    [Fact]
    public void ActivePageCaption_NamesTheEditedPage()
    {
        var page = new MapEventPageDefinition
        {
            TriggerKind = Phase8MapEventTriggerKinds.Autorun,
            Priority = 4,
            Conditions =
            [
                new MapEventConditionDefinition { Kind = MapEventConditionKinds.CharacterSwitch },
            ],
            Commands =
            [
                new MapEventCommandDefinition { Discriminator = MapEventCommandDiscriminators.ShowText },
                new MapEventCommandDefinition { Discriminator = MapEventCommandDiscriminators.Wait },
            ],
        };

        Assert.Equal(
            "Page active : 2 sur 3 — Automatique — priorité 4 — 1 condition, 2 commandes",
            MapEventEditorLabels.ActivePageCaption(1, 3, page));
        Assert.Contains("Aucune page active", MapEventEditorLabels.ActivePageCaption(-1, 0, null));
        Assert.Equal(
            "Page 2  ·  Automatique  ·  priorité 4  ·  1 condition  ·  2 commandes",
            MapEventEditorLabels.PageListLine(1, page));
    }

    [Fact]
    public void ConditionAndCommandLines_AreReadableWithoutChangingStoredKinds()
    {
        Assert.Equal("Action", MapEventEditorLabels.Trigger(Phase8MapEventTriggerKinds.Action));
        Assert.Equal("Contact joueur", MapEventEditorLabels.Trigger(Phase8MapEventTriggerKinds.PlayerContact));
        Assert.Equal("Trajet", MapEventEditorLabels.Movement(MapEventMovementKinds.Route));
        Assert.Equal("Attente", MapEventEditorLabels.RouteStep(MapEventRouteStepKinds.Wait));
        Assert.Equal("Bas", MapEventEditorLabels.RouteStep(MapEventRouteStepKinds.Down));
        Assert.True(MapEventEditorLabels.TryParseRouteStep("Attente", out var waitKind));
        Assert.Equal(MapEventRouteStepKinds.Wait, waitKind);
        Assert.True(MapEventEditorLabels.TryParseRouteStep("Déplacement", out var moveKind));
        Assert.Equal(MapEventRouteStepKinds.Move, moveKind);
        Assert.Equal("Interrupteur", MapEventEditorLabels.Field("switchId"));
        Assert.Equal("≥ supérieur ou égal", MapEventEditorLabels.CompareOp("gte"));

        Assert.Equal(
            "1. Interrupteur « gate_open » est oui",
            MapEventEditorLabels.ConditionListLine(
                0,
                MapEventConditionKinds.CharacterSwitch,
                """{"switchId":"gate_open","value":true}"""));
        Assert.Equal(
            "2. Variable « score » ≥ 10",
            MapEventEditorLabels.ConditionListLine(
                1,
                MapEventConditionKinds.CharacterVariableCompare,
                """{"variableId":"score","op":"gte","value":10}"""));
        Assert.Equal(
            "1. Texte : Structured smoke",
            MapEventEditorLabels.CommandListLine(
                0,
                MapEventCommandDiscriminators.ShowText,
                """{"text":"Structured smoke"}"""));
        Assert.Equal(
            "2. Attendre 500 ms",
            MapEventEditorLabels.CommandListLine(
                1,
                MapEventCommandDiscriminators.Wait,
                """{"milliseconds":500}"""));
        Assert.Equal(
            "Branche si / alors / sinon",
            MapEventEditorLabels.CommandSummary(MapEventCommandDiscriminators.Branch, "{}"));
        Assert.Equal(
            "character_switch",
            MapEventConditionKinds.CharacterSwitch);
    }

    [Theory]
    [InlineData(MapEventCommandDiscriminators.ShowText, "Texte")]
    [InlineData(MapEventCommandDiscriminators.ShowChoices, "Afficher choix")]
    [InlineData(MapEventCommandDiscriminators.PlayBgm, "Jouer BGM")]
    [InlineData(MapEventCommandDiscriminators.PlaySe, "Jouer SE")]
    [InlineData(MapEventCommandDiscriminators.SetSwitch, "Régler interrupteur")]
    [InlineData(MapEventCommandDiscriminators.Teleport, "Téléportation")]
    [InlineData(MapEventCommandDiscriminators.CallCommonEvent, "Événement commun")]
    [InlineData(MapEventCommandDiscriminators.OpenShop, "Ouvrir boutique")]
    public void CommandKind_UsesFrenchWithoutRenamingDiscriminator(string discriminator, string french)
    {
        Assert.Equal(french, MapEventEditorLabels.CommandKind(discriminator));
        Assert.Equal(french, MapEventEditorLabels.CommandSummary(discriminator, "not-json"));
    }
}
