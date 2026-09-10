using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// Disposition d'un essai de commit Core (contrat que J4-PG doit miroiter).
/// </summary>
public enum MapEventCommitDisposition
{
    Committed,
    IdempotentReplay,
    RolledBack,
    Cancelled,
    Rejected,
}

/// <summary>Résultat d'un essai de commit d'une <see cref="MapEventTransactionalUnit"/>.</summary>
public sealed record MapEventCommitOutcome(
    MapEventCommitDisposition Disposition,
    string? Error,
    MapEventExecutionIdentity Identity,
    MapEventWorldScratch? Snapshot = null,
    MapEventWaitBoundary? Wait = null)
{
    public bool WroteLedger =>
        Disposition is MapEventCommitDisposition.Committed or MapEventCommitDisposition.IdempotentReplay;
}

/// <summary>
/// État monde scratch (switches / items / or / intents session) pour prouver
/// cancel-before-commit, rollback intégral et retry sans poison ledger.
/// </summary>
public sealed class MapEventWorldScratch
{
    public Dictionary<string, bool> Switches { get; private set; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> Variables { get; private set; } = new(StringComparer.Ordinal);

    public Dictionary<Guid, int> Items { get; private set; } = new();

    public HashSet<Guid> StartedQuests { get; private set; } = [];

    public HashSet<Guid> LearnedProfessions { get; private set; } = [];

    public int Gold { get; set; }

    public string? ShowText { get; set; }

    public (int MapId, int TileX, int TileY)? Teleport { get; set; }

    public Guid? DialogueId { get; set; }

    public MapEventWorldScratch Clone() =>
        new()
        {
            Switches = new Dictionary<string, bool>(Switches, StringComparer.Ordinal),
            Variables = new Dictionary<string, int>(Variables, StringComparer.Ordinal),
            Items = new Dictionary<Guid, int>(Items),
            StartedQuests = new HashSet<Guid>(StartedQuests),
            LearnedProfessions = new HashSet<Guid>(LearnedProfessions),
            Gold = Gold,
            ShowText = ShowText,
            Teleport = Teleport,
            DialogueId = DialogueId,
        };

    public void ReplaceWith(MapEventWorldScratch other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Switches = new Dictionary<string, bool>(other.Switches, StringComparer.Ordinal);
        Variables = new Dictionary<string, int>(other.Variables, StringComparer.Ordinal);
        Items = new Dictionary<Guid, int>(other.Items);
        StartedQuests = new HashSet<Guid>(other.StartedQuests);
        LearnedProfessions = new HashSet<Guid>(other.LearnedProfessions);
        Gold = other.Gold;
        ShowText = other.ShowText;
        Teleport = other.Teleport;
        DialogueId = other.DialogueId;
    }
}

/// <summary>Ligne ledger scratch (CharacterId, RequestId).</summary>
public sealed record MapEventLedgerEntry(
    MapEventExecutionIdentity Identity,
    MapEventWorldScratch Snapshot,
    MapEventWaitBoundary? Wait);

/// <summary>
/// Bac à sable de commit Core : une unité = une TX + une clé ledger.
/// Expose cancel-before-commit, rollback intégral et retry pour les tests PG à venir.
/// </summary>
public sealed class MapEventTransactionalCommitSandbox
{
    private readonly Dictionary<(Guid CharacterId, Guid RequestId), MapEventLedgerEntry> _ledger = new();

    public MapEventWorldScratch World { get; } = new();

    public Func<CancellationToken, Task>? BeforeCommitAsync { get; set; }

    public int TransactionsBegun { get; private set; }

    public int LedgerCount => _ledger.Count;

    public bool TryGetLedger(
        in MapEventExecutionIdentity identity,
        out MapEventLedgerEntry entry) =>
        _ledger.TryGetValue(identity.LedgerKey, out entry!);

    public Task<MapEventCommitOutcome> TryCommitAsync(
        MapEventTransactionalUnit unit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return CommitCoreAsync(unit, cancellationToken);
    }

    public MapEventCommitOutcome TryCommit(
        MapEventTransactionalUnit unit,
        CancellationToken cancellationToken = default) =>
        TryCommitAsync(unit, cancellationToken).GetAwaiter().GetResult();

    private async Task<MapEventCommitOutcome> CommitCoreAsync(
        MapEventTransactionalUnit unit,
        CancellationToken cancellationToken)
    {
        if (!unit.IsSuccess)
        {
            return Reject(unit.Identity, unit.Error ?? "Unité transactionnelle invalide.");
        }

        var identity = unit.Identity;
        if (!identity.IsValid)
        {
            return Reject(identity, "Identité d'exécution invalide.");
        }

        if (_ledger.TryGetValue(identity.LedgerKey, out var existing))
        {
            if (!existing.Identity.IsSameActivation(identity)
                || existing.Identity.WaitOrdinal != identity.WaitOrdinal
                || existing.Identity.PlacementId != identity.PlacementId
                || existing.Identity.CatalogAliasId != identity.CatalogAliasId)
            {
                return Reject(identity, "RequestId réutilisé avec événement différent.");
            }

            return new MapEventCommitOutcome(
                MapEventCommitDisposition.IdempotentReplay,
                null,
                identity,
                existing.Snapshot.Clone(),
                existing.Wait);
        }

        TransactionsBegun++;
        var working = World.Clone();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var effect in unit.CommitEffects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var err = Apply(working, effect);
                if (err is not null)
                {
                    return Rollback(identity, err);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (BeforeCommitAsync is not null)
            {
                await BeforeCommitAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = working.Clone();
            World.ReplaceWith(working);
            var wait = unit.Wait;
            _ledger[identity.LedgerKey] = new MapEventLedgerEntry(identity, snapshot, wait);
            return new MapEventCommitOutcome(
                MapEventCommitDisposition.Committed,
                null,
                identity,
                snapshot,
                wait);
        }
        catch (OperationCanceledException)
        {
            return new MapEventCommitOutcome(
                MapEventCommitDisposition.Cancelled,
                "Commit annulé avant écriture ledger.",
                identity);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Rollback(identity, ex.Message);
        }
    }

    private static MapEventCommitOutcome Reject(MapEventExecutionIdentity identity, string error) =>
        new(MapEventCommitDisposition.Rejected, error, identity);

    private static MapEventCommitOutcome Rollback(MapEventExecutionIdentity identity, string error) =>
        new(MapEventCommitDisposition.RolledBack, error, identity);

    private static string? Apply(MapEventWorldScratch world, MapEventCommandDefinition command)
    {
        switch (command.Discriminator)
        {
            case MapEventCommandDiscriminators.ShowText:
                if (!MapEventParameterSchemas.TryParseShowText(command.ParameterJson, out var text, out var showErr))
                {
                    return showErr;
                }

                world.ShowText = text;
                return null;

            case MapEventCommandDiscriminators.SetSwitch:
                if (!MapEventParameterSchemas.TryParseSetSwitch(
                        command.ParameterJson,
                        out var switchId,
                        out var value,
                        out var switchErr))
                {
                    return switchErr;
                }

                world.Switches[switchId] = value;
                return null;

            case MapEventCommandDiscriminators.SetVariable:
                if (!MapEventParameterSchemas.TryParseSetVariable(
                        command.ParameterJson,
                        out var setVarId,
                        out var setValue,
                        out var setVarErr))
                {
                    return setVarErr;
                }

                world.Variables[setVarId] = setValue;
                return null;

            case MapEventCommandDiscriminators.AddVariable:
            case MapEventCommandDiscriminators.SubVariable:
                string deltaVarId;
                int delta;
                string? deltaErr;
                var parsedDelta = command.Discriminator == MapEventCommandDiscriminators.AddVariable
                    ? MapEventParameterSchemas.TryParseAddVariable(
                        command.ParameterJson,
                        out deltaVarId,
                        out delta,
                        out deltaErr)
                    : MapEventParameterSchemas.TryParseSubVariable(
                        command.ParameterJson,
                        out deltaVarId,
                        out delta,
                        out deltaErr);
                if (!parsedDelta)
                {
                    return deltaErr;
                }

                world.Variables.TryGetValue(deltaVarId, out var current);
                world.Variables[deltaVarId] = command.Discriminator == MapEventCommandDiscriminators.AddVariable
                    ? current + delta
                    : current - delta;
                return null;

            case MapEventCommandDiscriminators.GiveItem:
            case MapEventCommandDiscriminators.TakeItem:
                if (!MapEventParameterSchemas.TryParseItemMutation(
                        command.ParameterJson,
                        out var itemId,
                        out var quantity,
                        out var itemErr))
                {
                    return itemErr;
                }

                if (itemId == Guid.Empty)
                {
                    return "give_item/take_item: itemId invalide.";
                }

                world.Items.TryGetValue(itemId, out var have);
                if (command.Discriminator == MapEventCommandDiscriminators.TakeItem)
                {
                    if (have < quantity)
                    {
                        return "take_item: quantité insuffisante.";
                    }

                    world.Items[itemId] = have - quantity;
                    return null;
                }

                world.Items[itemId] = have + quantity;
                return null;

            case MapEventCommandDiscriminators.GiveGold:
            case MapEventCommandDiscriminators.TakeGold:
                if (!MapEventParameterSchemas.TryParseGoldMutation(
                        command.ParameterJson,
                        out var amount,
                        out var goldErr))
                {
                    return goldErr;
                }

                if (command.Discriminator == MapEventCommandDiscriminators.TakeGold)
                {
                    if (world.Gold < amount)
                    {
                        return "take_gold: or insuffisant.";
                    }

                    world.Gold -= amount;
                    return null;
                }

                world.Gold += amount;
                return null;

            case MapEventCommandDiscriminators.StartQuest:
                if (!MapEventParameterSchemas.TryParseQuestId(command.ParameterJson, out var questId, out var questErr))
                {
                    return questErr;
                }

                world.StartedQuests.Add(questId);
                return null;

            case MapEventCommandDiscriminators.AdvanceQuest:
            case MapEventCommandDiscriminators.TurnInQuest:
                return null;

            case MapEventCommandDiscriminators.LearnProfession:
                if (!MapEventParameterSchemas.TryParseLearnProfession(
                        command.ParameterJson,
                        out var professionId,
                        out var profErr))
                {
                    return profErr;
                }

                world.LearnedProfessions.Add(professionId);
                return null;

            case MapEventCommandDiscriminators.Wait:
                return null;

            case MapEventCommandDiscriminators.Teleport:
                if (!MapEventParameterSchemas.TryParseTeleport(
                        command.ParameterJson,
                        out var mapId,
                        out var tileX,
                        out var tileY,
                        out var teleportErr))
                {
                    return teleportErr;
                }

                world.Teleport = (mapId, tileX, tileY);
                return null;

            case MapEventCommandDiscriminators.StartDialogue:
                if (!MapEventParameterSchemas.TryParseStartDialogue(
                        command.ParameterJson,
                        out var dialogueId,
                        out var dialogueErr))
                {
                    return dialogueErr;
                }

                world.DialogueId = dialogueId;
                return null;

            default:
                return $"Commande non supportée en unité transactionnelle: {command.Discriminator}.";
        }
    }
}
