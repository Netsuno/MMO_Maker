using Frog.Core.Enums;
using Frog.Core.Instances;
using Frog.Core.Protocol;
using Frog.Server.Models;
using Frog.Server.Services;
using Frog.Server.Social;

namespace Frog.Server.Instances;

/// <summary>
/// Stub in-memory donjon / raid / instance. Create/destroy + gate groupe + leave overworld.
/// TODO persistance PostgreSQL — voir docs/progress/dungeons-instances/STATUS.md.
/// </summary>
public sealed class InstanceHubService
{
    private readonly PartyRoster _parties;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, InstanceRun> _runs = new();
    private readonly Dictionary<Guid, Guid> _runByParty = new();

    public InstanceHubService(PartyRoster parties)
    {
        _parties = parties;
    }

    public (InstanceHubResultWire Result, InstanceHubSnapshotWire? Snapshot) Execute(
        Session session,
        InstanceHubKind kind,
        byte action,
        Guid requestId,
        ReadOnlyMemory<byte> extra)
    {
        if (session.CharacterGuid is not Guid actorId || actorId == Guid.Empty)
        {
            return (Fail(kind, action, requestId, "Personnage actif requis."), null);
        }

        if (!InstanceHubWire.IsKnownKind((byte)kind))
        {
            return (Fail(kind, action, requestId, "Action inconnue."), null);
        }

        if (!InstanceHubWire.IsKnownAction(action))
        {
            return (Fail(kind, action, requestId, "Action inconnue."), null);
        }

        return action switch
        {
            (byte)InstanceHubAction.Query => Query(session, kind, action, requestId),
            (byte)InstanceHubAction.Enter => Enter(session, actorId, kind, action, requestId, extra.Span),
            (byte)InstanceHubAction.Leave => Leave(session, actorId, kind, action, requestId),
            _ => (Fail(kind, action, requestId, "Action inconnue."), null)
        };
    }

    internal InstanceRun? FindRun(Guid instanceId)
    {
        lock (_gate)
        {
            return _runs.TryGetValue(instanceId, out var run) ? run.Clone() : null;
        }
    }

    internal int LiveRunCount
    {
        get
        {
            lock (_gate)
            {
                return _runs.Count;
            }
        }
    }

    private (InstanceHubResultWire Result, InstanceHubSnapshotWire Snapshot) Query(
        Session session,
        InstanceHubKind kind,
        byte action,
        Guid requestId)
    {
        lock (_gate)
        {
            var snap = BuildSnapshotLocked(kind, session);
            return (new InstanceHubResultWire(kind, action, requestId, true, "Catalogue instance (MVP).", snap.SubjectId), snap);
        }
    }

    private (InstanceHubResultWire Result, InstanceHubSnapshotWire? Snapshot) Enter(
        Session session,
        Guid actorId,
        InstanceHubKind kind,
        byte action,
        Guid requestId,
        ReadOnlySpan<byte> extra)
    {
        if (!InstanceHubWire.TryReadDefinitionId(extra, out var definitionId))
        {
            return (Fail(kind, action, requestId, "Donjon inconnu."), null);
        }

        var definition = DungeonCatalog.Find(definitionId);
        if (definition is null || definition.Kind != kind)
        {
            return (Fail(kind, action, requestId, "Donjon inconnu."), null);
        }

        var party = _parties.FindByCharacter(actorId);
        if (party is null)
        {
            return (Fail(kind, action, requestId, "Groupe requis."), null);
        }

        if (party.Members.Count < definition.MinPartySize)
        {
            return (Fail(kind, action, requestId, "Groupe insuffisant."), null);
        }

        lock (_gate)
        {
            if (session.InstanceId != Guid.Empty)
            {
                return (Fail(kind, action, requestId, "Deja dans une instance."), null);
            }

            InstanceRun run;
            string message;
            if (_runByParty.TryGetValue(party.Id, out var existingId) && _runs.TryGetValue(existingId, out var existing))
            {
                if (existing.DefinitionId != definition.Id)
                {
                    return (Fail(kind, action, requestId, "Le groupe a deja une instance."), null);
                }

                existing.Occupants.Add(actorId);
                run = existing;
                message = "Instance rejointe.";
            }
            else if (party.LeaderId != actorId)
            {
                return (Fail(kind, action, requestId, "Chef de groupe requis."), null);
            }
            else
            {
                var instanceId = InstanceId.New();
                var seed = SeedFrom(instanceId.Value, definition.Id);
                run = new InstanceRun
                {
                    Id = instanceId,
                    DefinitionId = definition.Id,
                    PartyId = party.Id,
                    Seed = seed,
                    Layout = ProceduralDungeonGenerator.Generate(seed),
                    Name = definition.Name,
                    Kind = definition.Kind,
                    TemplateMapId = definition.TemplateMapId,
                    SpawnTileX = definition.SpawnTileX,
                    SpawnTileY = definition.SpawnTileY,
                };
                run.Occupants.Add(actorId);
                _runs[instanceId.Value] = run;
                _runByParty[party.Id] = instanceId.Value;
                message = "Instance creee.";
            }

            ApplyEnterHook(session, run);
            var snap = BuildSnapshotLocked(kind, session);
            return (new InstanceHubResultWire(kind, action, requestId, true, message, run.Id.Value), snap);
        }
    }

    private (InstanceHubResultWire Result, InstanceHubSnapshotWire? Snapshot) Leave(
        Session session,
        Guid actorId,
        InstanceHubKind kind,
        byte action,
        Guid requestId)
    {
        lock (_gate)
        {
            if (session.InstanceId == Guid.Empty
                || !_runs.TryGetValue(session.InstanceId, out var run))
            {
                return (Fail(kind, action, requestId, "Pas dans une instance."), null);
            }

            run.Occupants.Remove(actorId);
            ApplyLeaveHook(session);
            if (run.Occupants.Count == 0)
            {
                _runs.Remove(run.Id.Value);
                _runByParty.Remove(run.PartyId);
            }

            var snap = BuildSnapshotLocked(kind, session);
            return (new InstanceHubResultWire(kind, action, requestId, true, "Retour overworld.", Guid.Empty), snap);
        }
    }

    private InstanceHubSnapshotWire BuildSnapshotLocked(InstanceHubKind kind, Session session)
    {
        InstanceRun? partyRun = null;
        if (session.CharacterGuid is Guid actor)
        {
            var party = _parties.FindByCharacter(actor);
            if (party is not null
                && _runByParty.TryGetValue(party.Id, out var runId)
                && _runs.TryGetValue(runId, out var found))
            {
                partyRun = found;
            }
        }

        var defs = DungeonCatalog.OfKind(kind);
        var entries = new InstanceHubEntryWire[defs.Count];
        for (var i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            var related = partyRun is not null && partyRun.DefinitionId == def.Id
                ? partyRun.Id.Value
                : Guid.Empty;
            var occupants = related == Guid.Empty ? 0 : partyRun!.Occupants.Count;
            var seed = related == Guid.Empty ? 0 : partyRun!.Seed;
            entries[i] = new InstanceHubEntryWire(def.Id, related, occupants, seed, def.Name);
        }

        var subject = session.InstanceId;
        return new InstanceHubSnapshotWire(kind, subject, entries);
    }

    private static void ApplyEnterHook(Session session, InstanceRun run)
    {
        session.OverworldReturnMapId = session.CurrentMapId;
        session.OverworldReturnTileX = session.PositionX;
        session.OverworldReturnTileY = session.PositionY;
        session.InstanceId = run.Id.Value;
        session.CurrentMapId = run.TemplateMapId;
        session.PositionX = run.SpawnTileX;
        session.PositionY = run.SpawnTileY;
        SessionPixelSync.SyncFromTileGrid(session);
    }

    private static void ApplyLeaveHook(Session session)
    {
        session.InstanceId = Guid.Empty;
        session.CurrentMapId = session.OverworldReturnMapId;
        session.PositionX = session.OverworldReturnTileX;
        session.PositionY = session.OverworldReturnTileY;
        SessionPixelSync.SyncFromTileGrid(session);
    }

    private static int SeedFrom(Guid instanceId, Guid definitionId)
    {
        var a = instanceId.GetHashCode();
        var b = definitionId.GetHashCode();
        var seed = a ^ b;
        if (seed == int.MinValue)
        {
            return 1;
        }

        return seed == 0 ? 1 : Math.Abs(seed);
    }

    private static InstanceHubResultWire Fail(InstanceHubKind kind, byte action, Guid requestId, string message)
        => new(kind, action, requestId, false, message, Guid.Empty);

    internal sealed class InstanceRun
    {
        public required InstanceId Id { get; init; }
        public required Guid DefinitionId { get; init; }
        public required Guid PartyId { get; init; }
        public required int Seed { get; init; }
        public required ProceduralDungeonLayout Layout { get; init; }
        public required string Name { get; init; }
        public required InstanceHubKind Kind { get; init; }
        public required int TemplateMapId { get; init; }
        public required int SpawnTileX { get; init; }
        public required int SpawnTileY { get; init; }
        public HashSet<Guid> Occupants { get; } = [];

        public InstanceRun Clone()
        {
            var copy = new InstanceRun
            {
                Id = Id,
                DefinitionId = DefinitionId,
                PartyId = PartyId,
                Seed = Seed,
                Layout = Layout,
                Name = Name,
                Kind = Kind,
                TemplateMapId = TemplateMapId,
                SpawnTileX = SpawnTileX,
                SpawnTileY = SpawnTileY,
            };
            foreach (var id in Occupants)
            {
                copy.Occupants.Add(id);
            }

            return copy;
        }
    }
}
