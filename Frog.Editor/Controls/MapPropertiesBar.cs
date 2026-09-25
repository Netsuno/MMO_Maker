using System.Drawing;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Core.Models;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

/// <summary>Résumé des propriétés de carte dans la colonne de droite, et accès au dialogue.</summary>
internal sealed class MapPropertiesBar : UserControl
{
    private readonly Label _summary;

    public event EventHandler? EditRequested;

    public MapPropertiesBar()
    {
        Height = 156;
        MinimumSize = new Size(0, 156);
        BackColor = EditorChrome.SidebarBg;
        Padding = new Padding(8, 4, 8, 6);

        var title = new Label
        {
            Text = "PROPRIÉTÉS",
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = EditorChrome.LabelMuted,
            BackColor = EditorChrome.SidebarBg,
            Font = EditorChrome.CaptionFont,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _summary = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = EditorChrome.LabelPrimary,
            BackColor = EditorChrome.SidebarBg,
            Font = EditorChrome.BodyFont,
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = false,
        };
        var edit = new Button
        {
            Text = "Modifier…",
            Dock = DockStyle.Bottom,
            Height = 30,
        };
        EditorChrome.StyleDialogButton(edit, primary: true);
        edit.Click += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);

        Controls.Add(_summary);
        Controls.Add(edit);
        Controls.Add(title);
        Bind(null, null);
    }

    public void Bind(Map? map, Point? playtestSpawn)
    {
        _summary.Text = map is null
            ? "Aucune carte."
            : MapEditOperations.FormatPropertiesSummary(map, playtestSpawn?.X, playtestSpawn?.Y);
    }

    internal string SummaryForTest => _summary.Text;

    internal string EditButtonTextForTest
    {
        get
        {
            foreach (Control child in Controls)
            {
                if (child is Button button)
                {
                    return button.Text;
                }
            }

            return string.Empty;
        }
    }
}
