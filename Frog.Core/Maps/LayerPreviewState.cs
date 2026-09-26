namespace Frog.Core.Maps;

/// <summary>
/// Opacités d’aperçu, indexées comme les couches de la carte ouverte.
/// L’état vit dans l’éditeur : il n’est pas copié dans l’historique ni dans le fichier.
/// </summary>
public sealed class LayerPreviewState
{
    private float[] _opacity = [];

    public bool DimOthers { get; set; }

    public int Count => _opacity.Length;

    public void Fit(int layerCount)
    {
        if (layerCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layerCount));
        }

        if (_opacity.Length == layerCount)
        {
            return;
        }

        var next = new float[layerCount];
        Array.Fill(next, LayerPreviewOpacity.Opaque);
        var keep = Math.Min(_opacity.Length, layerCount);
        if (keep > 0)
        {
            Array.Copy(_opacity, next, keep);
        }

        _opacity = next;
    }

    public void Reset(int layerCount)
    {
        if (layerCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layerCount));
        }

        _opacity = new float[layerCount];
        if (layerCount > 0)
        {
            Array.Fill(_opacity, LayerPreviewOpacity.Opaque);
        }

        DimOthers = false;
    }

    public void SetOpacity(int index, float opacity)
    {
        if ((uint)index >= (uint)_opacity.Length)
        {
            return;
        }

        _opacity[index] = LayerPreviewOpacity.Clamp(opacity);
    }

    public float Opacity(int index)
    {
        if ((uint)index >= (uint)_opacity.Length)
        {
            return LayerPreviewOpacity.Opaque;
        }

        return _opacity[index];
    }

    public float DrawAlpha(int index, bool visible, int activeIndex) =>
        LayerPreviewOpacity.DrawAlpha(visible, index == activeIndex, Opacity(index), DimOthers);
}
