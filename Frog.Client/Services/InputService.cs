using System.Windows.Forms;
using Frog.Client.Config;

namespace Frog.Client.Services;

/// <summary>
/// Disposition AZERTY (ZQSD+E) / QWERTY (WASD+E) + flèches toujours actives.
/// Rebind persisté via <see cref="UserSettings"/>.
/// </summary>
public sealed class InputService
{
    private Keys _moveUp = Keys.Z;
    private Keys _moveDown = Keys.S;
    private Keys _moveLeft = Keys.Q;
    private Keys _moveRight = Keys.D;
    private Keys _interact = Keys.E;
    private Keys _attack = Keys.Space;

    public KeyboardLayoutPreset Preset { get; private set; } = KeyboardLayoutPreset.Azerty;

    public Keys MoveUp => _moveUp;

    public Keys MoveDown => _moveDown;

    public Keys MoveLeft => _moveLeft;

    public Keys MoveRight => _moveRight;

    public Keys Interact => _interact;

    public Keys Attack => _attack;

    public void Apply(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();
        Preset = settings.KeyboardPreset;
        _moveUp = ParseKey(settings.Bindings.MoveUp, Preset == KeyboardLayoutPreset.Qwerty ? Keys.W : Keys.Z);
        _moveDown = ParseKey(settings.Bindings.MoveDown, Keys.S);
        _moveLeft = ParseKey(settings.Bindings.MoveLeft, Preset == KeyboardLayoutPreset.Qwerty ? Keys.A : Keys.Q);
        _moveRight = ParseKey(settings.Bindings.MoveRight, Keys.D);
        _interact = ParseKey(settings.Bindings.Interact, Keys.E);
        _attack = ParseKey(settings.Bindings.Attack, Keys.Space);
    }

    public bool IsMoveLeft(Keys key) => key == Keys.Left || key == _moveLeft;

    public bool IsMoveRight(Keys key) => key == Keys.Right || key == _moveRight;

    public bool IsMoveUp(Keys key) => key == Keys.Up || key == _moveUp;

    public bool IsMoveDown(Keys key) => key == Keys.Down || key == _moveDown;

    public bool IsInteract(Keys key) => key == _interact;

    public bool IsAttack(Keys key) => key == _attack;

    public bool IsMovementOrInteract(Keys key) =>
        IsMoveLeft(key) || IsMoveRight(key) || IsMoveUp(key) || IsMoveDown(key) || IsInteract(key) || IsAttack(key);

    public static bool IsTextInputFocus(Control? control)
    {
        for (var c = control; c is not null; c = c.Parent)
        {
            if (c is TextBoxBase)
            {
                return true;
            }

            if (c is ComboBox combo && combo.DropDownStyle != ComboBoxStyle.DropDownList)
            {
                return true;
            }
        }

        return false;
    }

    public static Keys ParseKey(string? name, Keys fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        if (Enum.TryParse(name.Trim(), ignoreCase: true, out Keys parsed)
            && parsed is not Keys.None and not Keys.Modifiers)
        {
            return parsed;
        }

        return fallback;
    }

    public static string KeyDisplayName(Keys key)
    {
        return key switch
        {
            Keys.Left => "←",
            Keys.Right => "→",
            Keys.Up => "↑",
            Keys.Down => "↓",
            Keys.Space => "Espace",
            _ => key.ToString(),
        };
    }
}
