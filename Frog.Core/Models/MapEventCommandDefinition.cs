namespace Frog.Core.Models;

/// <summary>Commande typée data-driven (payload JSON validé côté Core).</summary>
public sealed class MapEventCommandDefinition
{
    public string Discriminator { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    public string ParameterJson { get; set; } = "{}";

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Discriminator))
        {
            error = "Discriminator de commande requis.";
            return false;
        }

        if (SchemaVersion < 1)
        {
            error = "SchemaVersion doit être >= 1.";
            return false;
        }

        if (ParameterJson.Length > MapEventRuntimeLimits.MaxCommandParameterBytes)
        {
            error = "Paramètres de commande trop volumineux.";
            return false;
        }

        if (System.Text.Encoding.UTF8.GetByteCount(ParameterJson) > MapEventRuntimeLimits.MaxCommandParameterBytes)
        {
            error = "Paramètres de commande trop volumineux (octets UTF-8).";
            return false;
        }

        if (!MapEventCommandDiscriminators.IsKnown(Discriminator))
        {
            error = $"Commande inconnue: {Discriminator}.";
            return false;
        }

        error = null;
        return true;
    }
}

public static class MapEventCommandDiscriminators
{
    public const string ShowText = "show_text";
    public const string StartDialogue = "start_dialogue";
    public const string Branch = "branch";
    public const string ShowChoices = "show_choices";
    public const string PlayBgm = "play_bgm";
    public const string PlaySe = "play_se";
    public const string SetSwitch = "set_switch";
    public const string SetVariable = "set_variable";
    public const string AddVariable = "add_variable";
    public const string SubVariable = "sub_variable";
    public const string GiveItem = "give_item";
    public const string TakeItem = "take_item";
    public const string GiveGold = "give_gold";
    public const string TakeGold = "take_gold";
    public const string ChangeGold = "change_gold";
    public const string ChangeItems = "change_items";
    public const string StartQuest = "start_quest";
    public const string AdvanceQuest = "advance_quest";
    public const string TurnInQuest = "turn_in_quest";
    public const string Teleport = "teleport";
    public const string Wait = "wait";
    public const string CallCommonEvent = "call_common_event";
    public const string LearnProfession = "learn_profession";
    public const string OpenShop = "open_shop";
    public const string SetWeather = "set_weather";
    public const string ShowPicture = "show_picture";
    public const string MovePicture = "move_picture";
    public const string TintPicture = "tint_picture";
    public const string ErasePicture = "erase_picture";
    public const string FadeOutScreen = "fadeout_screen";
    public const string FadeInScreen = "fadein_screen";
    public const string TintScreen = "tint_screen";
    public const string ShakeScreen = "shake_screen";
    public const string FlashScreen = "flash_screen";
    public const string ShowAnimation = "show_animation";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        ShowText, StartDialogue, Branch, ShowChoices, PlayBgm, PlaySe,
        SetSwitch, SetVariable, AddVariable, SubVariable,
        GiveItem, TakeItem, GiveGold, TakeGold, ChangeGold, ChangeItems, StartQuest, AdvanceQuest, TurnInQuest,
        Teleport, Wait, CallCommonEvent, LearnProfession, OpenShop, SetWeather,
        ShowPicture, MovePicture, TintPicture, ErasePicture, FadeOutScreen, FadeInScreen, TintScreen,
        ShakeScreen, FlashScreen, ShowAnimation,
    };

    public static bool IsKnown(string discriminator) => All.Contains(discriminator);
}
