using Frog.Core.Events;
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

    public bool ProgressionChanged { get; set; }

    public bool StatsChanged { get; set; }

    public bool QuestsChanged { get; set; }

    public bool ProfessionsChanged { get; set; }

    public bool RecipesChanged { get; set; }

    public bool TeleportApplied { get; set; }

    /// <summary>Un <c>set_weather</c> connu a posé l'override, ou un changement de carte l'a retiré.</summary>
    public bool WeatherChanged { get; set; }

    public string? DialogueSummary { get; set; }

    public string? QuestSummary { get; set; }

    public DialogueStatePushWire? DialogueState { get; set; }

    /// <summary>Boutique publiée à ouvrir pour le joueur (commande <c>open_shop</c>).</summary>
    public Guid? OpenShopId { get; set; }

    /// <summary>Images affichées ou effacées pendant cette exécution, dans l'ordre.</summary>
    public List<MapEventPictureOp> PictureOps { get; } = [];

    /// <summary>Effets d'écran de cette exécution, dans l'ordre.</summary>
    public List<MapEventScreenOp> ScreenOps { get; } = [];

    /// <summary>Animations de carte de cette exécution, dans l'ordre. Tuiles résolues plus tard.</summary>
    public List<MapEventAnimationOp> AnimationOps { get; } = [];

    /// <summary>Ordre commun images / écran / animation. Voir <see cref="MapEventVisualSequence"/>.</summary>
    public List<string> VisualOrder { get; } = [];

    public void RecordPicture(MapEventPictureOp op)
    {
        PictureOps.Add(op);
        VisualOrder.Add(MapEventVisualSequence.Picture);
    }

    public void RecordScreen(MapEventScreenOp op)
    {
        ScreenOps.Add(op);
        VisualOrder.Add(MapEventVisualSequence.Screen);
    }

    public void RecordAnimation(MapEventAnimationOp op)
    {
        AnimationOps.Add(op);
        VisualOrder.Add(MapEventVisualSequence.Animation);
    }

    public IReadOnlyList<MapEventVisualOp> VisualOps =>
        MapEventVisualSequence.Expand(VisualOrder, PictureOps, ScreenOps, AnimationOps);

    public bool StopExecution { get; set; }

    /// <summary>Attente non terminale : reprise au heartbeat après <see cref="WaitUntilUtc"/>.</summary>
    public bool Waiting { get; set; }

    public DateTimeOffset? WaitUntilUtc { get; set; }

    public int? ResumeCommandIndex { get; set; }

    public IReadOnlyList<Frog.Core.Models.MapEventCommandDefinition>? PendingCommands { get; set; }

    public int CommonEventDepth { get; set; }

    /// <summary>Pile d'appels CE pour refuser les cycles CE→CE (J5-FIX-08) hors planification.</summary>
    public HashSet<Guid> CommonEventCallStack { get; } = [];

    public int TotalSteps { get; set; }

    public int BranchDepth { get; set; }
}
