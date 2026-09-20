using Frog.Core.Enums;
using Frog.Core.Protocol;

namespace Frog.Core.Economy;

/// <summary>État client HdV / courrier / coffre — testable sans WinForms.</summary>
public sealed class ClientEconomyHub
{
    public EconomyHubSnapshotWire? Auction { get; private set; }

    public EconomyHubSnapshotWire? Mail { get; private set; }

    public EconomyHubSnapshotWire? GuildBank { get; private set; }

    public string StatusLine { get; private set; } = string.Empty;

    public void ApplySnapshot(EconomyHubSnapshotWire snapshot)
    {
        switch (snapshot.Kind)
        {
            case EconomyHubKind.Auction:
                Auction = snapshot;
                break;
            case EconomyHubKind.Mail:
                Mail = snapshot;
                break;
            case EconomyHubKind.GuildBank:
                GuildBank = snapshot;
                break;
        }

        var label = KindLabel(snapshot.Kind);
        StatusLine = snapshot.Entries.Count == 0
            ? $"{label} : liste vide."
            : $"{label} : {snapshot.Entries.Count} entrée(s).";
    }

    public void ApplyResult(EconomyHubResultWire result)
    {
        StatusLine = string.IsNullOrWhiteSpace(result.Message)
            ? KindLabel(result.Kind)
            : (result.Success ? result.Message : result.Message);
    }

    public IReadOnlyList<EconomyHubEntryWire> Entries(EconomyHubKind kind) => kind switch
    {
        EconomyHubKind.Auction => Auction?.Entries ?? Array.Empty<EconomyHubEntryWire>(),
        EconomyHubKind.Mail => Mail?.Entries ?? Array.Empty<EconomyHubEntryWire>(),
        EconomyHubKind.GuildBank => GuildBank?.Entries ?? Array.Empty<EconomyHubEntryWire>(),
        _ => Array.Empty<EconomyHubEntryWire>()
    };

    public string EmptyHint(EconomyHubKind kind)
    {
        if (Entries(kind).Count > 0)
        {
            return string.Empty;
        }

        return kind switch
        {
            EconomyHubKind.Auction => "Aucune enchère — hôtel des ventes vide (MVP).",
            EconomyHubKind.Mail => "Boîte de courrier vide.",
            EconomyHubKind.GuildBank => GuildBank is { SubjectId: var id } && id != Guid.Empty
                ? "Coffre de guilde : emplacements vides."
                : "Pas de guilde — coffre indisponible.",
            _ => "Liste vide."
        };
    }

    public static string KindLabel(EconomyHubKind kind) => kind switch
    {
        EconomyHubKind.Auction => "HdV",
        EconomyHubKind.Mail => "Courrier",
        EconomyHubKind.GuildBank => "Coffre",
        _ => "Économie"
    };

    public static string FormatEntry(EconomyHubKind kind, EconomyHubEntryWire entry)
    {
        var title = string.IsNullOrWhiteSpace(entry.Title) ? "(sans titre)" : entry.Title;
        return kind switch
        {
            EconomyHubKind.Auction => $"{title} ×{entry.Quantity} — {entry.PriceOrFlags} or",
            EconomyHubKind.Mail => (entry.PriceOrFlags != 0 ? "[non lu] " : "") + title
                                  + (entry.Quantity > 0 ? $" ({entry.Quantity} pièce(s))" : string.Empty),
            EconomyHubKind.GuildBank => entry.RelatedId == Guid.Empty || entry.Quantity <= 0
                ? $"Slot {entry.PriceOrFlags + 1} — vide"
                : $"Slot {entry.PriceOrFlags + 1} — {title} ×{entry.Quantity}",
            _ => title
        };
    }

    public static byte[] QueryExtra() => Array.Empty<byte>();

    public static bool IsPlaceholderListing(EconomyHubEntryWire entry)
        => entry.Quantity <= 0 && entry.RelatedId == Guid.Empty;
}
