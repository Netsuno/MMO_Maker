using Frog.Core.Constants;

namespace Frog.Core.Events;

/// <summary>
/// Commandes événement persistées que l’interpréteur accepte sans effet joueur.
/// Pas d’opcode nouveau : <see cref="FrogWireProtocol.Version"/> reste 11.
/// <list type="bullet">
/// <item>
/// <c>show_choices</c> — les pages restent dans le JSON de la commande.
/// Aucune branche n’est prise : <c>DialogueChoiceRequest</c> (opcodes 67/68) vise un
/// dialogue catalogue, pas une commande inline.
/// </item>
/// <item>
/// <c>play_bgm</c> / <c>play_se</c> — la piste (chemin, volume, fondu) est validée
/// comme une piste de carte. Le client ne joue que les cues UI / boucle stub,
/// pas un asset d’événement.
/// </item>
/// </list>
/// </summary>
public static class MapEventDeferredPresentation
{
    public static bool TryAcceptShowChoices(string parameterJson, out string? error) =>
        MapEventParameterSchemas.TryParseShowChoices(
            parameterJson,
            out _,
            out _,
            out _,
            out _,
            out error);

    public static bool TryAcceptPlayAudio(string discriminator, string parameterJson, out string? error) =>
        MapEventParameterSchemas.TryParsePlayAudio(parameterJson, discriminator, out _, out error);
}
