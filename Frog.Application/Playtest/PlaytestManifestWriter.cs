using System.Text.Json;
using System.Text.Json.Serialization;
using Frog.Core.IO;

namespace Frog.Application.Playtest;

/// <summary>Écrit le manifeste playtest + blobs .fmap (cartes publiées uniquement).</summary>
public static class PlaytestManifestWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Write(PlaytestLaunchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Directory.CreateDirectory(plan.WorkDirectory);

        var entries = new List<PlaytestManifestMapEntry>(plan.Maps.Count);
        foreach (var map in plan.Maps)
        {
            var fileName = $"map-{map.RuntimeMapId}.fmap";
            var path = Path.Combine(plan.WorkDirectory, fileName);
            File.WriteAllBytes(path, map.SerializedFmap);
            entries.Add(new PlaytestManifestMapEntry
            {
                CanonicalMapId = map.CanonicalMapId,
                PublishedRevision = map.PublishedRevision,
                RuntimeMapId = map.RuntimeMapId,
                Name = map.Name,
                RelativePath = fileName,
            });
        }

        var document = new PlaytestManifestDocument
        {
            CorrelationId = plan.CorrelationId,
            PrimaryCanonicalMapId = plan.PrimaryCanonicalMapId,
            PrimaryPublishedRevision = plan.PrimaryPublishedRevision,
            Spawn = plan.Spawn,
            Maps = entries,
        };

        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(plan.ManifestPath, json);
        return plan.ManifestPath;
    }

    public static PlaytestManifestDocument Read(string manifestPath)
    {
        var json = File.ReadAllText(manifestPath);
        var doc = JsonSerializer.Deserialize<PlaytestManifestDocument>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Manifeste playtest vide ou invalide.");
        if (doc.SchemaVersion != PlaytestManifestDocument.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Version de manifeste playtest non supportée: {doc.SchemaVersion}.");
        }

        return doc;
    }

    /// <summary>Charge les blobs .fmap référencés par le manifeste (répertoire = dossier du manifeste).</summary>
    public static IReadOnlyDictionary<int, (byte[] Bytes, long Revision, string Name)> LoadBlobs(
        PlaytestManifestDocument document,
        string manifestDirectory)
    {
        ArgumentNullException.ThrowIfNull(document);
        var result = new Dictionary<int, (byte[] Bytes, long Revision, string Name)>();
        foreach (var entry in document.Maps)
        {
            var path = Path.Combine(manifestDirectory, entry.RelativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Blob playtest introuvable: {path}");
            }

            result[entry.RuntimeMapId] = (File.ReadAllBytes(path), entry.PublishedRevision, entry.Name);
        }

        return result;
    }

    /// <summary>Vérifie qu’un blob .fmap se désérialise (défensive).</summary>
    public static bool TryValidateFmap(byte[] bytes, out string? error)
    {
        error = null;
        try
        {
            _ = new MapSerializer().Deserialize(bytes);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
