using Frog.Core.Protocol;

namespace Frog.Server.Gameplay;

/// <summary>État mutable produit par l'exécution d'une page d'événement.</summary>
public sealed class MapEventExecutionState
{
    public string? ShowText { get; set; }

    public bool SwitchesChanged { get; set; }

    public bool VariablesChanged { get; set; }

    public bool InventoryChanged { get; set; }

    public bool GoldChanged { get; set; }

    public bool TeleportApplied { get; set; }

    public string? DialogueSummary { get; set; }

    public string? QuestSummary { get; set; }

    public DialogueStatePushWire? DialogueState { get; set; }

    public bool StopExecution { get; set; }

    public int CommonEventDepth { get; set; }

    public int TotalSteps { get; set; }

    public int BranchDepth { get; set; }
}
