#nullable enable
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Frog.Core.Models;

namespace Frog.Core.IO;

/// <summary>Sérialisation JSON du catalogue prefabs (UTF‑8, camelCase).</summary>
public static class PrefabCatalogJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static byte[] Serialize(PrefabCatalog catalog)
        => JsonSerializer.SerializeToUtf8Bytes(catalog, Options);

    public static byte[] SerializeObject<T>(T value)
        => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    public static T? TryDeserializeObject<T>(ReadOnlySpan<byte> utf8)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(utf8, Options);
        }
        catch
        {
            return default;
        }
    }

    public static PrefabCatalog? TryDeserialize(ReadOnlySpan<byte> utf8)
    {
        try
        {
            return JsonSerializer.Deserialize<PrefabCatalog>(utf8, Options);
        }
        catch
        {
            return null;
        }
    }

    public static PrefabCatalog? TryDeserializeFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return TryDeserialize(File.ReadAllBytes(path));
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Sérialisation JSON des placements prefab (sidecar carte).</summary>
public static class PrefabPlacementDocumentJson
{
    public static byte[] Serialize(PrefabPlacementDocument document)
        => JsonSerializer.SerializeToUtf8Bytes(document, PrefabCatalogJson.Options);

    public static PrefabPlacementDocument? TryDeserialize(ReadOnlySpan<byte> utf8)
    {
        try
        {
            return JsonSerializer.Deserialize<PrefabPlacementDocument>(utf8, PrefabCatalogJson.Options);
        }
        catch
        {
            return null;
        }
    }

    public static PrefabPlacementDocument? TryDeserializeFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return TryDeserialize(File.ReadAllBytes(path));
        }
        catch
        {
            return null;
        }
    }
}
