using System;
using System.Windows.Forms;
using Frog.Core.Protocol;

namespace Frog.Client.Controls;

/// <summary>Dialogue actif : texte, choix, envoi <see cref="Frog.Core.Enums.PacketId.DialogueChoiceRequest"/>.</summary>
public sealed class DialoguePanel : UserControl
{
    private readonly Label _lblSpeaker = new() { AutoSize = true, Text = "—", Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold) };
    private readonly Label _lblText = new() { AutoSize = true, MaximumSize = new System.Drawing.Size(320, 0), Text = "Aucun dialogue actif." };
    private readonly FlowLayoutPanel _choicesFlow = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
    };
    private DialogueStateWire? _state;

    public event Action<byte[], string>? ChoiceRequested;

    public DialoguePanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(4),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(_lblSpeaker, 0, 0);
        layout.Controls.Add(_lblText, 0, 1);
        layout.Controls.Add(_choicesFlow, 0, 2);
        Controls.Add(layout);
    }

    public void ApplyState(DialogueStateWire? state)
    {
        _state = state;
        _choicesFlow.Controls.Clear();
        if (state is null)
        {
            _lblSpeaker.Text = "—";
            _lblText.Text = "Aucun dialogue actif.";
            return;
        }

        _lblSpeaker.Text = string.IsNullOrWhiteSpace(state.Speaker) ? "???" : state.Speaker;
        _lblText.Text = string.IsNullOrWhiteSpace(state.Text) ? "…" : state.Text;
        foreach (var choice in state.Choices)
        {
            if (string.IsNullOrWhiteSpace(choice.ChoiceId))
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(choice.Label) ? choice.ChoiceId : choice.Label;
            var btn = new Button
            {
                Text = label,
                AutoSize = true,
                Tag = choice.ChoiceId,
                Margin = new Padding(0, 0, 0, 4),
            };
            btn.Click += OnChoiceClick;
            _choicesFlow.Controls.Add(btn);
        }
    }

    public void ClearDialogue() => ApplyState(null);

    private void OnChoiceClick(object? sender, EventArgs e)
    {
        if (_state is null || sender is not Button btn || btn.Tag is not string choiceId)
        {
            return;
        }

        if (_state.SessionToken.Length != Phase8Wire.DialogueSessionTokenBytes)
        {
            return;
        }

        ChoiceRequested?.Invoke(_state.SessionToken, choiceId);
    }

    internal string SpeakerTextForTest => _lblSpeaker.Text;

    internal int ChoiceButtonCountForTest => _choicesFlow.Controls.Count;

    internal void ClickFirstChoiceForTest()
    {
        if (_choicesFlow.Controls.Count > 0 && _choicesFlow.Controls[0] is Button btn)
        {
            btn.PerformClick();
        }
    }
}
