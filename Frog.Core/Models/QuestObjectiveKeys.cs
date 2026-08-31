namespace Frog.Core.Models;

public static class QuestObjectiveKeys
{
    public static string For(int stageIndex, int objectiveIndex) => $"{stageIndex}:{objectiveIndex}";
}
