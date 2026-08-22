using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Garde-fous de frontières (Phase 1 / Task 3). Basés sur le graphe réel, pas sur la structure PRD encore absente.
/// </summary>
public sealed class ArchitectureBoundaryTests
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

    private static readonly string[] FrogProjects =
    [
        "Frog.Core",
        "Frog.Legacy",
        "Frog.Client",
        "Frog.Editor",
        "Frog.Server",
        "Frog.Tests",
    ];

    private static readonly HashSet<string> ForbiddenCoreAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "MySqlConnector",
        "Npgsql",
        "Microsoft.EntityFrameworkCore",
        "System.Windows.Forms",
        "PresentationFramework",
        "PresentationCore",
    };

    [Fact]
    public void FrogCore_HasNoProjectReferences()
    {
        var refs = GetProjectReferences("Frog.Core");
        Assert.Empty(refs);
    }

    [Fact]
    public void FrogLegacy_ReferencesOnlyCore()
    {
        var refs = GetProjectReferences("Frog.Legacy");
        Assert.Equal(new[] { "Frog.Core" }, refs);
    }

    [Fact]
    public void FrogCore_DoesNotReferenceForbiddenAssemblies()
    {
        var coreAsm = typeof(Frog.Core.Models.Map).Assembly;
        var referenced = coreAsm.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        var hits = referenced.Where(name => ForbiddenCoreAssemblies.Contains(name)).ToArray();
        Assert.True(hits.Length == 0, "Frog.Core référence des assemblages interdits: " + string.Join(", ", hits));
    }

    [Fact]
    public void Solution_HasNoCircularProjectReferences()
    {
        var graph = FrogProjects.ToDictionary(
            p => p,
            p => GetProjectReferences(p).Where(r => FrogProjects.Contains(r)).ToList(),
            StringComparer.Ordinal);

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Dfs(string node, List<string> stack)
        {
            if (visiting.Contains(node))
            {
                throw new InvalidOperationException(
                    "Dépendance circulaire détectée: " + string.Join(" -> ", stack) + " -> " + node);
            }

            if (!visited.Add(node))
            {
                return;
            }

            visiting.Add(node);
            stack.Add(node);
            foreach (var next in graph[node])
            {
                Dfs(next, stack);
            }

            stack.RemoveAt(stack.Count - 1);
            visiting.Remove(node);
        }

        foreach (var project in FrogProjects)
        {
            Dfs(project, []);
        }
    }

    [Fact]
    public void EditorUiSurfaces_DoNotConstructMySqlOrDbContext()
    {
        var editorRoot = Path.Combine(RepoRoot, "Frog.Editor");
        var files = new List<string>();
        files.AddRange(Directory.GetFiles(Path.Combine(editorRoot, "Forms"), "*.cs", SearchOption.AllDirectories));
        var mainWindow = Path.Combine(editorRoot, "MainWindow.xaml.cs");
        if (File.Exists(mainWindow))
        {
            files.Add(mainWindow);
        }

        Assert.NotEmpty(files);

        var pattern = new Regex(@"\b(new\s+MySqlConnection\b|DbContext\b|NpgsqlConnection\b)", RegexOptions.CultureInvariant);
        var offenders = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            if (pattern.IsMatch(text))
            {
                offenders.Add(Path.GetRelativePath(RepoRoot, file));
            }
        }

        Assert.True(offenders.Count == 0, "UI éditeur contient un accès DB direct: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Client_DoesNotReferenceDatabasePackages()
    {
        var csproj = Path.Combine(RepoRoot, "Frog.Client", "Frog.Client.csproj");
        var xml = XDocument.Load(csproj);
        var packages = xml.Descendants("PackageReference")
            .Select(e => (string?)e.Attribute("Include") ?? "")
            .Where(s => s.Length > 0)
            .ToArray();

        Assert.DoesNotContain(packages, p => p.Equals("MySqlConnector", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(packages, p => p.Equals("Npgsql", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(packages, p => p.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> GetProjectReferences(string projectName)
    {
        var csproj = Path.Combine(RepoRoot, projectName, projectName + ".csproj");
        Assert.True(File.Exists(csproj), "csproj manquant: " + csproj);

        var xml = XDocument.Load(csproj);
        return xml.Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include") ?? "")
            .Where(s => s.Length > 0)
            .Select(s => Path.GetFileNameWithoutExtension(s.Replace('\\', Path.DirectorySeparatorChar)))
            .ToList();
    }
}
