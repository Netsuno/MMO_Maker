using System.Text.Json;
using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// Libellés français de l’éditeur de pages. Les valeurs stockées (kinds, discriminators, JSON) ne changent pas.
/// </summary>
public static class MapEventEditorLabels
{
    public static string Trigger(string? kind) => kind switch
    {
        Phase8MapEventTriggerKinds.Action => "Action",
        Phase8MapEventTriggerKinds.PlayerContact => "Contact joueur",
        Phase8MapEventTriggerKinds.Autorun => "Automatique",
        Phase8MapEventTriggerKinds.Parallel => "Parallèle",
        _ => string.IsNullOrWhiteSpace(kind) ? "Déclencheur" : kind.Trim(),
    };

    public static string Movement(string? kind) => kind switch
    {
        MapEventMovementKinds.Fixed => "Fixe",
        MapEventMovementKinds.Route => "Trajet",
        _ => string.IsNullOrWhiteSpace(kind) ? "Fixe" : kind.Trim(),
    };

    public static string ConditionKind(string? kind) => kind switch
    {
        MapEventConditionKinds.CharacterSwitch => "Interrupteur",
        MapEventConditionKinds.CharacterVariableCompare => "Variable",
        MapEventConditionKinds.QuestStatus => "Quête",
        MapEventConditionKinds.ItemQuantity => "Objet",
        MapEventConditionKinds.CharacterLevel => "Niveau",
        MapEventConditionKinds.ProfessionLevel => "Métier",
        MapEventConditionKinds.MapOrRegion => "Carte ou région",
        _ => string.IsNullOrWhiteSpace(kind) ? "Condition" : kind.Trim(),
    };

    public static string CommandKind(string? discriminator) => discriminator switch
    {
        MapEventCommandDiscriminators.ShowText => "Texte",
        MapEventCommandDiscriminators.ShowChoices => "Afficher choix",
        MapEventCommandDiscriminators.PlayBgm => "Jouer BGM",
        MapEventCommandDiscriminators.PlaySe => "Jouer SE",
        MapEventCommandDiscriminators.StartDialogue => "Dialogue",
        MapEventCommandDiscriminators.Branch => "Branche",
        MapEventCommandDiscriminators.SetSwitch => "Régler interrupteur",
        MapEventCommandDiscriminators.SetVariable => "Régler variable",
        MapEventCommandDiscriminators.AddVariable => "Ajouter à la variable",
        MapEventCommandDiscriminators.SubVariable => "Retirer de la variable",
        MapEventCommandDiscriminators.GiveItem => "Donner objet",
        MapEventCommandDiscriminators.TakeItem => "Retirer objet",
        MapEventCommandDiscriminators.GiveGold => "Donner or",
        MapEventCommandDiscriminators.TakeGold => "Retirer or",
        MapEventCommandDiscriminators.StartQuest => "Démarrer quête",
        MapEventCommandDiscriminators.AdvanceQuest => "Avancer quête",
        MapEventCommandDiscriminators.TurnInQuest => "Rendre quête",
        MapEventCommandDiscriminators.Teleport => "Téléportation",
        MapEventCommandDiscriminators.Wait => "Attendre",
        MapEventCommandDiscriminators.CallCommonEvent => "Événement commun",
        MapEventCommandDiscriminators.LearnProfession => "Apprendre métier",
        _ => string.IsNullOrWhiteSpace(discriminator) ? "Commande" : discriminator.Trim(),
    };

    public static string Field(string? key) => key switch
    {
        "text" => "Texte",
        "choices" => "Choix",
        "cancel" => "Annulation",
        "asset" => "Fichier",
        "volume" => "Volume",
        "fadeMs" => "Fondu (ms)",
        "switchId" => "Interrupteur",
        "value" => "Valeur",
        "variableId" => "Variable",
        "delta" => "Delta",
        "itemId" => "Objet",
        "quantity" => "Quantité",
        "onceKey" => "Clé une fois",
        "amount" => "Montant",
        "dialogueId" => "Dialogue",
        "questId" => "Quête",
        "stageIndex" => "Étape",
        "mapId" => "Carte",
        "tileX" => "Tuile X",
        "tileY" => "Tuile Y",
        "milliseconds" => "Durée (ms)",
        "commonEventId" => "Événement commun",
        "editorAliasId" => "Alias éditeur",
        "professionId" => "Métier",
        "condition" => "Si",
        "thenCommands" => "Alors",
        "elseCommands" => "Sinon",
        "parameterJson" => "Paramètres",
        "minLevel" => "Niveau min.",
        "regionId" => "Région",
        "op" => "Comparaison",
        "status" => "Statut",
        _ => string.IsNullOrWhiteSpace(key) ? "" : key,
    };

    public static string CancelType(string? cancel) => cancel switch
    {
        MapEventShowChoices.CancelDisallow => "Interdit",
        MapEventShowChoices.CancelChoice1 => "Choix 1",
        MapEventShowChoices.CancelChoice2 => "Choix 2",
        MapEventShowChoices.CancelChoice3 => "Choix 3",
        MapEventShowChoices.CancelChoice4 => "Choix 4",
        MapEventShowChoices.CancelBranch => "Branche",
        _ => string.IsNullOrWhiteSpace(cancel) ? "Annulation" : cancel.Trim(),
    };

    public static string CompareOp(string? op) => op switch
    {
        "eq" => "= égal",
        "ne" => "≠ différent",
        "lt" => "< inférieur",
        "lte" => "≤ inférieur ou égal",
        "gt" => "> supérieur",
        "gte" => "≥ supérieur ou égal",
        _ => string.IsNullOrWhiteSpace(op) ? "" : op,
    };

    public static string QuestStatus(string? status) => status switch
    {
        "not_started" => "pas commencée",
        "active" => "en cours",
        "ready" => "prête à rendre",
        "completed" => "terminée",
        _ => string.IsNullOrWhiteSpace(status) ? "" : status,
    };

    public static string PageListLine(int index, MapEventPageDefinition page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var n = index + 1;
        return $"Page {n}  ·  {Trigger(page.TriggerKind)}  ·  priorité {page.Priority}  ·  {CountPhrase(page.Conditions.Count, "condition", "conditions")}  ·  {CountPhrase(page.Commands.Count, "commande", "commandes")}";
    }

    public static string ActivePageCaption(int index, int pageCount, MapEventPageDefinition? page)
    {
        if (page is null || index < 0 || pageCount <= 0)
        {
            return "Aucune page active. Ajoutez une page pour régler le déclencheur, les conditions et les commandes.";
        }

        return $"Page active : {index + 1} sur {pageCount} — {Trigger(page.TriggerKind)} — priorité {page.Priority} — {CountPhrase(page.Conditions.Count, "condition", "conditions")}, {CountPhrase(page.Commands.Count, "commande", "commandes")}";
    }

    public static string ConditionListLine(int index, string? kind, string? parameterJson) =>
        $"{index + 1}. {ConditionSummary(kind, parameterJson)}";

    public static string CommandListLine(int index, string? discriminator, string? parameterJson) =>
        $"{index + 1}. {CommandSummary(discriminator, parameterJson)}";

    public static string ConditionSummary(string? kind, string? parameterJson)
    {
        var title = ConditionKind(kind);
        if (!TryOpen(parameterJson, out var root))
        {
            return title;
        }

        return kind switch
        {
            MapEventConditionKinds.CharacterSwitch =>
                $"Interrupteur « {Clip(ReadString(root, "switchId"), 24)} » est {(ReadBool(root, "value") ? "oui" : "non")}",
            MapEventConditionKinds.CharacterVariableCompare =>
                $"Variable « {Clip(ReadString(root, "variableId"), 24)} » {CompareOp(ReadString(root, "op")).Split(' ')[0]} {ReadInt(root, "value")}",
            MapEventConditionKinds.QuestStatus =>
                $"Quête {ShortId(ReadString(root, "questId"))} {QuestStatus(ReadString(root, "status"))}".Trim(),
            MapEventConditionKinds.ItemQuantity =>
                $"Objet {ShortId(ReadString(root, "itemId"))} × {ReadInt(root, "quantity")}",
            MapEventConditionKinds.CharacterLevel =>
                $"Niveau ≥ {ReadInt(root, "minLevel")}",
            MapEventConditionKinds.ProfessionLevel =>
                $"Métier {ShortId(ReadString(root, "professionId"))} niveau ≥ {ReadInt(root, "minLevel")}",
            MapEventConditionKinds.MapOrRegion => SummarizeMapOrRegion(root),
            _ => title,
        };
    }

    public static string CommandSummary(string? discriminator, string? parameterJson)
    {
        var title = CommandKind(discriminator);
        if (!TryOpen(parameterJson, out var root))
        {
            return title;
        }

        return discriminator switch
        {
            MapEventCommandDiscriminators.ShowText =>
                $"Texte : {Clip(ReadString(root, "text"), 42)}",
            MapEventCommandDiscriminators.ShowChoices => SummarizeChoices(root),
            MapEventCommandDiscriminators.PlayBgm => SummarizeAudio("Jouer BGM", root),
            MapEventCommandDiscriminators.PlaySe => SummarizeAudio("Jouer SE", root),
            MapEventCommandDiscriminators.SetSwitch =>
                $"Interrupteur « {Clip(ReadString(root, "switchId"), 24)} » ← {(ReadBool(root, "value") ? "oui" : "non")}",
            MapEventCommandDiscriminators.SetVariable =>
                $"Variable « {Clip(ReadString(root, "variableId"), 24)} » = {ReadInt(root, "value")}",
            MapEventCommandDiscriminators.AddVariable =>
                $"Variable « {Clip(ReadString(root, "variableId"), 24)} » + {ReadInt(root, "delta")}",
            MapEventCommandDiscriminators.SubVariable =>
                $"Variable « {Clip(ReadString(root, "variableId"), 24)} » − {ReadInt(root, "delta")}",
            MapEventCommandDiscriminators.GiveItem =>
                $"Donner {ShortId(ReadString(root, "itemId"))} × {ReadInt(root, "quantity")}",
            MapEventCommandDiscriminators.TakeItem =>
                $"Retirer {ShortId(ReadString(root, "itemId"))} × {ReadInt(root, "quantity")}",
            MapEventCommandDiscriminators.GiveGold =>
                $"Donner {ReadInt(root, "amount")} or",
            MapEventCommandDiscriminators.TakeGold =>
                $"Retirer {ReadInt(root, "amount")} or",
            MapEventCommandDiscriminators.StartDialogue =>
                $"Dialogue {ShortId(ReadString(root, "dialogueId"))}",
            MapEventCommandDiscriminators.StartQuest =>
                $"Démarrer quête {ShortId(ReadString(root, "questId"))}",
            MapEventCommandDiscriminators.TurnInQuest =>
                $"Rendre quête {ShortId(ReadString(root, "questId"))}",
            MapEventCommandDiscriminators.AdvanceQuest =>
                $"Avancer quête {ShortId(ReadString(root, "questId"))}, étape {ReadInt(root, "stageIndex")}",
            MapEventCommandDiscriminators.Teleport =>
                $"Téléportation carte {ReadInt(root, "mapId")} ({ReadInt(root, "tileX")}, {ReadInt(root, "tileY")})",
            MapEventCommandDiscriminators.Wait =>
                $"Attendre {ReadInt(root, "milliseconds")} ms",
            MapEventCommandDiscriminators.CallCommonEvent => SummarizeCommonEvent(root),
            MapEventCommandDiscriminators.LearnProfession =>
                $"Apprendre métier {ShortId(ReadString(root, "professionId"))}",
            MapEventCommandDiscriminators.Branch => SummarizeBranch(root),
            _ => title,
        };
    }

    private static string SummarizeChoices(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return "Afficher choix";
        }

        var labels = new List<string>();
        foreach (var item in choices.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                labels.Add(Clip(item.GetString(), 16));
            }
        }

        return labels.Count == 0
            ? "Afficher choix"
            : "Afficher choix : " + string.Join(" / ", labels);
    }

    private static string SummarizeAudio(string title, JsonElement root)
    {
        var asset = ReadString(root, "asset");
        var file = asset;
        var slash = asset.LastIndexOf('/');
        if (slash >= 0 && slash < asset.Length - 1)
        {
            file = asset[(slash + 1)..];
        }

        if (string.IsNullOrWhiteSpace(file))
        {
            return title;
        }

        return $"{title} : {Clip(file, 28)} · vol. {ReadInt(root, "volume")}";
    }

    private static string SummarizeBranch(JsonElement root)
    {
        var kind = ReadString(root, "conditionKind");
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "Branche si / alors / sinon";
        }

        var param = ReadString(root, "conditionParameterJson");
        return "Branche — " + ConditionSummary(kind, param);
    }

    private static string SummarizeMapOrRegion(JsonElement root)
    {
        var region = ReadString(root, "regionId");
        var mapId = ReadInt(root, "mapId");
        if (string.IsNullOrWhiteSpace(region))
        {
            return $"Carte {mapId}";
        }

        return mapId > 0
            ? $"Carte {mapId} ou région {Clip(region, 24)}"
            : $"Région {Clip(region, 24)}";
    }

    private static string SummarizeCommonEvent(JsonElement root)
    {
        var id = ReadString(root, "commonEventId");
        if (!string.IsNullOrWhiteSpace(id))
        {
            return $"Événement commun {ShortId(id)}";
        }

        var alias = ReadInt(root, "editorAliasId");
        return alias > 0 ? $"Événement commun #{alias}" : "Événement commun";
    }

    private static string CountPhrase(int count, string singular, string plural)
    {
        if (count <= 0)
        {
            return $"aucune {singular}";
        }

        return count == 1 ? $"1 {singular}" : $"{count} {plural}";
    }

    private static bool TryOpen(string? json, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            root = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty,
        };
    }

    private static int ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
        {
            return n;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)
            ? parsed
            : 0;
    }

    private static bool ReadBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => false,
        };
    }

    private static string ShortId(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return "—";
        }

        if (Guid.TryParse(text, out _))
        {
            return text[..8] + "…";
        }

        return Clip(text, 24);
    }

    private static string Clip(string? text, int max)
    {
        var trimmed = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (trimmed.Length <= max)
        {
            return trimmed.Length == 0 ? "…" : trimmed;
        }

        return trimmed[..(max - 1)] + "…";
    }
}
