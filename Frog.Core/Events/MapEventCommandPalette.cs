using System.Text.Json;
using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// Palette : texte, choix, image, fondu, teinte, tremblement, flash, audio, boutique, interrupteur, variable, branche, météo.
/// Les discriminators et le JSON restent ceux du catalogue (Postgres inchangé).
/// </summary>
public static class MapEventCommandPalette
{
    public const string ShowTextId = "show_text";
    public const string ShowChoicesId = "show_choices";
    public const string PlayBgmId = "play_bgm";
    public const string PlaySeId = "play_se";
    public const string OpenShopId = "open_shop";

    /// <summary>
    /// Guid vide jusqu'au choix d'une boutique. La validation des paramètres le refuse.
    /// </summary>
    public static readonly Guid OpenShopPlaceholderId = Guid.Empty;
    public const string SetSwitchId = "set_switch";
    public const string SetVariableId = "set_variable";
    public const string AddVariableId = "add_variable";
    public const string SubVariableId = "sub_variable";
    public const string BranchSwitchId = "branch_switch";
    public const string BranchVariableId = "branch_variable";
    public const string SetWeatherId = "set_weather";
    public const string ShowPictureId = "show_picture";
    public const string ErasePictureId = "erase_picture";
    public const string FadeOutScreenId = "fadeout_screen";
    public const string FadeInScreenId = "fadein_screen";
    public const string TintScreenId = "tint_screen";
    public const string ShakeScreenId = "shake_screen";
    public const string FlashScreenId = "flash_screen";

    public const string DefaultSwitchId = "interrupteur_1";
    public const string DefaultVariableId = "variable_1";

    public static readonly IReadOnlyList<Entry> Entries = new Entry[]
    {
        new(ShowTextId, "Texte"),
        new(ShowChoicesId, "Afficher choix"),
        new(PlayBgmId, "Jouer BGM"),
        new(PlaySeId, "Jouer SE"),
        new(OpenShopId, "Ouvrir boutique"),
        new(SetSwitchId, "Interrupteur"),
        new(SetVariableId, "Variable ="),
        new(AddVariableId, "Variable +"),
        new(SubVariableId, "Variable −"),
        new(BranchSwitchId, "Si interrupteur"),
        new(BranchVariableId, "Si variable"),
        new(SetWeatherId, "Changer météo"),
        new(ShowPictureId, "Afficher image"),
        new(ErasePictureId, "Effacer image"),
        new(FadeOutScreenId, "Fondu en fermeture"),
        new(FadeInScreenId, "Fondu en ouverture"),
        new(TintScreenId, "Teinte écran"),
        new(ShakeScreenId, "Tremblement écran"),
        new(FlashScreenId, "Flash écran"),
    };

    public readonly record struct Entry(string Id, string Label);

    public static bool TryCreate(string? id, out MapEventCommandDefinition command)
    {
        command = new MapEventCommandDefinition();
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var created = id.Trim() switch
        {
            ShowTextId => Command(
                MapEventCommandDiscriminators.ShowText,
                new { text = "Bonjour." }),
            ShowChoicesId => Command(
                MapEventCommandDiscriminators.ShowChoices,
                new
                {
                    choices = new[] { "Oui", "Non" },
                    cancel = MapEventShowChoices.CancelDisallow,
                    branches = new[] { Array.Empty<object>(), Array.Empty<object>() },
                    cancelCommands = Array.Empty<object>(),
                }),
            PlayBgmId => Command(
                MapEventCommandDiscriminators.PlayBgm,
                new { asset = "Assets/Audio/music-loop.wav", volume = MapAudioTrack.DefaultVolume, fadeMs = 0 }),
            PlaySeId => Command(
                MapEventCommandDiscriminators.PlaySe,
                new { asset = "Assets/Audio/ui-click.wav", volume = MapAudioTrack.DefaultVolume, fadeMs = 0 }),
            OpenShopId => Command(
                MapEventCommandDiscriminators.OpenShop,
                new { shopId = OpenShopPlaceholderId.ToString("D") }),
            SetSwitchId => Command(
                MapEventCommandDiscriminators.SetSwitch,
                new { switchId = DefaultSwitchId, value = true }),
            SetVariableId => Command(
                MapEventCommandDiscriminators.SetVariable,
                new { variableId = DefaultVariableId, value = 0 }),
            AddVariableId => Command(
                MapEventCommandDiscriminators.AddVariable,
                new { variableId = DefaultVariableId, delta = 1 }),
            SubVariableId => Command(
                MapEventCommandDiscriminators.SubVariable,
                new { variableId = DefaultVariableId, delta = 1 }),
            BranchSwitchId => Command(
                MapEventCommandDiscriminators.Branch,
                BranchBody(
                    MapEventConditionKinds.CharacterSwitch,
                    JsonSerializer.Serialize(new { switchId = DefaultSwitchId, value = true }))),
            BranchVariableId => Command(
                MapEventCommandDiscriminators.Branch,
                BranchBody(
                    MapEventConditionKinds.CharacterVariableCompare,
                    JsonSerializer.Serialize(new { variableId = DefaultVariableId, op = "gte", value = 1 }))),
            SetWeatherId => Command(
                MapEventCommandDiscriminators.SetWeather,
                new { weatherKind = "clear" }),
            ShowPictureId => Command(
                MapEventCommandDiscriminators.ShowPicture,
                new
                {
                    pictureId = 1,
                    asset = MapEventPicture.DefaultAsset,
                    x = 0,
                    y = 0,
                    opacity = MapEventPicture.MaxOpacity,
                    blend = MapEventPicture.BlendNormal,
                }),
            ErasePictureId => Command(
                MapEventCommandDiscriminators.ErasePicture,
                new { pictureId = 1 }),
            FadeOutScreenId => Command(
                MapEventCommandDiscriminators.FadeOutScreen,
                new { durationMs = MapEventScreen.DefaultDurationMs }),
            FadeInScreenId => Command(
                MapEventCommandDiscriminators.FadeInScreen,
                new { durationMs = MapEventScreen.DefaultDurationMs }),
            TintScreenId => Command(
                MapEventCommandDiscriminators.TintScreen,
                new
                {
                    red = MapEventScreen.DefaultTintRed,
                    green = MapEventScreen.DefaultTintGreen,
                    blue = MapEventScreen.DefaultTintBlue,
                    opacity = MapEventScreen.DefaultTintOpacity,
                    durationMs = MapEventScreen.DefaultDurationMs,
                }),
            ShakeScreenId => Command(
                MapEventCommandDiscriminators.ShakeScreen,
                new
                {
                    power = MapEventScreen.DefaultPower,
                    speed = MapEventScreen.DefaultSpeed,
                    durationMs = MapEventScreen.DefaultDurationMs,
                }),
            FlashScreenId => Command(
                MapEventCommandDiscriminators.FlashScreen,
                new
                {
                    red = MapEventScreen.DefaultFlashRed,
                    green = MapEventScreen.DefaultFlashGreen,
                    blue = MapEventScreen.DefaultFlashBlue,
                    opacity = MapEventScreen.DefaultFlashOpacity,
                    durationMs = MapEventScreen.DefaultDurationMs,
                }),
            _ => null,
        };

        if (created is null)
        {
            return false;
        }

        command = created;
        return true;
    }

    private static object BranchBody(string conditionKind, string conditionParameterJson) =>
        new
        {
            conditionKind,
            conditionParameterJson,
            thenCommands = Array.Empty<object>(),
            elseCommands = Array.Empty<object>(),
        };

    private static MapEventCommandDefinition Command(string discriminator, object body) =>
        new()
        {
            Discriminator = discriminator,
            SchemaVersion = 1,
            ParameterJson = JsonSerializer.Serialize(body),
        };
}
