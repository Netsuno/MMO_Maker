using System.Text.Json;
using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;

namespace Frog.Editor.Services;

public readonly record struct Phase8ContentListRow(
    Guid Id,
    string Name,
    int? EditorAliasId,
    long Revision,
    ContentPublishStatus Status,
    long? PublishedRevision);

/// <summary>Catalogue Phase 8 (dialogues, quêtes, recettes, …) via PostgreSQL.</summary>
public class Phase8ContentPostgreSqlService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IPhase8ContentEditorRepository _repository;
    private readonly FrogDbContextGate? _gate;
    private readonly bool _ownsGate;
    private bool _disposed;
    private int _disposeCallCount;

    public Phase8ContentPostgreSqlService(
        IPhase8ContentEditorRepository repository,
        FrogDbContextGate? gate = null,
        bool ownsGate = false)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _gate = gate;
        _ownsGate = ownsGate;
        if (_ownsGate && _gate is null)
        {
            throw new ArgumentException("ownsGate requires a non-null gate.", nameof(gate));
        }
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public bool IsAvailable => Capabilities.AllowsSave;

    public bool IsDisposedForTest => _disposed;

    public int DisposeCallCountForTest => Volatile.Read(ref _disposeCallCount);

    public async Task<IReadOnlyList<Phase8ContentListRow>> ListAsync(
        Phase8ContentKind kind,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repository.ListSummariesAsync(kind, cancellationToken).ConfigureAwait(false);
        return rows
            .Select(r => new Phase8ContentListRow(
                r.Id,
                r.Name,
                r.EditorAliasId,
                r.Revision,
                r.Status,
                r.PublishedRevision))
            .ToList();
    }

    public Task<Phase8StoredContent?> LoadDraftAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.LoadDraftByIdAsync(id, cancellationToken);

    public Task<Phase8SaveContentResult> SaveAsync(
        Phase8SaveContentRequest request,
        CancellationToken cancellationToken = default) =>
        _repository.SaveAsync(request, cancellationToken);

    public Task<Phase8DeleteContentResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);

    public static string CreateDefaultPayload(Phase8ContentKind kind, Guid id, string name)
    {
        return kind switch
        {
            Phase8ContentKind.Dialogue => Serialize(new DialogueDefinition
            {
                Id = id,
                Name = name,
                Lines =
                [
                    new DialogueLineDefinition { Speaker = "PNJ", Text = "Bonjour, voyageur." },
                ],
            }),
            Phase8ContentKind.Quest => Serialize(new QuestDefinition
            {
                Id = id,
                Name = name,
                Stages =
                [
                    new QuestStageDefinition
                    {
                        Description = "Étape 1",
                        Objectives =
                        [
                            new QuestObjectiveDefinition
                            {
                                Kind = QuestObjectiveKind.Visit,
                                Description = "Visiter la zone",
                                TargetMapId = 1,
                            },
                        ],
                    },
                ],
            }),
            Phase8ContentKind.CommonEvent => Serialize(new CommonEventDefinition
            {
                Id = id,
                Name = name,
            }),
            Phase8ContentKind.Profession => Serialize(new ProfessionDefinition
            {
                Id = id,
                Name = name,
            }),
            Phase8ContentKind.Recipe => Serialize(new RecipeDefinition
            {
                Id = id,
                Name = name,
                ProfessionId = Guid.NewGuid(),
                OutputItemId = Guid.NewGuid(),
                Ingredients =
                [
                    new RecipeIngredientDefinition { ItemId = Guid.NewGuid(), Quantity = 1 },
                ],
            }),
            Phase8ContentKind.Region => Serialize(new RegionDefinition
            {
                Id = id,
                Name = name,
                MapId = 1,
                WeatherProfileId = Guid.NewGuid(),
            }),
            Phase8ContentKind.WeatherProfile => Serialize(new WeatherProfileDefinition
            {
                Id = id,
                Name = name,
            }),
            _ => "{}",
        };
    }

    /// <summary>Réécrit l'Id (et le Name si fourni) dans un payload JSON connu.</summary>
    public static bool TryRewritePayloadIdentity(
        Phase8ContentKind kind,
        string payloadJson,
        Guid newId,
        string? newName,
        out string rewrittenJson,
        out string? error)
    {
        rewrittenJson = string.Empty;
        error = null;
        switch (kind)
        {
            case Phase8ContentKind.Dialogue when TryDeserialize(payloadJson, out DialogueDefinition d, out error):
                d.Id = newId;
                if (newName is not null)
                {
                    d.Name = newName;
                }

                rewrittenJson = Serialize(d);
                return true;
            case Phase8ContentKind.Quest when TryDeserialize(payloadJson, out QuestDefinition q, out error):
                q.Id = newId;
                if (newName is not null)
                {
                    q.Name = newName;
                }

                rewrittenJson = Serialize(q);
                return true;
            case Phase8ContentKind.CommonEvent when TryDeserialize(payloadJson, out CommonEventDefinition ce, out error):
                ce.Id = newId;
                if (newName is not null)
                {
                    ce.Name = newName;
                }

                rewrittenJson = Serialize(ce);
                return true;
            case Phase8ContentKind.Profession when TryDeserialize(payloadJson, out ProfessionDefinition p, out error):
                p.Id = newId;
                if (newName is not null)
                {
                    p.Name = newName;
                }

                rewrittenJson = Serialize(p);
                return true;
            case Phase8ContentKind.Recipe when TryDeserialize(payloadJson, out RecipeDefinition r, out error):
                r.Id = newId;
                if (newName is not null)
                {
                    r.Name = newName;
                }

                rewrittenJson = Serialize(r);
                return true;
            case Phase8ContentKind.Region when TryDeserialize(payloadJson, out RegionDefinition reg, out error):
                reg.Id = newId;
                if (newName is not null)
                {
                    reg.Name = newName;
                }

                rewrittenJson = Serialize(reg);
                return true;
            case Phase8ContentKind.WeatherProfile when TryDeserialize(payloadJson, out WeatherProfileDefinition w, out error):
                w.Id = newId;
                if (newName is not null)
                {
                    w.Name = newName;
                }

                rewrittenJson = Serialize(w);
                return true;
            default:
                error ??= "Type de contenu inconnu ou JSON invalide.";
                return false;
        }
    }

    public static bool TryValidatePayload(Phase8ContentKind kind, string payloadJson, out string? error)
    {
        error = null;
        return kind switch
        {
            Phase8ContentKind.Dialogue => TryDeserialize(payloadJson, out DialogueDefinition d, out error) && d.Validate(out error),
            Phase8ContentKind.Quest => TryDeserialize(payloadJson, out QuestDefinition q, out error) && q.Validate(out error),
            Phase8ContentKind.CommonEvent => TryDeserialize(payloadJson, out CommonEventDefinition ce, out error) && ce.Validate(out error),
            Phase8ContentKind.Profession => TryDeserialize(payloadJson, out ProfessionDefinition p, out error) && p.Validate(out error),
            Phase8ContentKind.Recipe => TryDeserialize(payloadJson, out RecipeDefinition r, out error) && r.Validate(out error),
            Phase8ContentKind.Region => TryDeserialize(payloadJson, out RegionDefinition reg, out error) && reg.Validate(out error),
            Phase8ContentKind.WeatherProfile => TryDeserialize(payloadJson, out WeatherProfileDefinition w, out error) && w.Validate(out error),
            _ => Fail("Type de contenu inconnu.", out error),
        };

        static bool Fail(string message, out string? err)
        {
            err = message;
            return false;
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    public static bool TryDeserialize<T>(string json, out T value, out string? error)
    {
        value = default!;
        error = null;
        try
        {
            value = JsonSerializer.Deserialize<T>(json, JsonOptions)!;
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interlocked.Exchange(ref _disposeCallCount, 1);
        if (_ownsGate)
        {
            _gate?.Dispose();
        }
    }
}

/// <summary>Service Phase 8 branché sur un dépôt mémoire (smoke Windows / tests).</summary>
public sealed class InMemoryPhase8ContentEditorService : Phase8ContentPostgreSqlService
{
    public InMemoryPhase8ContentEditorService()
        : base(new InMemoryPhase8ContentEditorRepository(), gate: null, ownsGate: false)
    {
    }

    public InMemoryPhase8ContentEditorService(InMemoryPhase8ContentEditorRepository repository)
        : base(repository ?? throw new ArgumentNullException(nameof(repository)), gate: null, ownsGate: false)
    {
    }
}
