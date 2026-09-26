using System.ComponentModel;
using System.Runtime.CompilerServices;
using Frog.Core.Enums;
using Frog.Core.Maps;

namespace Frog.Editor.Panels;

/// <summary>Ligne de liste des couches (binding WPF).</summary>
public sealed class LayerListRow : INotifyPropertyChanged
{
    private bool _visible;
    private bool _locked;
    private bool _isPaintTarget;
    private int _opacityPercent = 100;
    private string _lockLabel = string.Empty;

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
            OnPropertyChanged(nameof(VisibilityHint));
        }
    }

    public string VisibilityHint => Visible
        ? "Visible — cliquer pour masquer cette couche"
        : "Masquée — cliquer pour l’afficher";

    public bool Locked
    {
        get => _locked;
        set
        {
            if (_locked == value)
            {
                return;
            }

            _locked = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LockHint));
        }
    }

    public string LockHint => LayerTypeLabels.LockHint(Locked);

    public bool IsPaintTarget
    {
        get => _isPaintTarget;
        set
        {
            if (_isPaintTarget == value)
            {
                return;
            }

            _isPaintTarget = value;
            OnPropertyChanged();
        }
    }

    /// <summary>0–100. Aperçu éditeur seulement.</summary>
    public int OpacityPercent
    {
        get => _opacityPercent;
        set
        {
            var next = Math.Clamp(value, 0, 100);
            if (_opacityPercent == next)
            {
                return;
            }

            _opacityPercent = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OpacityCaption));
        }
    }

    public string OpacityCaption => LayerPreviewOpacity.PercentCaption(OpacityPercent);

    public string OpacityHint => LayerTypeLabels.OpacityHint;

    public string Display { get; set; } = string.Empty;

    /// <summary>Rang dans la pile et rôle, sous le nom.</summary>
    public string OrderHint { get; set; } = string.Empty;

    public string EngineType { get; set; } = string.Empty;

    public string LockLabel
    {
        get => _lockLabel;
        set
        {
            if (_lockLabel == value)
            {
                return;
            }

            _lockLabel = value;
            OnPropertyChanged();
        }
    }

    public string StripCaption { get; set; } = string.Empty;

    public string StripHint { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
