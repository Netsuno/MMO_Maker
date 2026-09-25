using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Menus éditeur : plus d’entrée MariaDB / MySQL / SQL Server.
/// Le chemin produit reste PostgreSQL (enregistrement, publication, événements) et le mémo local (départ, entités).
/// </summary>
public sealed class EditorMariaDbMenuScrubTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("Impossible de localiser Frog.Creator.sln depuis " + AppContext.BaseDirectory);
        }
    }

    [Fact]
    public void EditorMenus_DoNotAdvertiseMariaDbMySqlOrSqlServer()
    {
        var files = new[]
        {
            Path.Combine(RepoRoot, "Frog.Editor", "MainWindow.xaml"),
            Path.Combine(RepoRoot, "Frog.Editor", "MainWindow.xaml.cs"),
            Path.Combine(RepoRoot, "Frog.Editor", "Forms", "MainForm.cs"),
        };

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("MariaDB", text, StringComparison.Ordinal);
            Assert.DoesNotContain("MySQL", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SQL Server", text, StringComparison.OrdinalIgnoreCase);
        }

        var xaml = File.ReadAllText(files[0]);
        var winForms = File.ReadAllText(files[2]);
        Assert.Contains("Enregistrer (PostgreSQL)", xaml, StringComparison.Ordinal);
        Assert.Contains("Publier (PostgreSQL)…", xaml, StringComparison.Ordinal);
        Assert.Contains("Outil point de départ", xaml, StringComparison.Ordinal);
        Assert.Contains("Outil entités", xaml, StringComparison.Ordinal);
        Assert.Contains("Enregistrer (PostgreSQL)", winForms, StringComparison.Ordinal);
        Assert.Contains("Outil point de départ (D)", winForms, StringComparison.Ordinal);
        Assert.Contains("Outil entités (N)", winForms, StringComparison.Ordinal);
        Assert.Contains("Événements carte…", xaml, StringComparison.Ordinal);
        Assert.Contains("Actualiser marqueurs événements", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void EditorProject_DoesNotReferenceMySqlConnector()
    {
        var csproj = Path.Combine(RepoRoot, "Frog.Editor", "Frog.Editor.csproj");
        var xml = XDocument.Load(csproj);
        var packages = xml.Descendants("PackageReference")
            .Select(e => (string?)e.Attribute("Include") ?? "")
            .ToArray();

        Assert.DoesNotContain(packages, p => p.Equals("MySqlConnector", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MariaDbMenuOnlyEditorFiles_AreGone()
    {
        string[] gone =
        [
            Path.Combine(RepoRoot, "Frog.Editor", "Forms", "PublishMapDialog.cs"),
            Path.Combine(RepoRoot, "Frog.Editor", "Config", "EditorMariaDbConfig.cs"),
            Path.Combine(RepoRoot, "Frog.Editor", "Services", "MariaMapBlobPublisher.cs"),
            Path.Combine(RepoRoot, "Frog.Editor", "Services", "MapPublishNaming.cs"),
            Path.Combine(RepoRoot, "Frog.Editor", "Services", "MapEventsMariaDbReader.cs"),
            Path.Combine(RepoRoot, "Frog.Editor", "Services", "MapEventsMariaDbWriter.cs"),
        ];

        foreach (var path in gone)
        {
            Assert.False(File.Exists(path), path);
        }

        Assert.True(File.Exists(Path.Combine(RepoRoot, "Frog.Editor", "Services", "MapEventMarkerView.cs")));
        Assert.True(File.Exists(Path.Combine(RepoRoot, "Frog.Editor", "Services", "MapEventsPostgreSqlService.cs")));
    }
}
