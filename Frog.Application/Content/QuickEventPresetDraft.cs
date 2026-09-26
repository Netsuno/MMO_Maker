using System.Globalization;
using System.Text;
using System.Text.Json;
using Frog.Core.Character;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Application.Content;

/// <summary>Raccourcis coffre, porte et auberge (pages, commandes #86, collision).</summary>
public enum QuickEventPresetKind
{
    Chest = 0,
    Door = 1,
    Inn = 2,
}

/// <summary>Textes français posés par les presets. L'auteur les remplace dans l'éditeur de pages.</summary>
public static class QuickEventPresetTexts
{
    public const string ChestOpen = "Vous ouvrez le coffre.";
    public const string ChestEmpty = "Le coffre est vide.";
    public const string DoorOpen = "Vous ouvrez la porte.";
    public const string DoorAlreadyOpen = "La porte est ouverte.";
    public const string InnWelcome = "Bienvenue à l'auberge. Vous vous reposez.";
    public const string InnReturn = "Bon retour. Une nuit de plus.";
    public const string InnRegular = "Vous êtes un habitué. La chambre est prête.";

    /// <summary>À partir de ce nombre de nuits, la page de retour prend la branche habitué.</summary>
    public const int InnRegularNights = 3;
}

/// <summary>
/// Événement catalogue par défaut : deux pages Action, case bloquante.
/// La collision publiée suit la page de plus haute priorité
/// (<c>MapEventPersistenceMapper.ToWireEntry</c>) : les deux pages bloquent,
/// donc le coffre, la porte et l'aubergiste restent solides.
/// </summary>
public sealed class QuickEventPresetDraft
{
    public const int MaxSlugAttempts = 20;

    public required QuickEventPresetKind Kind { get; init; }

    public required string DisplayName { get; init; }

    public required string Slug { get; init; }

    public required string TriggerKind { get; init; }

    public required string SwitchKey { get; init; }

    public string? CounterKey { get; init; }

    public required IReadOnlyList<MapEventPageDefinition> Pages { get; init; }

    public static string DefaultName(QuickEventPresetKind kind) => kind switch
    {
        QuickEventPresetKind.Chest => "Coffre",
        QuickEventPresetKind.Door => "Porte",
        QuickEventPresetKind.Inn => "Auberge",
        _ => "Événement",
    };

    public static string Describe(QuickEventPresetKind kind) => kind switch
    {
        QuickEventPresetKind.Chest =>
            "Case bloquante, action. Première page : « Vous ouvrez le coffre. », interrupteur d'ouverture et variable de butin +1. Page suivante (interrupteur vrai) : « Le coffre est vide. »",
        QuickEventPresetKind.Door =>
            "Case bloquante, action. Première page : « Vous ouvrez la porte. » et interrupteur. Page suivante : « La porte est ouverte. » La case reste bloquante ; une téléportation s'ajoute dans les pages.",
        QuickEventPresetKind.Inn =>
            "Aubergiste bloquant, action. Première page : repos, interrupteur et variable de nuits à 1. Page suivante : branche — à partir de 3 nuits, texte d'habitué, sinon « Bon retour » et nuits +1.",
        _ => "Événement rapide.",
    };

    public static bool TryCreate(string? name, QuickEventPresetKind kind, out QuickEventPresetDraft? draft, out string? error)
    {
        draft = null;
        if (!Enum.IsDefined(kind))
        {
            error = "Type d'événement inconnu.";
            return false;
        }

        var displayName = MapEventCatalogNormalization.TryNormalizeDisplayName(name);
        if (displayName is null)
        {
            error = "Nom requis.";
            return false;
        }

        if (displayName.Length > 128)
        {
            error = "Le nom ne doit pas dépasser 128 caractères.";
            return false;
        }

        var slug = BuildSlug(kind, displayName);
        var pages = BuildPages(kind, slug);
        var definition = new MapEventDefinition
        {
            Name = displayName,
            CatalogSlug = slug,
            Pages = pages,
        };
        if (!definition.Validate(out error))
        {
            error = error is null ? "Événement invalide." : "Événement : " + error;
            return false;
        }

        draft = new QuickEventPresetDraft
        {
            Kind = kind,
            DisplayName = displayName,
            Slug = slug,
            TriggerKind = Phase8MapEventTriggerKinds.Action,
            SwitchKey = StateKey(slug, SwitchSuffix(kind)),
            CounterKey = CounterSuffix(kind) is { } counter ? StateKey(slug, counter) : null,
            Pages = pages,
        };
        error = null;
        return true;
    }

    public static IReadOnlyList<MapEventPageDefinition> BuildPages(QuickEventPresetKind kind, string slug)
    {
        var switchKey = StateKey(slug, SwitchSuffix(kind));
        return kind switch
        {
            QuickEventPresetKind.Chest => ChestPages(switchKey, StateKey(slug, CounterSuffix(kind)!)),
            QuickEventPresetKind.Door => DoorPages(switchKey),
            QuickEventPresetKind.Inn => InnPages(switchKey, StateKey(slug, CounterSuffix(kind)!)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    public static string BuildSlug(QuickEventPresetKind kind, string displayName)
    {
        var prefix = Prefix(kind);
        var folded = FoldAccents(displayName);
        var normalized = MapEventCatalogNormalization.TryNormalizeSlug(folded);
        var body = string.IsNullOrEmpty(normalized) ? "evt" : normalized;
        var slug = body.StartsWith(prefix, StringComparison.Ordinal) ? body : prefix + body;
        if (slug.Length > MapEventCatalogNormalization.MaxSlugLength)
        {
            slug = slug[..MapEventCatalogNormalization.MaxSlugLength].TrimEnd('_');
        }

        if (slug.Length == 0)
        {
            slug = prefix.TrimEnd('_');
        }

        return slug;
    }

    /// <summary>Ajoute <c>_2</c>, <c>_3</c>… en restant dans la longueur max du slug catalogue.</summary>
    public static string WithAttemptSuffix(string slug, int attempt)
    {
        if (attempt <= 1)
        {
            return slug;
        }

        var suffix = "_" + attempt.ToString(CultureInfo.InvariantCulture);
        var room = MapEventCatalogNormalization.MaxSlugLength - suffix.Length;
        var trimmed = slug.Length <= room ? slug : slug[..Math.Max(1, room)].TrimEnd('_');
        if (trimmed.Length == 0)
        {
            trimmed = "evt";
        }

        return trimmed + suffix;
    }

    /// <summary>Clé interrupteur ou variable, au plus <see cref="CharacterPayloadWorldFlags.MaxKeyUtf8Bytes"/> octets.</summary>
    public static string StateKey(string slug, string suffix)
    {
        var extra = "_" + suffix;
        var room = CharacterPayloadWorldFlags.MaxKeyUtf8Bytes - Encoding.UTF8.GetByteCount(extra);
        var source = slug ?? string.Empty;
        if (Encoding.UTF8.GetByteCount(source) > room)
        {
            source = source[..Math.Max(0, room)];
        }

        source = source.TrimEnd('_');
        if (source.Length == 0)
        {
            source = "evt";
        }

        return source + extra;
    }

    public static string SwitchSuffix(QuickEventPresetKind kind) => kind switch
    {
        QuickEventPresetKind.Chest => "ouvert",
        QuickEventPresetKind.Door => "ouverte",
        QuickEventPresetKind.Inn => "repose",
        _ => "etat",
    };

    public static string? CounterSuffix(QuickEventPresetKind kind) => kind switch
    {
        QuickEventPresetKind.Chest => "butin",
        QuickEventPresetKind.Inn => "nuits",
        _ => null,
    };

    private static string Prefix(QuickEventPresetKind kind) => kind switch
    {
        QuickEventPresetKind.Chest => "coffre_",
        QuickEventPresetKind.Door => "porte_",
        QuickEventPresetKind.Inn => "auberge_",
        _ => "evt_",
    };

    private static IReadOnlyList<MapEventPageDefinition> ChestPages(string switchKey, string lootKey) =>
    [
        Page(0, 0, blocks: true, conditions: [], commands:
        [
            Show(QuickEventPresetTexts.ChestOpen),
            SetSwitch(switchKey, true),
            AddVariable(lootKey, 1),
        ]),
        Page(1, 1, blocks: true, conditions: [SwitchIs(switchKey, true)], commands:
        [
            Show(QuickEventPresetTexts.ChestEmpty),
        ]),
    ];

    private static IReadOnlyList<MapEventPageDefinition> DoorPages(string switchKey) =>
    [
        Page(0, 0, blocks: true, conditions: [], commands:
        [
            Show(QuickEventPresetTexts.DoorOpen),
            SetSwitch(switchKey, true),
        ]),
        Page(1, 1, blocks: true, conditions: [SwitchIs(switchKey, true)], commands:
        [
            Show(QuickEventPresetTexts.DoorAlreadyOpen),
        ]),
    ];

    private static IReadOnlyList<MapEventPageDefinition> InnPages(string switchKey, string nightsKey)
    {
        var regular = new MapEventConditionDefinition
        {
            Kind = MapEventConditionKinds.CharacterVariableCompare,
            ParameterJson = JsonSerializer.Serialize(new
            {
                variableId = nightsKey,
                op = "gte",
                value = QuickEventPresetTexts.InnRegularNights,
            }),
        };
        return
        [
            Page(0, 0, blocks: true, conditions: [], commands:
            [
                Show(QuickEventPresetTexts.InnWelcome),
                SetSwitch(switchKey, true),
                SetVariable(nightsKey, 1),
            ]),
            Page(1, 1, blocks: true, conditions: [SwitchIs(switchKey, true)], commands:
            [
                Branch(regular, [Show(QuickEventPresetTexts.InnRegular)], [Show(QuickEventPresetTexts.InnReturn)]),
                AddVariable(nightsKey, 1),
            ]),
        ];
    }

    private static MapEventPageDefinition Page(
        int order,
        int priority,
        bool blocks,
        IReadOnlyList<MapEventConditionDefinition> conditions,
        IReadOnlyList<MapEventCommandDefinition> commands) => new()
    {
        PageOrder = order,
        Priority = priority,
        TriggerKind = Phase8MapEventTriggerKinds.Action,
        MovementKind = MapEventMovementKinds.Fixed,
        BlocksCollision = blocks,
        Conditions = conditions,
        Commands = commands,
    };

    private static MapEventConditionDefinition SwitchIs(string switchKey, bool value) => new()
    {
        Kind = MapEventConditionKinds.CharacterSwitch,
        ParameterJson = JsonSerializer.Serialize(new { switchId = switchKey, value }),
    };

    private static MapEventCommandDefinition Show(string text) =>
        Cmd(
            MapEventCommandDiscriminators.ShowText,
            JsonSerializer.Serialize(new Dictionary<string, string> { ["text"] = text }));

    private static MapEventCommandDefinition SetSwitch(string switchKey, bool value) =>
        Cmd(MapEventCommandDiscriminators.SetSwitch, JsonSerializer.Serialize(new { switchId = switchKey, value }));

    private static MapEventCommandDefinition SetVariable(string variableKey, int value) =>
        Cmd(MapEventCommandDiscriminators.SetVariable, JsonSerializer.Serialize(new { variableId = variableKey, value }));

    private static MapEventCommandDefinition AddVariable(string variableKey, int delta) =>
        Cmd(MapEventCommandDiscriminators.AddVariable, JsonSerializer.Serialize(new { variableId = variableKey, delta }));

    private static MapEventCommandDefinition Branch(
        MapEventConditionDefinition condition,
        IReadOnlyList<MapEventCommandDefinition> thenCommands,
        IReadOnlyList<MapEventCommandDefinition> elseCommands) =>
        Cmd(
            MapEventCommandDiscriminators.Branch,
            JsonSerializer.Serialize(new
            {
                conditionKind = condition.Kind,
                conditionParameterJson = condition.ParameterJson,
                thenCommands = Project(thenCommands),
                elseCommands = Project(elseCommands),
            }));

    private static object[] Project(IReadOnlyList<MapEventCommandDefinition> commands) =>
        commands.Select(command => new { discriminator = command.Discriminator, parameterJson = command.ParameterJson }).ToArray();

    private static MapEventCommandDefinition Cmd(string discriminator, string parameterJson) => new()
    {
        Discriminator = discriminator,
        SchemaVersion = 1,
        ParameterJson = parameterJson,
    };

    private static string FoldAccents(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
