using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventTriggerNormalizationTests
{
    [Theory]
    [InlineData(null, MapEventTriggerKinds.Interact)]
    [InlineData("", MapEventTriggerKinds.Interact)]
    [InlineData("  ", MapEventTriggerKinds.Interact)]
    [InlineData("INTERACT", MapEventTriggerKinds.Interact)]
    [InlineData("interact", MapEventTriggerKinds.Interact)]
    [InlineData("Step_On", MapEventTriggerKinds.StepOn)]
    [InlineData("step_on", MapEventTriggerKinds.StepOn)]
    [InlineData("PAGE", MapEventTriggerKinds.Page)]
    [InlineData("page", MapEventTriggerKinds.Page)]
    public void NormalizeTriggerKind_maps_known_values(string? raw, string expected)
    {
        Assert.Equal(expected, MapEventTriggerNormalization.NormalizeTriggerKind(raw));
    }
}
