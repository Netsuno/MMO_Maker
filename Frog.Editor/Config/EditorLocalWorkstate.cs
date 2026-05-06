using System.IO;
using System.Text.Json;

namespace Frog.Editor.Config;

/// <summary>Mémo locale éditeur (à côté de l’exécutable ; non sensible).</summary>
public static class EditorLocalWorkstate
{
    private sealed class PersistedDto
    {
        public int LastPublishedFrogMapId { get; set; } = 1;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "editor-workstate.json");

    public static int ReadLastPublishedFrogMapId()
    {
        try
        {
            var path = FilePath;
            if (!File.Exists(path))
            {
                return 1;
            }

            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<PersistedDto>(json, SerializerOptions);
            return dto?.LastPublishedFrogMapId is >= 1 and var id ? id : 1;
        }
        catch
        {
            return 1;
        }
    }

    public static void WriteLastPublishedFrogMapId(int frogMapId)
    {
        if (frogMapId < 1)
        {
            return;
        }

        try
        {
            var dto = new PersistedDto { LastPublishedFrogMapId = frogMapId };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dto, SerializerOptions));
        }
        catch
        {
            // optionnel pour l’UX ; échec ignoré
        }
    }
}
