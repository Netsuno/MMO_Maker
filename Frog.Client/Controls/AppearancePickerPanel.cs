#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Frog.Client.UI;
using Frog.Core.Gameplay;

namespace Frog.Client.Controls;

/// <summary>
/// Sélecteur Corps / Cheveux / Tunique à la création.
/// Aperçu paperdoll (planches déjà en jeu, teintes procédurales). Clic ou flèches.
/// Pas un champ de protocole.
/// </summary>
public sealed class AppearancePickerPanel : UserControl
{
    public const int PreviewScale = 2;

    private readonly PreviewView _preview = new();
    private readonly SlotRow[] _rows;
    private readonly Label _hint;
    private int _focusSlot;
    private CharacterLook _look = CharacterLook.CreateDraft;

    public AppearancePickerPanel()
    {
        AccessibleName = "Apparence";
        TabStop = true;
        BackColor = Color.Transparent;
        ForeColor = UiTheme.TextPrimary;
        var width = LoginShell.CharacterCardWidth - (LoginShell.CardPadding * 2);
        Size = new Size(width, 104);
        MinimumSize = Size;
        MaximumSize = new Size(width, 104);

        var title = new Label
        {
            Text = "Apparence",
            AutoSize = true,
            Font = UiTheme.UiFont(9f, FontStyle.Bold),
            ForeColor = UiTheme.TextPrimary,
            BackColor = Color.Transparent,
            Location = new Point(96, 2),
        };
        _hint = new Label
        {
            Text = "Clic ou flèches",
            AutoSize = true,
            Font = UiTheme.UiFont(8f),
            ForeColor = UiTheme.TextMuted,
            BackColor = Color.Transparent,
        };
        _rows = new SlotRow[3];
        for (var i = 0; i < _rows.Length; i++)
        {
            var slot = (CharacterLookSlot)i;
            var row = new SlotRow(slot);
            row.PreviousClicked += () => NudgeSlot(slot, -1);
            row.NextClicked += () => NudgeSlot(slot, +1);
            row.ValueClicked += () => NudgeSlot(slot, +1);
            row.GotFocus += (_, _) => _focusSlot = i;
            _rows[i] = row;
        }

        Controls.Add(_preview);
        Controls.Add(title);
        Controls.Add(_hint);
        foreach (var row in _rows)
        {
            Controls.Add(row);
        }

        Layout += (_, _) => PlaceChildren(title);
        ApplyLook(CharacterLook.CreateDraft, raise: false);
        HighlightSlot();
    }

    public event Action<CharacterLook>? LookChanged;

    public CharacterLook Look => _look;

    public void SetLook(CharacterLook look) => ApplyLook(look, raise: false);

    public void Nudge(Keys key)
    {
        switch (key)
        {
            case Keys.Up:
                _focusSlot = (_focusSlot + _rows.Length - 1) % _rows.Length;
                HighlightSlot();
                _rows[_focusSlot].Focus();
                break;
            case Keys.Down:
                _focusSlot = (_focusSlot + 1) % _rows.Length;
                HighlightSlot();
                _rows[_focusSlot].Focus();
                break;
            case Keys.Left:
                NudgeSlot((CharacterLookSlot)_focusSlot, -1);
                break;
            case Keys.Right:
                NudgeSlot((CharacterLookSlot)_focusSlot, +1);
                break;
        }
    }

    internal string ValueForTest(CharacterLookSlot slot) => _rows[(int)slot].ValueText;

    internal Button NextButtonForTest(CharacterLookSlot slot) => _rows[(int)slot].NextButton;

    internal Button PreviousButtonForTest(CharacterLookSlot slot) => _rows[(int)slot].PreviousButton;

    internal void ClickNextForTest(CharacterLookSlot slot) => _rows[(int)slot].NextButton.PerformClick();

    internal void ClickPreviousForTest(CharacterLookSlot slot) => _rows[(int)slot].PreviousButton.PerformClick();

    internal Bitmap RenderPreviewForTest() => _preview.RenderForTest();

    internal bool HandleKeyForTest(Keys key)
    {
        var message = Message.Create(Handle, 0x100, (IntPtr)key, IntPtr.Zero);
        return ProcessCmdKey(ref message, key);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        if (ContainsFocus && key is Keys.Left or Keys.Right or Keys.Up or Keys.Down)
        {
            Nudge(key);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void NudgeSlot(CharacterLookSlot slot, int delta)
    {
        _focusSlot = (int)slot;
        HighlightSlot();
        ApplyLook(_look.Cycle(slot, delta), raise: true);
    }

    private void ApplyLook(CharacterLook look, bool raise)
    {
        _look = look.Normalized();
        foreach (var row in _rows)
        {
            row.SetValue(_look.Label(row.Slot));
        }

        var appearance = new PaperdollOverlaySet(
            Tunic: _look.WearsTunic,
            Armor: false,
            Hat: false,
            Weapon: false);
        _preview.SetAppearance(appearance, _look);
        if (raise)
        {
            LookChanged?.Invoke(_look);
        }
    }

    private void HighlightSlot()
    {
        for (var i = 0; i < _rows.Length; i++)
        {
            _rows[i].SetActive(i == _focusSlot);
        }
    }

    private void PlaceChildren(Label title)
    {
        _preview.Location = new Point(4, 8);
        var x = _preview.Right + 8;
        title.Location = new Point(x, 2);
        _hint.Location = new Point(Math.Max(x, Width - _hint.Width - 4), 4);
        var y = title.Bottom + 2;
        var rowWidth = Math.Max(120, Width - x - 4);
        foreach (var row in _rows)
        {
            row.SetBounds(x, y, rowWidth, 22);
            y += 24;
        }
    }

    private sealed class PreviewView : Panel
    {
        private PaperdollOverlaySet _appearance;
        private CharacterLook _look;

        public PreviewView()
        {
            var edge = (PlayerWorldAssets.NativeSize * PreviewScale) + 8;
            Size = new Size(edge, edge);
            MinimumSize = Size;
            MaximumSize = Size;
            BackColor = UiTheme.BgSlot;
            AccessibleName = "Aperçu apparence";
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
                true);
        }

        public void SetAppearance(PaperdollOverlaySet appearance, CharacterLook look)
        {
            _appearance = appearance;
            _look = look;
            Invalidate();
        }

        public Bitmap RenderForTest()
        {
            var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            PaintPreview(g, ClientRectangle);
            return bmp;
        }

        protected override void OnPaint(PaintEventArgs e) => PaintPreview(e.Graphics, ClientRectangle);

        private void PaintPreview(Graphics g, Rectangle bounds)
        {
            using (var fill = new SolidBrush(UiTheme.BgSlot))
            {
                g.FillRectangle(fill, bounds);
            }

            if (bounds.Width > 2 && bounds.Height > 2)
            {
                using var pen = new Pen(UiTheme.AccentGold);
                g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            }

            var frame = PlayerWorldAssets.FrameFor(PlayerSpritePose.IdleDown, _appearance, _look);
            var size = Math.Min(
                PlayerWorldAssets.NativeSize * PreviewScale,
                Math.Max(1, Math.Min(bounds.Width - 4, bounds.Height - 4)));
            var dest = new Rectangle(
                bounds.X + ((bounds.Width - size) / 2),
                bounds.Y + ((bounds.Height - size) / 2),
                size,
                size);
            var prevInterp = g.InterpolationMode;
            var prevSmooth = g.SmoothingMode;
            var prevOffset = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            try
            {
                g.DrawImage(frame, dest, new Rectangle(0, 0, frame.Width, frame.Height), GraphicsUnit.Pixel);
            }
            finally
            {
                g.InterpolationMode = prevInterp;
                g.SmoothingMode = prevSmooth;
                g.PixelOffsetMode = prevOffset;
            }
        }
    }

    private sealed class SlotRow : Panel
    {
        private readonly Label _title;
        private readonly Label _value;
        private readonly Button _previous;
        private readonly Button _next;

        public SlotRow(CharacterLookSlot slot)
        {
            Slot = slot;
            Height = 22;
            BackColor = Color.Transparent;
            TabStop = true;
            AccessibleName = CharacterLook.Title(slot);

            _title = new Label
            {
                Text = CharacterLook.Title(slot),
                AutoSize = false,
                Size = new Size(72, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                BackColor = Color.Transparent,
                Font = UiTheme.UiFont(8f, FontStyle.Bold),
            };
            _previous = MakeButton("‹", $"{CharacterLook.Title(slot)}, précédent");
            _value = new Label
            {
                AutoSize = false,
                Size = new Size(108, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = UiTheme.TextGold,
                BackColor = Color.Transparent,
                Font = UiTheme.UiFont(8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            _next = MakeButton("›", $"{CharacterLook.Title(slot)}, suivant");
            _previous.Click += (_, _) => PreviousClicked?.Invoke();
            _next.Click += (_, _) => NextClicked?.Invoke();
            _value.Click += (_, _) => ValueClicked?.Invoke();
            Controls.Add(_title);
            Controls.Add(_previous);
            Controls.Add(_value);
            Controls.Add(_next);
            Resize += (_, _) => Place();
        }

        public event Action? PreviousClicked;

        public event Action? NextClicked;

        public event Action? ValueClicked;

        public CharacterLookSlot Slot { get; }

        public string ValueText => _value.Text;

        public Button NextButton => _next;

        public Button PreviousButton => _previous;

        public void SetValue(string text)
        {
            _value.Text = text;
            _value.AccessibleName = $"{CharacterLook.Title(Slot)} : {text}";
        }

        public void SetActive(bool active)
        {
            BackColor = active ? UiTheme.BgRowSelected : Color.Transparent;
        }

        private void Place()
        {
            _title.Location = new Point(2, 0);
            _previous.Location = new Point(_title.Right, 0);
            _value.Location = new Point(_previous.Right + 2, 0);
            var valueWidth = Math.Max(72, Width - _value.Left - _next.Width - 4);
            _value.Width = valueWidth;
            _next.Location = new Point(_value.Right + 2, 0);
        }

        private static Button MakeButton(string text, string accessibleName)
        {
            var button = new Button
            {
                Text = text,
                AccessibleName = accessibleName,
                Size = new Size(28, 22),
                TabStop = true,
                Margin = new Padding(0),
            };
            UiTheme.StyleButton(button);
            button.Font = UiTheme.UiFont(9f, FontStyle.Bold);
            button.PreviewKeyDown += (_, e) =>
            {
                if (e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down)
                {
                    e.IsInputKey = true;
                }
            };
            return button;
        }
    }
}
