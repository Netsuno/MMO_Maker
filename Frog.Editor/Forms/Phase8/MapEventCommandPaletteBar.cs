using Frog.Core.Events;
using Frog.Editor.Ui;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Boutons français pour insérer texte, choix, audio, boutique, interrupteur, variable et branche.</summary>
internal sealed class MapEventCommandPaletteBar : UserControl
{
    public MapEventCommandPaletteBar()
    {
        AutoSize = true;
        var flow = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            MaximumSize = new Size(900, 0),
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        flow.Controls.Add(new Label
        {
            Text = "Palette",
            AutoSize = true,
            Font = EditorChrome.CaptionFont,
            Margin = new Padding(0, 6, 8, 0),
        });
        foreach (var entry in MapEventCommandPalette.Entries)
        {
            var button = new Button
            {
                Text = entry.Label,
                AutoSize = true,
                Margin = new Padding(0, 0, 4, 4),
                Tag = entry.Id,
            };
            button.Click += (_, _) => EntryChosen?.Invoke(entry.Id);
            flow.Controls.Add(button);
        }

        Controls.Add(flow);
    }

    public event Action<string>? EntryChosen;
}
