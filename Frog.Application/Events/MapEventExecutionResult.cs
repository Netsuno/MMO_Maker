using Frog.Core.Events;
using Frog.Core.Protocol;

namespace Frog.Application.Events;

public sealed class MapEventExecutionResult
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    /// <summary>Texte client si une commande <c>show_text</c> a été exécutée.</summary>
    public string? ShowText { get; init; }

    public bool SwitchesChanged { get; init; }

    /// <summary>Interrupteurs mutés par <c>set_switch</c> (fil public <c>WorldSwitchSnapshot</c>).</summary>
    public IReadOnlyList<WorldSwitchWire> SwitchChanges { get; init; } = Array.Empty<WorldSwitchWire>();

    public bool VariablesChanged { get; init; }

    public bool InventoryChanged { get; init; }

    public bool GoldChanged { get; init; }

    public bool QuestsChanged { get; init; }

    public bool ProfessionsChanged { get; init; }

    public bool RecipesChanged { get; init; }

    public bool TeleportApplied { get; init; }

    /// <summary>La météo de session a changé : pousser <c>EnvironmentStatePush</c> (opcode 74).</summary>
    public bool WeatherChanged { get; init; }

    public string? DialogueSummary { get; init; }

    public string? QuestSummary { get; init; }

    public DialogueStatePushWire? DialogueState { get; init; }

    /// <summary>Boutique publiée à ouvrir chez le joueur (commande <c>open_shop</c>).</summary>
    public Guid? OpenShopId { get; init; }

    /// <summary>Images à afficher, déplacer, teinter ou effacer chez le joueur.</summary>
    public IReadOnlyList<MapEventPictureOp> PictureOps { get; init; } = Array.Empty<MapEventPictureOp>();

    /// <summary>Effets d'écran à jouer chez le joueur (fondu, teinte, tremblement, flash).</summary>
    public IReadOnlyList<MapEventScreenOp> ScreenOps { get; init; } = Array.Empty<MapEventScreenOp>();

    /// <summary>Images et effets d'écran dans l'ordre de la page. Vide : seules les images partent.</summary>
    public IReadOnlyList<MapEventVisualOp> VisualOps { get; init; } = Array.Empty<MapEventVisualOp>();

    /// <summary>
    /// Message <c>InteractResult</c>. Une boutique ouverte préfixe <c>shop:&lt;guid&gt;</c>
    /// sans remplacer un <c>show_text</c> déjà produit. Les images préfixent des lignes <c>pic:</c>.
    /// Fondu, teinte, tremblement, flash et animation partagent ce préfixe
    /// (<c>fade:</c>, <c>tint:</c>, <c>shake:</c>, <c>flash:</c>, <c>anim:</c>), Hello 11.
    /// </summary>
    public string ClientInteractMessage =>
        VisualOps.Count > 0
            ? MapEventScreenWire.Compose(VisualOps, OpenShopId, ShowText, Message)
            : MapEventPictureWire.Compose(PictureOps, OpenShopId, ShowText, Message);

    public static MapEventExecutionResult Ok(
        string message,
        string? showText = null,
        bool switchesChanged = false,
        bool variablesChanged = false,
        bool inventoryChanged = false,
        bool goldChanged = false,
        bool teleportApplied = false,
        string? dialogueSummary = null,
        string? questSummary = null,
        DialogueStatePushWire? dialogueState = null,
        bool questsChanged = false,
        bool professionsChanged = false,
        bool recipesChanged = false,
        IReadOnlyList<WorldSwitchWire>? switchChanges = null,
        Guid? openShopId = null,
        bool weatherChanged = false,
        IReadOnlyList<MapEventPictureOp>? pictureOps = null,
        IReadOnlyList<MapEventScreenOp>? screenOps = null,
        IReadOnlyList<MapEventVisualOp>? visualOps = null) =>
        new()
        {
            Success = true,
            Message = message,
            ShowText = showText,
            SwitchesChanged = switchesChanged,
            SwitchChanges = switchChanges is { Count: > 0 } listed
                ? listed
                : Array.Empty<WorldSwitchWire>(),
            VariablesChanged = variablesChanged,
            InventoryChanged = inventoryChanged,
            GoldChanged = goldChanged,
            QuestsChanged = questsChanged,
            ProfessionsChanged = professionsChanged,
            RecipesChanged = recipesChanged,
            TeleportApplied = teleportApplied,
            WeatherChanged = weatherChanged,
            DialogueSummary = dialogueSummary,
            QuestSummary = questSummary,
            DialogueState = dialogueState,
            OpenShopId = openShopId is Guid shopId && shopId != Guid.Empty ? shopId : null,
            PictureOps = pictureOps is { Count: > 0 } pictures
                ? pictures
                : Array.Empty<MapEventPictureOp>(),
            ScreenOps = screenOps is { Count: > 0 } screens
                ? screens
                : Array.Empty<MapEventScreenOp>(),
            VisualOps = visualOps is { Count: > 0 } visuals
                ? visuals
                : Array.Empty<MapEventVisualOp>(),
        };

    /// <summary>
    /// Mappe le snapshot persisté (J4-PG) vers le résultat public. Les intents
    /// <c>teleport</c> / <c>start_dialogue</c> sont enregistrés dans la TX ; le
    /// serveur les applique sur la session après commit puis passe l'état appliqué.
    /// </summary>
    public static MapEventExecutionResult FromMutationSnapshot(
        string fallbackMessage,
        MapEventExecutionSnapshot? snap,
        bool teleportApplied = false,
        string? dialogueSummary = null,
        DialogueStatePushWire? dialogueState = null,
        Guid? openShopId = null,
        string? sessionNote = null,
        bool weatherChanged = false) =>
        Ok(
            message: snap?.ShowText ?? dialogueSummary ?? sessionNote ?? fallbackMessage,
            showText: snap?.ShowText ?? dialogueSummary ?? sessionNote,
            switchesChanged: snap?.SwitchesChanged ?? false,
            switchChanges: snap?.SwitchChanges,
            variablesChanged: snap?.VariablesChanged ?? false,
            inventoryChanged: snap?.InventoryChanged ?? false,
            goldChanged: snap?.GoldChanged ?? false,
            teleportApplied: teleportApplied,
            weatherChanged: weatherChanged,
            dialogueSummary: dialogueSummary,
            questSummary: snap?.QuestSummary,
            dialogueState: dialogueState,
            questsChanged: snap?.QuestsChanged ?? false,
            professionsChanged: snap?.ProfessionsChanged ?? false,
            recipesChanged: snap?.RecipesChanged ?? false,
            openShopId: openShopId,
            pictureOps: snap?.PictureOps,
            screenOps: snap?.ScreenOps,
            visualOps: snap?.ExpandVisuals());

    public static MapEventExecutionResult Fail(string message) =>
        new() { Success = false, Message = message };
}
