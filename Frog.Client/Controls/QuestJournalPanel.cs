using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Client.Controls;

/// <summary>Journal quêtes (<see cref="Frog.Core.Enums.PacketId.QuestJournalSnapshot"/>) + turn-in.</summary>
public sealed class QuestJournalPanel : UserControl
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Button _btnTurnIn = new() { Text = "Rendre quête", AutoSize = true, Enabled = false };
    private IReadOnlyList<QuestJournalEntryWire> _entries = Array.Empty<QuestJournalEntryWire>();

    public event Action<Guid>? TurnInRequested;

    public QuestJournalPanel()
    {
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };
        top.Controls.Add(_btnTurnIn);
        Controls.Add(_list);
        Controls.Add(top);
        _btnTurnIn.Click += (_, _) =>
        {
            if (_list.SelectedItem is QuestRow row)
            {
                TurnInRequested?.Invoke(row.QuestId);
            }
        };
        _list.SelectedIndexChanged += (_, _) => UpdateTurnInButton();
    }

    public void ApplySnapshot(IReadOnlyList<QuestJournalEntryWire> entries)
    {
        _entries = entries;
        var selectedQuest = (_list.SelectedItem as QuestRow)?.QuestId;
        _list.Items.Clear();
        foreach (var entry in entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
        {
            _list.Items.Add(new QuestRow(entry));
        }

        if (_list.Items.Count > 0)
        {
            var restore = selectedQuest is Guid prev
                ? _list.Items.Cast<QuestRow>().ToList().FindIndex(r => r.QuestId == prev)
                : -1;
            _list.SelectedIndex = restore >= 0 ? restore : 0;
        }

        UpdateTurnInButton();
    }

    private void UpdateTurnInButton()
    {
        _btnTurnIn.Enabled = _list.SelectedItem is QuestRow row
                             && row.Status == CharacterQuestStatus.ReadyToTurnIn;
    }

    internal int EntryCountForTest => _list.Items.Count;

    internal void SelectFirstForTest()
    {
        if (_list.Items.Count > 0)
        {
            _list.SelectedIndex = 0;
        }
    }

    internal void ClickTurnInForTest() => _btnTurnIn.PerformClick();

    private sealed class QuestRow(QuestJournalEntryWire entry)
    {
        public Guid QuestId { get; } = entry.QuestId;

        public CharacterQuestStatus Status { get; } = (CharacterQuestStatus)entry.Status;

        public override string ToString()
        {
            var status = Status switch
            {
                CharacterQuestStatus.Active => "En cours",
                CharacterQuestStatus.ReadyToTurnIn => "Prête",
                CharacterQuestStatus.Completed => "Terminée",
                _ => Status.ToString(),
            };
            var objectives = entry.Objectives.Count == 0
                ? entry.StageDescription
                : string.Join("; ", entry.Objectives.Select(o =>
                    o.Completed ? $"✓ {o.Description}" : $"{o.Description} ({o.Current}/{o.Required})"));
            return $"{entry.Name} [{status}] — {objectives}";
        }
    }
}
