#nullable enable
using System.Drawing;
using System.Windows.Forms;
using Frog.Core.Protocol;

namespace Frog.Client.UI;

/// <summary>Tracker HD — 1–2 lignes, même snapshot que <c>QuestJournalPanel</c>.</summary>
public sealed class HudQuestTrackerModule : HudModulePanel
{
    private readonly Label _title = new()
    {
        AutoSize = false,
        Dock = DockStyle.Top,
        Height = 20,
        ForeColor = UiTheme.TextGold,
        Font = UiTheme.UiFont(9f, FontStyle.Bold),
        Text = "Aucune quête",
    };
    private readonly Label _obj1 = new() { AutoSize = false, Dock = DockStyle.Top, Height = 16, ForeColor = UiTheme.TextPrimary, Text = "—" };
    private readonly Label _obj2 = new() { AutoSize = false, Dock = DockStyle.Top, Height = 16, ForeColor = UiTheme.TextSecondary, Text = string.Empty };

    public HudQuestTrackerModule()
        : base("Quête")
    {
        Size = new Size(180, 76);
        MinimumSize = new Size(140, 64);
        Controls.Add(_obj2);
        Controls.Add(_obj1);
        Controls.Add(_title);
        _title.BringToFront();
    }

    internal string TitleTextForTest => _title.Text;

    internal string Objective1ForTest => _obj1.Text;

    public void ApplySnapshot(IReadOnlyList<QuestJournalEntryWire>? entries)
    {
        if (entries is null || entries.Count == 0)
        {
            _title.Text = "Aucune quête";
            _obj1.Text = "—";
            _obj2.Text = string.Empty;
            return;
        }

        var entry = entries.FirstOrDefault(e => e.Status == 1) ?? entries[0];
        _title.Text = string.IsNullOrWhiteSpace(entry.Name) ? "Quête" : entry.Name;
        var objectives = entry.Objectives;
        _obj1.Text = FormatObjective(objectives, 0) ?? (string.IsNullOrWhiteSpace(entry.StageDescription) ? "—" : entry.StageDescription);
        _obj2.Text = FormatObjective(objectives, 1) ?? string.Empty;
    }

    private static string? FormatObjective(IReadOnlyList<QuestObjectiveProgressWire> objectives, int index)
    {
        if (objectives.Count <= index)
        {
            return null;
        }

        var o = objectives[index];
        var desc = string.IsNullOrWhiteSpace(o.Description) ? "Objectif" : o.Description;
        return o.Required > 0 ? $"{desc} ({o.Current}/{o.Required})" : desc;
    }
}
