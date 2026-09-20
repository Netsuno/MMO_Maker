using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Xunit;

namespace Frog.Tests;

public sealed class PlayerWalkClockTests
{
    [Fact]
    public void Idle_UsesPlantedMiddleColumn()
    {
        Assert.Equal(1, PlayerWalkClock.Column(walking: false, elapsedMs: 0));
        Assert.Equal(1, PlayerWalkClock.Column(walking: false, elapsedMs: 10_000));
        Assert.Equal(1, PlayerSpritePose.IdleDown.SheetColumn);
        Assert.Equal(0, PlayerSpritePose.IdleDown.SheetRow);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(139, 0)]
    [InlineData(140, 1)]
    [InlineData(280, 2)]
    [InlineData(420, 1)]
    [InlineData(560, 0)]
    public void Walk_CyclesZeroOneTwoOne(int elapsedMs, int expectedColumn)
        => Assert.Equal(expectedColumn, PlayerWalkClock.Column(walking: true, elapsedMs: elapsedMs));

    [Fact]
    public void Row_MatchesDirectionEnumOrder()
    {
        Assert.Equal(0, PlayerWalkClock.Row(Direction.Down));
        Assert.Equal(1, PlayerWalkClock.Row(Direction.Left));
        Assert.Equal(2, PlayerWalkClock.Row(Direction.Right));
        Assert.Equal(3, PlayerWalkClock.Row(Direction.Up));
    }

    [Fact]
    public void FacingFromVector_PicksDominantAxis_KeepsFallbackWhenStill()
    {
        Assert.Equal(Direction.Down, PlayerWalkClock.FacingFromVector(0, 0, Direction.Down));
        Assert.Equal(Direction.Left, PlayerWalkClock.FacingFromVector(-1, 0, Direction.Down));
        Assert.Equal(Direction.Right, PlayerWalkClock.FacingFromVector(1, 0, Direction.Down));
        Assert.Equal(Direction.Up, PlayerWalkClock.FacingFromVector(0, -1, Direction.Down));
        Assert.Equal(Direction.Down, PlayerWalkClock.FacingFromVector(0, 1, Direction.Up));
        Assert.Equal(Direction.Right, PlayerWalkClock.FacingFromVector(1, 0.5f, Direction.Down));
    }

    [Fact]
    public void SheetMetrics_StayThirtyTwoCells()
    {
        Assert.Equal(32, PlayerWalkClock.NativeCellPixels);
        Assert.Equal(3, PlayerWalkClock.SheetColumns);
        Assert.Equal(4, PlayerWalkClock.SheetRows);
        Assert.Equal(32, Frog.Core.Constants.WorldMetrics.DefaultTileSizePixels);
    }
}
