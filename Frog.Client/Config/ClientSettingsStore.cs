using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frog.Client.Config;

/// <summary>
/// Charge / enregistre les réglages client. Chemin par défaut
/// <c>%LocalAppData%\Frog\client-settings.json</c> ; surcharge tests
/// <see cref="PathEnvironmentVariable"/>.
/// </summary>
public sealed class ClientSettingsStore
{
    public const string PathEnvironmentVariable = "FROG_CLIENT_SETTINGS_PATH";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _path;

    public ClientSettingsStore()
        : this(ResolvePath())
    {
    }

    public ClientSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public string FilePath => _path;

    public static string ResolvePath()
    {
        var overridePath = Environment.GetEnvironmentVariable(PathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Frog",
            "client-settings.json");
    }

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                var fresh = new UserSettings();
                fresh.Normalize();
                return fresh;
            }

            var json = File.ReadAllText(_path);
            var parsed = JsonSerializer.Deserialize<UserSettings>(json, SerializerOptions);
            if (parsed is null)
            {
                var fresh = new UserSettings();
                fresh.Normalize();
                return fresh;
            }

            parsed.Normalize();
            return parsed;
        }
        catch
        {
            var fresh = new UserSettings();
            fresh.Normalize();
            return fresh;
        }
    }

    /// <summary>Écriture atomique : fichier temporaire + <see cref="File.Replace"/> / Move.</summary>
    public void Save(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        try
        {
            if (File.Exists(_path))
            {
                File.Replace(tmp, _path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tmp, _path);
            }
        }
        finally
        {
            if (File.Exists(tmp))
            {
                try
                {
                    File.Delete(tmp);
                }
                catch
                {
                    // ignore leftover temp
                }
            }
        }
    }
}
