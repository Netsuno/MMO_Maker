using Frog.Core.Gameplay;

using Xunit;

namespace Frog.Tests;

public sealed class Phase7ProgressionTests
{
    [Fact]
    public void ApplyExperience_LevelsUpAtThreshold()
    {
        var (level, xp, gained) = ProgressionCurve.ApplyExperience(1, 0, ProgressionCurve.ExperienceToNextLevel(1));
        Assert.Equal(2, level);
        Assert.Equal(1, gained);
        Assert.Equal(0, xp);
    }

    [Fact]
    public void ApplyLevelUpBonuses_IncreasesStats()
    {
        var maxHp = 100;
        var maxMp = 50;
        var str = 10;
        var agi = 10;
        var vit = 10;
        var intel = 10;
        var dex = 10;
        var luck = 10;
        ProgressionCurve.ApplyLevelUpBonuses(ref maxHp, ref maxMp, ref str, ref agi, ref vit, ref intel, ref dex, ref luck, 1);
        Assert.Equal(110, maxHp);
        Assert.Equal(11, str);
    }

    [Fact]
    public void MonsterExperienceReward_IsDeterministic()
    {
        Assert.Equal(15, CombatFormulas.MonsterExperienceReward(1));
        Assert.Equal(CombatFormulas.MonsterExperienceReward(3), CombatFormulas.MonsterExperienceReward(3));
    }
}
