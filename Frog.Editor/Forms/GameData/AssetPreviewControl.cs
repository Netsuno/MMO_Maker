using System.IO;
using Frog.Application.Assets;
using Frog.Editor.Assets;

namespace Frog.Editor.Forms.GameData;

/// <summary>Aperçu visuel d’un asset logique (PNG) avec placeholder et libération des ressources.</summary>
public sealed class AssetPreviewControl : UserControl
{
    private readonly PictureBox _picture = new()
    {
        Dock = DockStyle.Fill,
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = Color.FromArgb(40, 40, 40),
    };
    private readonly Label _caption = new()
    {
        Dock = DockStyle.Bottom,
        Height = 18,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Color.Gainsboro,
    };
    private Image? _loadedImage;
    private bool _binding;

    public AssetPreviewControl()
    {
        Width = 128;
        Height = 128;
        Controls.Add(_picture);
        Controls.Add(_caption);
        SetPlaceholder("Aperçu");
    }

    public string AssetRoot { get; set; } = ProjectAssetRoot.Resolve();

    public string? LogicalPath
    {
        get => _logicalPath;
        set
        {
            _logicalPath = value;
            if (!_binding)
            {
                RefreshPreview();
            }
        }
    }

    private string? _logicalPath;

    public AssetPreviewState PreviewState { get; private set; } = AssetPreviewState.Placeholder;

    public void SetLogicalPathSilently(string? logicalPath)
    {
        _binding = true;
        try
        {
            _logicalPath = logicalPath;
            RefreshPreview();
        }
        finally
        {
            _binding = false;
        }
    }

    public void RefreshPreview()
    {
        ReleaseImage();
        var resolved = ProjectAssetPathResolver.TryResolve(AssetRoot, _logicalPath);
        switch (resolved.Status)
        {
            case ProjectAssetPathResolver.ResolveStatus.Success:
                try
                {
                    using var stream = File.OpenRead(resolved.AbsolutePath!);
                    _loadedImage = Image.FromStream(stream);
                    _picture.Image = _loadedImage;
                    PreviewState = AssetPreviewState.Loaded;
                    _caption.Text = Path.GetFileName(resolved.AbsolutePath!);
                }
                catch
                {
                    SetPlaceholder("Image illisible");
                    PreviewState = AssetPreviewState.Corrupt;
                }

                break;
            case ProjectAssetPathResolver.ResolveStatus.NotFound:
                SetPlaceholder("Fichier manquant");
                PreviewState = AssetPreviewState.Missing;
                break;
            case ProjectAssetPathResolver.ResolveStatus.TraversalRejected:
                SetPlaceholder("Chemin refusé");
                PreviewState = AssetPreviewState.Rejected;
                break;
            default:
                SetPlaceholder("Aperçu");
                PreviewState = AssetPreviewState.Placeholder;
                break;
        }
    }

    private void SetPlaceholder(string text)
    {
        _picture.Image = null;
        _caption.Text = text;
    }

    private void ReleaseImage()
    {
        _picture.Image = null;
        _loadedImage?.Dispose();
        _loadedImage = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReleaseImage();
        }

        base.Dispose(disposing);
    }
}

public enum AssetPreviewState
{
    Placeholder,
    Loaded,
    Missing,
    Corrupt,
    Rejected,
}
