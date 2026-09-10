using Frog.Core.Protocol;

namespace Frog.Server.Gameplay;

/// <summary>État mutable produit par l'exécution d'une page d'événement.</summary>
public sealed class MapEventExecutionState
{
    public string? ShowText { get; set; }

    public bool SwitchesChanged { get; set; }

    public List<WorldSwitchWire> SwitchChanges { get; } = [];

    public void RecordSwitch(string switchId, bool value)
    {
        SwitchesChanged = true;
        for (var i = 0; i < SwitchChanges.Count; i++)
        {
            if (string.Equals(SwitchChanges[i].SwitchId, switchId, StringComparison.Ordinal))
            {
                SwitchChanges[i].Value = value;
                return;
            }
        }

        SwitchChanges.Add(new WorldSwitchWire { SwitchId = switchId, Value = value });
    }

    public bool VariablesChanged { get; set; }

    public bool InventoryChanged { get; set; }

    public bool GoldChanged { get; set; }

    public bool QuestsChanged { get; set; }

    public bool ProfessionsChanged { get; set; }

    public bool RecipesChanged { get; set; }

    public bool TeleportApplied { get; set; }

    public string? DialogueSummary { get; set; }

    public string? QuestSummary { get; set; }

    public DialogueStatePushWire? DialogueState { get; set; }

    public bool StopExecution { get; set; }

    /// <summary>Attente non terminale : reprise au heartbeat après <see cref="WaitUntilUtc"/>.</summary>
    public bool Waiting { get; set; }

    public DateTimeOffset? WaitUntilUtc { get; set; }

    public int? ResumeCommandIndex { get; set; }

    public IReadOnlyList<Frog.Core.Models.MapEventCommandDefinition>? PendingCommands { get; set; }

    public int CommonEventDepth { get; set; }

    public int TotalSteps { get; set; }

    public int BranchDepth { get; set; }
}
