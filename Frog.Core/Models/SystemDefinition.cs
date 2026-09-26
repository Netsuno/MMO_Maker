namespace Frog.Core.Models;

/// <summary>
/// Document unique « Système » (Netsun) : unité monétaire, groupe de départ,
/// carte de départ, musiques optionnelles et termes courts. Pas un champ de protocole.
/// Le catalogue interrupteurs / variables reste <see cref="SystemFlagDefinition"/>.
/// </summary>
public sealed class SystemDefinition
{
    public const int MaxTermLength = 32;
    public const int MaxPartySize = 4;
    public const string DefaultCurrencyUnit = "Or";
    public const string DefaultTermHp = "HP";
    public const string DefaultTermMp = "MP";

    /// <summary>Une seule ligne projet. La migration reprend cet identifiant.</summary>
    public static readonly Guid SingletonId = new("8f3c2a91-6d14-4e7b-a0c5-1b9e4d7f2a60");

    public Guid Id { get; set; } = SingletonId;

    /// <summary>Unité monétaire affichée. Défaut « Or ». Sert aussi de terme Or.</summary>
    public string CurrencyUnit { get; set; } = DefaultCurrencyUnit;

    public string TermHp { get; set; } = DefaultTermHp;

    public string TermMp { get; set; } = DefaultTermMp;

    public Guid? PartyActor1 { get; set; }

    public Guid? PartyActor2 { get; set; }

    public Guid? PartyActor3 { get; set; }

    public Guid? PartyActor4 { get; set; }

    /// <summary>Carte de départ. Null = aucune. L’identifiant doit exister dans le dépôt de cartes.</summary>
    public Guid? StartMapId { get; set; }

    public MapAudioTrack TitleBgm { get; set; } = new();

    public MapAudioTrack StartBgm { get; set; } = new();

    public static SystemDefinition CreateDefault() => new()
    {
        Id = SingletonId,
    };

    public bool Validate(out string? error)
        => TryCanonicalize(this, out _, out error);

    public SystemDefinition Copy() => new()
    {
        Id = Id,
        CurrencyUnit = CurrencyUnit,
        TermHp = TermHp,
        TermMp = TermMp,
        PartyActor1 = PartyActor1,
        PartyActor2 = PartyActor2,
        PartyActor3 = PartyActor3,
        PartyActor4 = PartyActor4,
        StartMapId = StartMapId,
        TitleBgm = MapAudioTrack.CopyOf(TitleBgm),
        StartBgm = MapAudioTrack.CopyOf(StartBgm),
    };

    public IReadOnlyList<Guid?> PartySlots()
        => new[] { PartyActor1, PartyActor2, PartyActor3, PartyActor4 };

    /// <summary>
    /// Valide et canonise. Les pistes vides reprennent volume 100 et fondu 0.
    /// Les chemins audio suivent <see cref="MapAudioTrack.TryCreate"/>.
    /// </summary>
    public static bool TryCanonicalize(
        SystemDefinition? source,
        out SystemDefinition definition,
        out string? error)
    {
        definition = CreateDefault();
        if (source is null)
        {
            error = "Paramètres système manquants.";
            return false;
        }

        var id = source.Id == Guid.Empty ? SingletonId : source.Id;
        if (id != SingletonId)
        {
            error = "Le document Système est unique.";
            return false;
        }

        if (!TryTerm(source.CurrencyUnit, "Unité monétaire", out var currency, out error)
            || !TryTerm(source.TermHp, "Terme HP", out var termHp, out error)
            || !TryTerm(source.TermMp, "Terme MP", out var termMp, out error))
        {
            return false;
        }

        var slots = new[]
        {
            source.PartyActor1,
            source.PartyActor2,
            source.PartyActor3,
            source.PartyActor4,
        };
        if (slots.Length > MaxPartySize)
        {
            error = $"Le groupe de départ compte au plus {MaxPartySize} héros.";
            return false;
        }

        var seen = new HashSet<Guid>();
        foreach (var slot in slots)
        {
            if (slot is not Guid actorId)
            {
                continue;
            }

            if (actorId == Guid.Empty)
            {
                error = "Un héros du groupe de départ est invalide.";
                return false;
            }

            if (!seen.Add(actorId))
            {
                error = "Un héros ne peut apparaître qu’une fois dans le groupe de départ.";
                return false;
            }
        }

        if (source.StartMapId == Guid.Empty)
        {
            error = "L’identifiant de la carte de départ est invalide.";
            return false;
        }

        if (!TryTrack(source.TitleBgm, "Musique du titre", out var titleBgm, out error)
            || !TryTrack(source.StartBgm, "Musique de départ", out var startBgm, out error))
        {
            return false;
        }

        definition = new SystemDefinition
        {
            Id = SingletonId,
            CurrencyUnit = currency,
            TermHp = termHp,
            TermMp = termMp,
            PartyActor1 = slots[0],
            PartyActor2 = slots[1],
            PartyActor3 = slots[2],
            PartyActor4 = slots[3],
            StartMapId = source.StartMapId,
            TitleBgm = titleBgm,
            StartBgm = startBgm,
        };
        error = null;
        return true;
    }

    private static bool TryTerm(string? raw, string label, out string value, out string? error)
    {
        value = raw?.Trim() ?? string.Empty;
        if (value.Length is 0 or > MaxTermLength || value.Any(char.IsControl))
        {
            error = $"{label} invalide (1–{MaxTermLength} caractères).";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryTrack(
        MapAudioTrack? source,
        string label,
        out MapAudioTrack track,
        out string? error)
    {
        var volume = source?.Volume ?? MapAudioTrack.DefaultVolume;
        var fade = source?.FadeMs ?? MapAudioTrack.MinFadeMs;
        return MapAudioTrack.TryCreate(source?.Asset, volume, fade, label, out track, out error);
    }
}
