using Frog.Core.Enums;
using Frog.Core.Protocol;

namespace Frog.Core.Instances;

/// <summary>État client donjon / raid — testable sans WinForms.</summary>
public sealed class ClientInstanceHub
{
    public InstanceHubSnapshotWire? Dungeon { get; private set; }

    public InstanceHubSnapshotWire? Raid { get; private set; }

    public Guid CurrentInstanceId { get; private set; }

    public string CurrentInstanceName { get; private set; } = string.Empty;

    public string StatusLine { get; private set; } = string.Empty;

    public void ApplySnapshot(InstanceHubSnapshotWire snapshot)
    {
        switch (snapshot.Kind)
        {
            case InstanceHubKind.Dungeon:
                Dungeon = snapshot;
                break;
            case InstanceHubKind.Raid:
                Raid = snapshot;
                break;
        }

        if (snapshot.SubjectId != Guid.Empty)
        {
            CurrentInstanceId = snapshot.SubjectId;
            CurrentInstanceName = FindTitle(snapshot.SubjectId) ?? KindLabel(snapshot.Kind);
        }
        else if (CurrentInstanceId != Guid.Empty
                 && !Entries(InstanceHubKind.Dungeon).Concat(Entries(InstanceHubKind.Raid))
                     .Any(e => e.RelatedId == CurrentInstanceId && e.Quantity > 0))
        {
            CurrentInstanceId = Guid.Empty;
            CurrentInstanceName = string.Empty;
        }

        StatusLine = string.IsNullOrEmpty(CurrentInstanceName)
            ? $"{KindLabel(snapshot.Kind)} : {snapshot.Entries.Count} entrée(s)."
            : $"Instance : {CurrentInstanceName}";
    }

    public void ApplyResult(InstanceHubResultWire result)
    {
        if (result.Success && result.SubjectId != Guid.Empty)
        {
            CurrentInstanceId = result.SubjectId;
            CurrentInstanceName = FindTitle(result.SubjectId) ?? KindLabel(result.Kind);
        }
        else if (result.Success && result.Action == (byte)InstanceHubAction.Leave)
        {
            CurrentInstanceId = Guid.Empty;
            CurrentInstanceName = string.Empty;
        }

        StatusLine = string.IsNullOrWhiteSpace(result.Message)
            ? KindLabel(result.Kind)
            : result.Message;
    }

    public IReadOnlyList<InstanceHubEntryWire> Entries(InstanceHubKind kind) => kind switch
    {
        InstanceHubKind.Dungeon => Dungeon?.Entries ?? Array.Empty<InstanceHubEntryWire>(),
        InstanceHubKind.Raid => Raid?.Entries ?? Array.Empty<InstanceHubEntryWire>(),
        _ => Array.Empty<InstanceHubEntryWire>()
    };

    public IReadOnlyList<InstanceHubEntryWire> AllEntries()
    {
        var dungeon = Entries(InstanceHubKind.Dungeon);
        var raid = Entries(InstanceHubKind.Raid);
        if (dungeon.Count == 0)
        {
            return raid;
        }

        if (raid.Count == 0)
        {
            return dungeon;
        }

        var all = new List<InstanceHubEntryWire>(dungeon.Count + raid.Count);
        all.AddRange(dungeon);
        all.AddRange(raid);
        return all;
    }

    public string EmptyHint()
    {
        if (AllEntries().Count > 0)
        {
            return string.Empty;
        }

        return "Aucun donjon — catalogue vide (MVP).";
    }

    public static string KindLabel(InstanceHubKind kind) => kind switch
    {
        InstanceHubKind.Dungeon => "Donjon",
        InstanceHubKind.Raid => "Raid",
        _ => "Instance"
    };

    public static string FormatEntry(InstanceHubEntryWire entry)
    {
        var title = string.IsNullOrWhiteSpace(entry.Title) ? "(sans titre)" : entry.Title;
        if (entry.RelatedId == Guid.Empty || entry.Quantity <= 0)
        {
            return title + " — overworld";
        }

        return $"{title} — instance ({entry.Quantity} joueur(s), seed {entry.PriceOrFlags})";
    }

    public static byte[] QueryExtra() => Array.Empty<byte>();

    public static byte[] EnterExtra(Guid definitionId) => InstanceHubWire.BuildDefinitionIdExtra(definitionId);

    public static byte[] LeaveExtra() => Array.Empty<byte>();

    private string? FindTitle(Guid instanceId)
    {
        foreach (var entry in AllEntries())
        {
            if (entry.RelatedId == instanceId && !string.IsNullOrWhiteSpace(entry.Title))
            {
                return entry.Title;
            }
        }

        return null;
    }
}
