using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Frog.Editor.Panels;

/// <summary>Ligne de liste des couches (binding WPF).</summary>
public sealed class LayerListRow : INotifyPropertyChanged
{
    private bool _visible;

    public int Index { get; init; }

    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value)
            {
                return;
            }

            _visible = value;
            OnPropertyChanged();
        }
    }

    public string Display { get; set; } = string.Empty;

    public string EngineType { get; set; } = string.Empty;

    public string LockLabel { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
