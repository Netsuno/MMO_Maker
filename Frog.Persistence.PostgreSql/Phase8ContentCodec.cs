using System.Text.Json;
using Frog.Core.Events;
using Frog.Core.Models;

namespace Frog.Persistence.PostgreSql;

internal static class Phase8ContentCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string SerializeDialogue(DialogueDefinition definition) =>
        JsonSerializer.Serialize(definition, JsonOptions);

    public static bool TryDeserializeDialogue(string json, out DialogueDefinition definition, out string? error)
    {
        definition = new DialogueDefinition();
        error = null;
        try
        {
            definition = JsonSerializer.Deserialize<DialogueDefinition>(json, JsonOptions) ?? new DialogueDefinition();
            return definition.Validate(out error);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string SerializeQuest(QuestDefinition definition) =>
        JsonSerializer.Serialize(definition, JsonOptions);

    public static bool TryDeserializeQuest(string json, out QuestDefinition definition, out string? error)
    {
        definition = new QuestDefinition();
        error = null;
        try
        {
            definition = JsonSerializer.Deserialize<QuestDefinition>(json, JsonOptions) ?? new QuestDefinition();
            return definition.Validate(out error);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string SerializeCommonEvent(CommonEventDefinition definition)
    {
        var clone = new CommonEventDefinition
        {
            Id = definition.Id,
            Name = definition.Name,
            EditorAliasId = definition.EditorAliasId,
            Pages = definition.Pages,
        };
        return JsonSerializer.Serialize(clone, JsonOptions);
    }

    public static bool TryDeserializeCommonEvent(string json, out CommonEventDefinition definition, out string? error)
    {
        definition = new CommonEventDefinition();
        error = null;
        try
        {
            definition = JsonSerializer.Deserialize<CommonEventDefinition>(json, JsonOptions) ?? new CommonEventDefinition();
            if (!MapEventPagesCodec.TryDeserializePages(
                    MapEventPagesCodec.SerializePages(definition.Pages),
                    out var pages,
                    out error))
            {
                return false;
            }

            definition.Pages = pages;
            return definition.Validate(out error);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string SerializeProfession(ProfessionDefinition definition) =>
        JsonSerializer.Serialize(definition, JsonOptions);

    public static bool TryDeserializeProfession(string json, out ProfessionDefinition definition, out string? error)
    {
        definition = new ProfessionDefinition();
        error = null;
        try
        {
            definition = JsonSerializer.Deserialize<ProfessionDefinition>(json, JsonOptions) ?? new ProfessionDefinition();
            return definition.Validate(out error);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string SerializeRecipe(RecipeDefinition definition) =>
        JsonSerializer.Serialize(definition, JsonOptions);

    public static bool TryDeserializeRecipe(string json, out RecipeDefinition definition, out string? error)
    {
        definition = new RecipeDefinition();
        error = null;
        try
        {
            definition = JsonSerializer.Deserialize<RecipeDefinition>(json, JsonOptions) ?? new RecipeDefinition();
            return definition.Validate(out error);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string SerializeRegion(RegionDefinition definition) =>
        JsonSerializer.Serialize(definition, JsonOptions);

    public static bool TryDeserializeRegion(string json, out RegionDefinition definition, out string? error)
    {
        definition = new RegionDefinition();
        error = null;
        try
        {
            definition = JsonSerializer.Deserialize<RegionDefinition>(json, JsonOptions) ?? new RegionDefinition();
            return definition.Validate(out error);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string SerializeWeather(WeatherProfileDefinition definition) =>
        JsonSerializer.Serialize(definition, JsonOptions);

    public static bool TryDeserializeWeather(string json, out WeatherProfileDefinition definition, out string? error)
    {
        definition = new WeatherProfileDefinition();
        error = null;
        try
        {
            definition = JsonSerializer.Deserialize<WeatherProfileDefinition>(json, JsonOptions) ?? new WeatherProfileDefinition();
            return definition.Validate(out error);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
