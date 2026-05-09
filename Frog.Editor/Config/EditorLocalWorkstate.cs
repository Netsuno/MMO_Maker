using System.IO;
using System.Text.Json;

namespace Frog.Editor.Config;

/// <summary>Mémo locale éditeur (à côté de l’exécutable ; non sensible).</summary>
public static class EditorLocalWorkstate
{
    private sealed class PersistedDto
    {
        public int LastPublishedFrogMapId { get; set; } = 1;

        /// <summary>Chemin absolu vers <c>Frog.Client.exe</c> si la détection automatique a échoué une première fois.</summary>
        public string? ClientExePath { get; set; }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "editor-workstate.json");

    private static PersistedDto LoadOrDefault()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new PersistedDto();
            }

            var json = File.ReadAllText(FilePath);
            var dto = JsonSerializer.Deserialize<PersistedDto>(json, SerializerOptions);
            return dto ?? new PersistedDto();
        }
        catch
        {
            return new PersistedDto();
        }
    }

    private static void Save(PersistedDto dto)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dto, SerializerOptions));
        }
        catch
        {
            // optionnel pour l’UX ; échec ignoré
        }
    }

    public static int ReadLastPublishedFrogMapId()
    {
        var id = LoadOrDefault().LastPublishedFrogMapId;
        return id >= 1 ? id : 1;
    }

    public static void WriteLastPublishedFrogMapId(int frogMapId)
    {
        if (frogMapId < 1)
        {
            return;
        }

        var dto = LoadOrDefault();
        dto.LastPublishedFrogMapId = frogMapId;
        Save(dto);
    }

    public static bool TryReadClientExePath(out string fullPath)
    {
        fullPath = string.Empty;
        var p = LoadOrDefault().ClientExePath?.Trim();
        if (string.IsNullOrEmpty(p) || !File.Exists(p))
        {
            return false;
        }

        fullPath = p;
        return true;
    }

    public static void WriteClientExePath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return;
        }

        var dto = LoadOrDefault();
        dto.ClientExePath = Path.GetFullPath(absolutePath);
        Save(dto);
    }
}
