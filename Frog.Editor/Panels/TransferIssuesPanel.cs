using System.Drawing;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Editor.Ui;

namespace Frog.Editor.Panels;

/// <summary>Liste des warps et téléportations à corriger, dans le chrome sombre de l’éditeur.</summary>
internal sealed class TransferIssuesPanel : UserControl
{
    private readonly Label _title;
    private readonly Label _empty;
    private readonly ListBox _list;
    private bool _binding;

    public TransferIssuesPanel()
    {
        Height = 132;
        MinimumSize = new Size(0, 96);
        BackColor = EditorChrome.SidebarBg;
        ForeColor = EditorChrome.LabelPrimary;
        Padding = new Padding(8, 6, 8, 8);

        _title = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Text = "Transferts",
            Font = EditorChrome.SectionFont,
            ForeColor = EditorChrome.LabelMuted,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _empty = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Aucun problème. Les warps et les téléportations d’événements sont cohérents.",
            Font = EditorChrome.BodyFont,
            ForeColor = EditorChrome.LabelMuted,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 4, 0, 0),
        };

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            Visible = false,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
            BackColor = EditorChrome.SidebarElevated,
            ForeColor = EditorChrome.LabelPrimary,
            Font = EditorChrome.BodyFont,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 40,
        };
        _list.DrawItem += OnDrawItem;
        _list.SelectedIndexChanged += (_, _) =>
        {
            if (_binding || _list.SelectedItem is not MapTransferIssue issue)
            {
                return;
            }

            IssueActivated?.Invoke(issue);
        };

        var rule = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = Color.FromArgb(70, 74, 86),
        };

        Controls.Add(_title);
        Controls.Add(rule);
        Controls.Add(_empty);
        Controls.Add(_list);
    }

    public event Action<MapTransferIssue>? IssueActivated;

    public void SetIssues(IReadOnlyList<MapTransferIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        _binding = true;
        try
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var issue in issues)
            {
                _list.Items.Add(issue);
            }

            _list.EndUpdate();
            var count = issues.Count;
            _title.Text = count == 0 ? "Transferts" : count == 1 ? "Transferts · 1 alerte" : $"Transferts · {count} alertes";
            _title.ForeColor = count == 0 ? EditorChrome.LabelMuted : EditorChrome.WarningAmber;
            _empty.Visible = count == 0;
            _list.Visible = count > 0;
        }
        finally
        {
            _binding = false;
        }
    }

    private static void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox list || e.Index < 0 || list.Items[e.Index] is not MapTransferIssue issue)
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) != 0;
        var back = selected ? Color.FromArgb(72, 58, 36) : EditorChrome.SidebarElevated;
        using (var brush = new SolidBrush(back))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        var kindColor = issue.Kind switch
        {
            MapTransferIssueKind.MissingMap => Color.FromArgb(255, 140, 120),
            MapTransferIssueKind.OutOfBounds => EditorChrome.WarningAmber,
            MapTransferIssueKind.BlockedTile => Color.FromArgb(255, 120, 96),
            MapTransferIssueKind.NotPublished => Color.FromArgb(160, 190, 255),
            _ => EditorChrome.LabelMuted,
        };
        var kindRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 3, e.Bounds.Width - 12, 14);
        var messageRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 18, e.Bounds.Width - 12, e.Bounds.Height - 20);
        TextRenderer.DrawText(
            e.Graphics,
            issue.KindLabel,
            EditorChrome.SectionFont,
            kindRect,
            kindColor,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(
            e.Graphics,
            issue.Message,
            EditorChrome.BodyFont,
            messageRect,
            EditorChrome.LabelPrimary,
            TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}
