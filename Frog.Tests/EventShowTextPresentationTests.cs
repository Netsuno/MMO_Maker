using Frog.Core.Events;
using Xunit;

namespace Frog.Tests;

public sealed class EventShowTextPresentationTests
{
    private static readonly EventShowTextPresentation.Placement[] Placements =
    [
        new("Gardien", "gardien"),
    ];

    [Theory]
    [InlineData(true, "Bonjour.", false, "", "", true)]
    [InlineData(true, "  Porte ouverte.  ", false, "", "", true)]
    [InlineData(false, "Bonjour.", false, "", "", false)]
    [InlineData(true, "   ", false, "", "", false)]
    [InlineData(true, "Rien a interagir ici.", false, "", "", false)]
    [InlineData(true, "Ramasse.", false, "", "", false)]
    [InlineData(true, "Gardien (gardien)", false, "", "", false)]
    [InlineData(true, "[Page] Gardien (gardien)", false, "", "", false)]
    [InlineData(true, "[Marche] Gardien (gardien)", false, "", "", false)]
    [InlineData(true, "Gardien: Bonjour.", true, "Gardien", "Bonjour.", false)]
    [InlineData(true, "Bonjour.", true, "Gardien", "Bonjour.", false)]
    [InlineData(true, "Porte ouverte.", true, "Gardien", "Bonjour.", true)]
    [InlineData(true, "Bonjour.", true, "", "", true)]
    public void ShouldOpen_ShowsCreatorTextAndSkipsFallbacks(
        bool success,
        string message,
        bool dialogueActive,
        string speaker,
        string dialogueText,
        bool expected)
    {
        var open = EventShowTextPresentation.ShouldOpen(
            success,
            message,
            dialogueActive,
            speaker,
            dialogueText,
            Placements);
        Assert.Equal(expected, open);
    }

    [Fact]
    public void ShouldOpen_IgnoresNullPlacementsForFreeText()
    {
        Assert.True(EventShowTextPresentation.ShouldOpen(true, "Seul.", false, null, null, null));
        Assert.False(EventShowTextPresentation.ShouldOpen(true, "Ramasse.", false, null, null, null));
    }
}
