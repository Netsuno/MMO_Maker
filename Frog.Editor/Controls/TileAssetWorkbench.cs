using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Frog.Core.Constants;
using Frog.Core.Maps;
using Frog.Editor.Assets;
using Frog.Editor.Dialogs;
using Frog.Editor.Services;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

/// <summary>
/// Catalogue global (vignettes 48×48) et tileset de travail. Le pinceau retient un
/// <see cref="TileAssetId"/>, jamais une position de palette.
/// </summary>
public sealed class TileAssetWorkbench : UserControl
{
    private readonly TileAssetCatalogue _catalogue;
    private readonly CheckBox _resize;
    private readonly NumericUpDown _sourceCell;
    private readonly TextBox _search;
    private readonly TileAssetThumbGrid _grid;
    private readonly ComboBox _sets;
    private readonly ListBox _palette;
    private readonly Label _status;
    private readonly TileAssetFlagsPanel _flags;
    private bool _suspend;

    public event Action<TileAssetId>? BrushTileChosen;

    public TileAssetWorkbench(TileAssetCatalogue catalogue)
    {
        _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
        Dock = DockStyle.Fill;
        BackColor = EditorChrome.SidebarBg;
        Font = EditorChrome.BodyFont;
        ForeColor = EditorChrome.LabelPrimary;
        _flags = new TileAssetFlagsPanel(_catalogue);

        var import = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 78,
            WrapContents = true,
            Padding = new Padding(8, 6, 8, 0),
            BackColor = EditorChrome.SidebarBg,
        };
        var importButton = new Button { Text = "Importer une feuille…", AutoSize = true, Margin = new Padding(0, 0, 8, 4) };
        EditorChrome.StyleDialogButton(importButton, primary: true);
        importButton.Click += (_, _) => PromptImport();
        _resize = new CheckBox
        {
            Text = "Redimensionner explicitement vers 48 px",
            AutoSize = true,
            ForeColor = EditorChrome.LabelPrimary,
            Margin = new Padding(0, 6, 8, 0),
        };
        _sourceCell = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 512,
            Value = WorldMetrics.DefaultTileSizePixels,
            Width = 64,
            Enabled = false,
            Margin = new Padding(0, 2, 0, 0),
        };
        var cellLabel = new Label
        {
            Text = "Cellule source",
            AutoSize = true,
            ForeColor = EditorChrome.LabelMuted,
            Margin = new Padding(0, 6, 4, 0),
        };
        _resize.CheckedChanged += (_, _) => _sourceCell.Enabled = _resize.Checked;
        import.Controls.Add(importButton);
        import.Controls.Add(_resize);
        import.Controls.Add(cellLabel);
        import.Controls.Add(_sourceCell);

        _search = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Rechercher un id…" };
        var searchHost = new Panel { Dock = DockStyle.Top, Height = 28, Padding = new Padding(8, 2, 8, 2) };
        searchHost.Controls.Add(_search);
        _search.TextChanged += (_, _) => RefreshGrid();

        _grid = new TileAssetThumbGrid { Dock = DockStyle.Fill };
        _grid.ImageProvider = id => TileAssetThumbnails.Get(_catalogue, id);
        _grid.FlagsProvider = id => _catalogue.GetFlags(id);
        _grid.ModeProvider = () => _flags.Mode;
        _flags.ModeChanged += () =>
        {
            if (!IsDisposed)
            {
                _grid.Invalidate();
            }
        };
        _grid.TileChosen += id =>
        {
            BrushTileChosen?.Invoke(id);
            SetStatus(ShortId(id));
            _flags.Bind(id);
        };
        _grid.TileEdited += (id, localX, localY) => _flags.ApplyClick(id, localX, localY);
        _grid.TileActivated += id => AddSelected(id);
        var catalogueHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 8, 4) };
        var catalogueBanner = EditorChrome.BuildZoneBanner("Catalogue");
        catalogueHost.Controls.Add(_grid);
        catalogueHost.Controls.Add(catalogueBanner);

        _sets = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Margin = new Padding(0, 2, 6, 0) };
        EditorChrome.StyleSidebarComboBox(_sets);
        _sets.SelectedIndexChanged += (_, _) =>
        {
            if (_suspend || _sets.SelectedItem is not string name)
            {
                return;
            }

            _catalogue.TrySelectWorkingTileset(name);
            RefreshPalette();
        };
        var newSet = SmallButton("Nouveau");
        newSet.Click += (_, _) => CreateSet();
        var rename = SmallButton("Renommer");
        rename.Click += (_, _) => RenameSet();
        var setBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 32,
            WrapContents = false,
            Padding = new Padding(8, 2, 8, 0),
        };
        setBar.Controls.Add(_sets);
        setBar.Controls.Add(newSet);
        setBar.Controls.Add(rename);

        _palette = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            BackColor = EditorChrome.SidebarElevated,
            ForeColor = EditorChrome.LabelPrimary,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _palette.SelectedIndexChanged += (_, _) =>
        {
            if (_suspend || _catalogue.ActiveWorkingTileset is not { } set)
            {
                return;
            }

            var index = _palette.SelectedIndex;
            if ((uint)index >= (uint)set.Tiles.Count)
            {
                return;
            }

            var id = set.Tiles[index];
            _grid.SelectId(id);
            BrushTileChosen?.Invoke(id);
            _flags.Bind(id);
        };

        var add = SmallButton("Ajouter");
        add.Click += (_, _) => AddSelected(_grid.SelectedId);
        var remove = SmallButton("Retirer");
        remove.Click += (_, _) => RemoveSelected();
        var up = SmallButton("Monter");
        up.Click += (_, _) => MoveSelected(-1);
        var down = SmallButton("Descendre");
        down.Click += (_, _) => MoveSelected(1);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            WrapContents = false,
            Padding = new Padding(8, 2, 8, 2),
        };
        actions.Controls.Add(add);
        actions.Controls.Add(remove);
        actions.Controls.Add(up);
        actions.Controls.Add(down);

        _status = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            ForeColor = EditorChrome.LabelMuted,
            Padding = new Padding(8, 0, 8, 4),
            Text = "Catalogue vide. Importez une feuille déjà en cellules 48×48.",
        };

        var working = new Panel { Dock = DockStyle.Bottom, Height = 196, Padding = new Padding(0) };
        var paletteHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 8, 0) };
        paletteHost.Controls.Add(_palette);
        working.Controls.Add(paletteHost);
        working.Controls.Add(actions);
        working.Controls.Add(setBar);
        working.Controls.Add(EditorChrome.BuildZoneBanner("Tileset de travail"));

        Controls.Add(catalogueHost);
        Controls.Add(_flags);
        Controls.Add(working);
        Controls.Add(_status);
        Controls.Add(searchHost);
        Controls.Add(import);

        _catalogue.Changed += RefreshAll;
        _catalogue.EnsureDefaultWorkingTileset();
        RefreshAll();
    }

    public void BeginAutotileEdit()
    {
        _flags.SelectAutotileMode();
        if (!_grid.SelectedId.IsNone)
        {
            _flags.Bind(_grid.SelectedId);
            return;
        }

        if (_catalogue.Ids.Count > 0)
        {
            _flags.Bind(_catalogue.Ids[0]);
        }
    }

    public void PromptImport()
    {
        var path = EditorTestHooks.OverrideImportSourcePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Images|*.png;*.bmp;*.jpg;*.jpeg;*.gif",
                Title = "Importer une feuille TileAsset",
            };
            var owner = FindForm();
            if (owner is null || dialog.ShowDialog(owner) != DialogResult.OK)
            {
                return;
            }

            path = dialog.FileName;
        }

        ImportFromPath(path);
    }

    public void ImportFromPath(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            int width;
            int height;
            byte[] rgba;
            if (!TileAssetPngCodec.TryDecode(bytes, out width, out height, out rgba))
            {
                using var stream = new MemoryStream(bytes, writable: false);
                using var bitmap = new Bitmap(stream);
                width = bitmap.Width;
                height = bitmap.Height;
                rgba = TileAssetThumbnails.BitmapToStraightRgba(bitmap);
            }

            var options = new TileImportOptions
            {
                ExplicitResize = _resize.Checked,
                SourceCellPixels = (int)_sourceCell.Value,
            };
            var result = _catalogue.ImportStraightRgba(rgba, width, height, options);
            var resizeNote = result.ExplicitResizeApplied
                ? $" Redimensionnement explicite (cellule {_sourceCell.Value} → {TileAssetMetrics.TargetTileSizePixels})."
                : string.Empty;
            var leftover = result.DiscardedRightPixels + result.DiscardedBottomPixels > 0
                ? $" Reliquat ignoré : {result.DiscardedRightPixels}×{result.DiscardedBottomPixels} px."
                : string.Empty;
            SetStatus(
                $"Import : {result.UniqueAdded} nouvelle(s), {result.UniqueAlreadyPresent} déjà connue(s). "
                + $"Grille {result.Columns}×{result.Rows}. Catalogue {result.CatalogueCount}.{resizeNote}{leftover}");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or NotSupportedException)
        {
            SetStatus(ex.Message);
            var owner = FindForm();
            if (owner is not null)
            {
                MessageBox.Show(owner, ex.Message, "Import TileAsset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    public void SelectTile(TileAssetId id)
    {
        _grid.SelectId(id);
        _flags.Bind(id);
        if (_catalogue.ActiveWorkingTileset is { } set)
        {
            var index = set.Tiles.IndexOf(id);
            if (index >= 0)
            {
                _suspend = true;
                _palette.SelectedIndex = index;
                _suspend = false;
            }
        }
    }

    private void AddSelected(TileAssetId id)
    {
        if (id.IsNone)
        {
            SetStatus("Choisissez une tuile du catalogue.");
            return;
        }

        if (!_catalogue.TryAddToActiveWorkingTileset(id, out var error))
        {
            SetStatus(error ?? "Ajout impossible.");
            return;
        }

        SetStatus($"Ajoutée au tileset « {_catalogue.ActiveWorkingTileset?.Name} ».");
    }

    private void RemoveSelected()
    {
        if (!_catalogue.TryRemoveFromActiveWorkingTileset(_palette.SelectedIndex, out var error))
        {
            SetStatus(error ?? "Retrait impossible.");
        }
    }

    private void MoveSelected(int delta)
    {
        var from = _palette.SelectedIndex;
        if (!_catalogue.TryMoveInActiveWorkingTileset(from, from + delta, out var error))
        {
            SetStatus(error ?? "Déplacement impossible.");
            return;
        }

        _suspend = true;
        _palette.SelectedIndex = from + delta;
        _suspend = false;
    }

    private void CreateSet()
    {
        var owner = FindForm();
        if (owner is null)
        {
            return;
        }

        var name = SimpleInputDialog.Show(owner, "Tileset de travail", "Nom", "Nouveau tileset");
        if (name is null)
        {
            return;
        }

        if (!_catalogue.TryCreateWorkingTileset(name, out var error))
        {
            SetStatus(error ?? "Création impossible.");
        }
    }

    private void RenameSet()
    {
        var current = _catalogue.ActiveWorkingTileset?.Name ?? string.Empty;
        var owner = FindForm();
        if (owner is null)
        {
            return;
        }

        var name = SimpleInputDialog.Show(owner, "Renommer", "Nom", current);
        if (name is null)
        {
            return;
        }

        if (!_catalogue.TryRenameActiveWorkingTileset(name, out var error))
        {
            SetStatus(error ?? "Renommage impossible.");
        }
    }

    private void RefreshAll()
    {
        if (IsDisposed)
        {
            return;
        }

        RefreshGrid();
        RefreshSets();
        RefreshPalette();
        _flags.Bind(_grid.SelectedId);
    }

    private void RefreshGrid()
    {
        _grid.Tiles = _catalogue.Search(_search.Text);
    }

    private void RefreshSets()
    {
        _suspend = true;
        _sets.Items.Clear();
        foreach (var set in _catalogue.WorkingTilesets)
        {
            _sets.Items.Add(set.Name);
        }

        if (_catalogue.ActiveWorkingTileset is { } active)
        {
            _sets.SelectedItem = active.Name;
        }

        _suspend = false;
    }

    private void RefreshPalette()
    {
        _suspend = true;
        var keep = _palette.SelectedIndex;
        _palette.Items.Clear();
        if (_catalogue.ActiveWorkingTileset is { } set)
        {
            for (var i = 0; i < set.Tiles.Count; i++)
            {
                _palette.Items.Add($"{i + 1,3}.  {ShortId(set.Tiles[i])}");
            }

            if ((uint)keep < (uint)_palette.Items.Count)
            {
                _palette.SelectedIndex = keep;
            }
        }

        _suspend = false;
    }

    private void SetStatus(string text) => _status.Text = text;

    private static string ShortId(TileAssetId id)
    {
        var hex = id.ToHex();
        return hex.Length <= 12 ? hex : hex[..12] + "…";
    }

    private static Button SmallButton(string text)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(0, 0, 6, 0) };
        EditorChrome.StyleDialogButton(button, primary: false);
        return button;
    }
}

/// <summary>Grille de vignettes 48×48. La sélection est un <see cref="TileAssetId"/>.</summary>
internal sealed class TileAssetThumbGrid : Control
{
    private const int Thumb = TileAssetMetrics.TargetTileSizePixels;
    private const int Gap = 4;
    private const int Cell = Thumb + Gap;
    private readonly VScrollBar _scroll = new() { Dock = DockStyle.Right, Width = 16 };

    public TileAssetThumbGrid()
    {
        DoubleBuffered = true;
        BackColor = EditorChrome.PaletteStripBg;
        _scroll.ValueChanged += (_, _) => Invalidate();
        Controls.Add(_scroll);
        Resize += (_, _) => UpdateScroll();
    }

    public IReadOnlyList<TileAssetId> Tiles
    {
        get => _tiles;
        set
        {
            _tiles = value ?? Array.Empty<TileAssetId>();
            if (!_tiles.Contains(SelectedId))
            {
                SelectedId = _tiles.Count > 0 ? _tiles[0] : TileAssetId.None;
            }

            UpdateScroll();
            Invalidate();
        }
    }

    private IReadOnlyList<TileAssetId> _tiles = Array.Empty<TileAssetId>();

    public TileAssetId SelectedId { get; private set; }

    public Func<TileAssetId, Image?>? ImageProvider { get; set; }

    public Func<TileAssetId, TileAssetFlags>? FlagsProvider { get; set; }

    public Func<TileFlagEditMode>? ModeProvider { get; set; }

    public event Action<TileAssetId>? TileChosen;

    /// <summary>Clic dans les 48×48 de la vignette (le trou de 4 px ne compte pas).</summary>
    public event Action<TileAssetId, int, int>? TileEdited;

    public event Action<TileAssetId>? TileActivated;

    public void SelectId(TileAssetId id)
    {
        if (id.IsNone || !_tiles.Contains(id))
        {
            return;
        }

        SelectedId = id;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && TryHit(e.X, e.Y, out var id, out var localX, out var localY, out var inThumb))
        {
            SelectedId = id;
            Invalidate();
            TileChosen?.Invoke(id);
            if (inThumb)
            {
                TileEdited?.Invoke(id, localX, localY);
            }
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left && TryHit(e.X, e.Y, out var id, out _, out _, out _))
        {
            TileActivated?.Invoke(id);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);
        var columns = Columns();
        if (columns <= 0 || _tiles.Count == 0)
        {
            return;
        }

        var firstRow = _scroll.Value / Cell;
        var lastRow = Math.Min((_tiles.Count + columns - 1) / columns, firstRow + (Height / Cell) + 2);
        for (var row = firstRow; row < lastRow; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var index = (row * columns) + column;
                if ((uint)index >= (uint)_tiles.Count)
                {
                    return;
                }

                var id = _tiles[index];
                var x = column * Cell;
                var y = (row * Cell) - _scroll.Value;
                var dest = new Rectangle(x, y, Thumb, Thumb);
                var image = ImageProvider?.Invoke(id);
                if (image is not null)
                {
                    e.Graphics.DrawImage(image, dest);
                }
                else
                {
                    using var missing = new SolidBrush(Color.FromArgb(70, 74, 86));
                    e.Graphics.FillRectangle(missing, dest);
                }

                if (id == SelectedId)
                {
                    using var pen = new Pen(Color.FromArgb(255, 182, 72), 2);
                    e.Graphics.DrawRectangle(pen, dest);
                }

                PaintMode(e.Graphics, dest, FlagsProvider?.Invoke(id) ?? TileAssetFlags.Default);
            }
        }
    }

    private void PaintMode(Graphics graphics, Rectangle dest, TileAssetFlags flags)
    {
        var mode = ModeProvider?.Invoke() ?? TileFlagEditMode.PassageGlobal;
        if (mode == TileFlagEditMode.PassageFourDirections)
        {
            PaintDirections(graphics, dest, flags);
            return;
        }

        if (mode == TileFlagEditMode.Bush && flags.Bush)
        {
            using var shade = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
            var half = dest.Height / 2;
            graphics.FillRectangle(shade, dest.Left, dest.Top + half, dest.Width, dest.Height - half);
        }

        var text = TileFlagEdit.OverlayText(flags, mode);
        if (string.IsNullOrEmpty(text) || mode == TileFlagEditMode.Bush)
        {
            return;
        }

        using var font = new Font(FontFamily.GenericSansSerif, 14f, FontStyle.Bold, GraphicsUnit.Pixel);
        var size = graphics.MeasureString(text, font);
        var x = dest.Left + ((dest.Width - size.Width) / 2f);
        var y = dest.Top + ((dest.Height - size.Height) / 2f);
        graphics.DrawString(text, font, Brushes.Black, x + 1f, y + 1f);
        graphics.DrawString(text, font, Brushes.White, x, y);
    }

    private static void PaintDirections(Graphics graphics, Rectangle dest, TileAssetFlags flags)
    {
        const int arm = 5;
        var midX = dest.Left + (dest.Width / 2);
        var midY = dest.Top + (dest.Height / 2);
        DrawArrow(graphics, flags.PassageNorth, new[]
        {
            new Point(midX, dest.Top + 2),
            new Point(midX - arm, dest.Top + 2 + arm),
            new Point(midX + arm, dest.Top + 2 + arm),
        });
        DrawArrow(graphics, flags.PassageSouth, new[]
        {
            new Point(midX, dest.Bottom - 3),
            new Point(midX - arm, dest.Bottom - 3 - arm),
            new Point(midX + arm, dest.Bottom - 3 - arm),
        });
        DrawArrow(graphics, flags.PassageWest, new[]
        {
            new Point(dest.Left + 2, midY),
            new Point(dest.Left + 2 + arm, midY - arm),
            new Point(dest.Left + 2 + arm, midY + arm),
        });
        DrawArrow(graphics, flags.PassageEast, new[]
        {
            new Point(dest.Right - 3, midY),
            new Point(dest.Right - 3 - arm, midY - arm),
            new Point(dest.Right - 3 - arm, midY + arm),
        });
    }

    private static void DrawArrow(Graphics graphics, bool open, Point[] points)
    {
        var shadow = new Point[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            shadow[i] = new Point(points[i].X + 1, points[i].Y + 1);
        }

        graphics.FillPolygon(Brushes.Black, shadow);
        using var brush = new SolidBrush(open ? Color.White : Color.FromArgb(180, 40, 32));
        graphics.FillPolygon(brush, points);
    }

    private int Columns() => Math.Max(1, Math.Max(1, ClientSize.Width - _scroll.Width) / Cell);

    private void UpdateScroll()
    {
        var columns = Columns();
        var rows = (_tiles.Count + columns - 1) / columns;
        var content = Math.Max(0, rows * Cell);
        _scroll.Enabled = content > Height;
        _scroll.Maximum = Math.Max(0, content);
        _scroll.LargeChange = Math.Max(1, Height);
        _scroll.SmallChange = Cell;
        if (_scroll.Value > Math.Max(0, content - Height))
        {
            _scroll.Value = Math.Max(0, content - Height);
        }
    }

    private bool TryHit(int x, int y, out TileAssetId id, out int localX, out int localY, out bool inThumb)
    {
        id = TileAssetId.None;
        localX = 0;
        localY = 0;
        inThumb = false;
        var columns = Columns();
        if (x < 0 || y < 0 || x >= columns * Cell)
        {
            return false;
        }

        var column = x / Cell;
        var row = (y + _scroll.Value) / Cell;
        var index = (row * columns) + column;
        if ((uint)index >= (uint)_tiles.Count || row < 0)
        {
            return false;
        }

        localX = x - (column * Cell);
        localY = (y + _scroll.Value) - (row * Cell);
        inThumb = localX < Thumb && localY < Thumb;
        id = _tiles[index];
        return true;
    }
}
