using System;
using System.IO;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// P10 editor: sync-over-async on the WinForms UI thread deadlocks when
/// <c>ExecuteAsync(...).ConfigureAwait(false).GetAwaiter().GetResult()</c>
/// starts on that SynchronizationContext (Shown / OpenMap → LoadPlacementsForMap).
/// </summary>
public sealed class Phase10EditorSyncOverAsyncTests
{
    [Fact]
    public void MapEventsPostgreSqlService_LoadAndMutate_UseTaskRunOffUi()
    {
        var path = Path.Combine(RepoRoot(), "Frog.Editor", "Services", "MapEventsPostgreSqlService.cs");
        var text = File.ReadAllText(path);

        Assert.Contains("RunOffUiSyncContext", text, StringComparison.Ordinal);
        Assert.Contains("Task.Run(work)", text, StringComparison.Ordinal);
        Assert.Contains("LoadPlacementsForMap", text, StringComparison.Ordinal);
        Assert.Contains("WaitAsync often", text, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "}).ConfigureAwait(false).GetAwaiter().GetResult()",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ListSummariesAsync().ConfigureAwait(false).GetAwaiter().GetResult()",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "DeleteAsync(eventId).ConfigureAwait(false).GetAwaiter().GetResult()",
            text,
            StringComparison.Ordinal);
    }

    private static string RepoRoot()
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

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
