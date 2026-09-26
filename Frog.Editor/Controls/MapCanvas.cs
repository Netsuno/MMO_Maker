using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Application.Prefabs;
using Frog.Core.Enums;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Editor.Assets;
using Frog.Editor.Enums;
using Frog.Editor.Services;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

/// <summary>
/// Canvas carte : les couches denses ne dessinent que le viewport, et un bitmap
/// des tuiles statiques amortit le défilement et le fantôme du pinceau.
/// Ctrl+C/X/V sur toutes les couches (Ctrl+Maj = couche active), undo intégré.
/// Le tracé en cours est figé au copier-coller. Les régions de rencontre (sidecar) ne suivent pas la zone.
/// </summary>
public sealed class MapCanvas : Control
{
    public readonly MapUndoController History = new();

    private const int ViewportPadTiles = 1;
    private const int LayerCachePadTiles = 12;
    private const int LayerCacheMaxEdge = 4096;

    private Bitmap? _layerCache;
    private Map? _layerCacheMap;
    private int _layerCacheTx0;
    private int _layerCacheTy0;
    private int _layerCacheTx1;
    private int _layerCacheTy1;
    private int _layerCacheTileSize;
    private int _layerCacheKey;
    private int _tilesetAnimEpoch;

    public int TileSize { get; set; } = 32;
    public float Zoom { get; private set; } = 1f;
    public PointF Pan { get; private set; } = new(0, 0);

    private Map? _map;

    /// <summary>Carte affichée ; notifier les abonnés (mini-carte) lors d’un changement d’instance.</summary>
    public Map? Map
    {
        get => _map;
        set
        {
            if (ReferenceEquals(_map, value))
            {
                return;
            }

            _map = value;
            DisposeLayerCache();
            AttachTileFlags();
            NotifyViewTransformChanged();
            Invalidate();
        }
    }

    /// <summary>Pan, zoom ou carte changés — pour synchroniser la mini-carte.</summary>
    public event Action? ViewTransformChanged;

    public int ActiveTilesetId { get; set; } = 0;
    public Point SelectedSrc { get; set; } = new(0, 0);

    /// <summary>Catalogue TileAsset. Les cartes feuille continuent d’utiliser <see cref="TilesetCache"/>.</summary>
    public TileAssetCatalogue? TileAssets
    {
        get => _tileAssets;
        set
        {
            _tileAssets = value;
            AttachTileFlags();
        }
    }

    private TileAssetCatalogue? _tileAssets;

    private void AttachTileFlags()
    {
        if (_map is not null
            && _map.GraphicIdentity == TileGraphicIdentity.TileAsset
            && _tileAssets is not null)
        {
            _map.TileFlags = _tileAssets.Flags;
        }
    }

    /// <summary>Pinceau v6. Ignoré tant que la carte n’est pas en identité TileAsset.</summary>
    public TileAssetId ActiveTileAssetId { get; set; }

    /// <summary>Numéro peint par l’outil Région. 0 efface. Le clic droit efface sans changer ce numéro.</summary>
    public byte ActiveRegionId
    {
        get => _activeRegionId;
        set => _activeRegionId = value > MapRegionDocument.MaxRegionId ? MapRegionDocument.MaxRegionId : value;
    }

    private byte _activeRegionId = 1;
    private bool _regionStroke;

    /// <summary>
    /// Vrai : peindre une tuile d’un groupe d’autotile choisit le rôle (centre, bord, coin)
    /// d’après les voisins. Faux : le pinceau pose l’id choisi tel quel.
    /// </summary>
    public bool JoinAutotiles { get; set; } = true;

    public event Action<TileAssetId>? TileAssetSampled;

    /// <summary>Tampon pinceau en tuiles (largeur × hauteur), aligné sur <see cref="SelectedSrc"/> dans le tileset.</summary>
    public Size SelectedStampInTiles { get; set; } = new(1, 1);

    public int ActiveLayerIndex { get; set; } = 0;

    /// <summary>
    /// Opacité d’aperçu et voile des autres couches. N’entre pas dans le .fmap ni dans l’annulation.
    /// </summary>
    private readonly LayerPreviewState _layerPreview = new();

    public bool DimOtherLayers
    {
        get => _layerPreview.DimOthers;
        set
        {
            if (_layerPreview.DimOthers == value)
            {
                return;
            }

            _layerPreview.DimOthers = value;
            Invalidate();
        }
    }

    public void SetLayerPreviewOpacity(int layerIndex, float opacity)
    {
        _layerPreview.Fit(Map?.Layers.Count ?? 0);
        _layerPreview.SetOpacity(layerIndex, opacity);
        Invalidate();
    }

    public float GetLayerPreviewOpacity(int layerIndex)
    {
        _layerPreview.Fit(Map?.Layers.Count ?? 0);
        return _layerPreview.Opacity(layerIndex);
    }

    /// <summary>Remet l’aperçu à opaque, sans atténuation. À appeler quand on ouvre une autre carte.</summary>
    public void ResetLayerPreview()
    {
        _layerPreview.Reset(Map?.Layers.Count ?? 0);
        Invalidate();
    }
    public event Action<Point>? HoveredTileChanged;
    public TileType SelectedTileType { get; set; } = TileType.Ground;
    public event Action<Tile?>? TileClicked;

    /// <summary>Ctrl+clic droit sur une tuile (sans gommage) — menu contextuel éditeur.</summary>
    public event Action<Point>? TileContextMenuRequested;

    /// <summary>
    /// Si défini, le clic gauche sur une tuile place l'événement rapide (PNJ, coffre, porte, auberge)
    /// et n'applique pas l'outil courant. Le rappel retourne true quand le clic est consommé.
    /// </summary>
    public Func<Point, bool>? QuickNpcPlacementClick { get; set; }
    public event Action? MapReplaced;
    public event Action? UndoHistoryChanged;
    /// <summary>Carte modifiée par une action d’édition (peinture, undo, etc.).</summary>
    public event Action? MapEdited;

    /// <summary>Pipette : le pinceau a échantillonné tileset + source depuis la carte.</summary>
    public event Action<BrushSample>? BrushSampled;

    public readonly record struct BrushSample(int TilesetId, int SrcX, int SrcY, TileType Type, bool SwitchToBrush);

    /// <summary>Carte cible par défaut pour les nouvelles tuiles warp (souvent la carte courante).</summary>
    public Guid? DefaultWarpTargetMapId { get; set; }

    /// <summary>Marqueurs événements ou visibilité overlay ont changé (mini-carte, etc.).</summary>
    public event Action? MapEventOverlayChanged;

    private bool _showMapEventMarkers = true;

    /// <summary>Affiche les losanges d’événements placés (<see cref="MapEventMarkers"/>) sur le canevas.</summary>
    public bool ShowMapEventMarkers
    {
        get => _showMapEventMarkers;
        set
        {
            if (_showMapEventMarkers == value)
            {
                return;
            }

            _showMapEventMarkers = value;
            if (!value)
            {
                _hoveredMapEventMarker = null;
            }

            MapEventOverlayChanged?.Invoke();
            MapEventMarkerInteractionChanged?.Invoke();
            Invalidate();
        }
    }

    private bool _showMapEventNames = true;

    /// <summary>Affiche le nom du type d’événement (zoom suffisant, survol ou sélection). Défaut : oui.</summary>
    public bool ShowMapEventNames
    {
        get => _showMapEventNames;
        set
        {
            if (_showMapEventNames == value)
            {
                return;
            }

            _showMapEventNames = value;
            Invalidate();
        }
    }

    private IReadOnlyList<MapEventMarkerView>? _mapEventMarkers;

    /// <summary>Marqueurs agrégés par tuile (null = aucun overlay).</summary>
    public IReadOnlyList<MapEventMarkerView>? MapEventMarkers
    {
        get => _mapEventMarkers;
        set
        {
            _mapEventMarkers = value;
            var interaction = false;
            if (_hasSelectedMapEvent && !MarkerListContainsSelection(value))
            {
                _hasSelectedMapEvent = false;
                _selectedMapEventPlacementKey = "";
                interaction = true;
            }

            if (_hoveredMapEventMarker is { } hovered && !MarkerListContains(value, hovered))
            {
                _hoveredMapEventMarker = null;
                interaction = true;
            }

            MapEventOverlayChanged?.Invoke();
            if (interaction)
            {
                MapEventMarkerInteractionChanged?.Invoke();
            }

            Invalidate();
        }
    }

    private readonly HashSet<(int X, int Y)> _transferIssueTiles = new();

    /// <summary>Tuiles source d’un warp ou d’un événement dont la destination est invalide.</summary>
    public void SetTransferIssueTiles(IEnumerable<(int X, int Y)> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        _transferIssueTiles.Clear();
        foreach (var tile in tiles)
        {
            _transferIssueTiles.Add(tile);
        }

        Invalidate();
    }

    internal bool HasTransferIssueTileForTest(int tileX, int tileY) =>
        _transferIssueTiles.Contains((tileX, tileY));

    /// <summary>Clic sur un losange ou son libellé (sélection canevas déjà appliquée).</summary>
    public event Action<MapEventMarkerView>? MapEventMarkerPicked;

    /// <summary>Survol, sélection ou visibilité des noms ont changé (barre d’état).</summary>
    public event Action? MapEventMarkerInteractionChanged;

    private MapEventMarkerView? _hoveredMapEventMarker;
    private bool _hasSelectedMapEvent;
    private int _selectedMapEventTileX;
    private int _selectedMapEventTileY;
    private string _selectedMapEventPlacementKey = "";
    private bool _mapEventMarkerGesture;

    /// <summary>Nom tronqué de l’événement survolé, sinon de l’événement sélectionné.</summary>
    public string? ActiveMapEventCaption
    {
        get
        {
            if (!ShowMapEventMarkers)
            {
                return null;
            }

            if (_hoveredMapEventMarker is { } hovered)
            {
                return MapEventMarkerLayout.FormatLabel(hovered.PrimaryDisplayName, hovered.PrimarySlug, hovered.PlacementCount);
            }

            if (_hasSelectedMapEvent && TryFindSelectedMarker(out var selected))
            {
                return MapEventMarkerLayout.FormatLabel(selected.PrimaryDisplayName, selected.PrimarySlug, selected.PlacementCount);
            }

            return null;
        }
    }

    /// <summary>Déclencheur français de l’événement survolé, sinon de l’événement sélectionné.</summary>
    public string? ActiveMapEventTriggerLabel
    {
        get
        {
            if (!ShowMapEventMarkers)
            {
                return null;
            }

            if (_hoveredMapEventMarker is { } hovered)
            {
                return MapEventMarkerLayout.TriggerLabel(hovered.PrimaryTriggerKind);
            }

            if (_hasSelectedMapEvent && TryFindSelectedMarker(out var selected))
            {
                return MapEventMarkerLayout.TriggerLabel(selected.PrimaryTriggerKind);
            }

            return null;
        }
    }

    /// <summary>Met en évidence un placement (liste événements) sans redéclencher <see cref="MapEventMarkerPicked"/>.</summary>
    public void HighlightMapEventMarker(int tileX, int tileY, string? placementKey)
    {
        _hasSelectedMapEvent = true;
        _selectedMapEventTileX = tileX;
        _selectedMapEventTileY = tileY;
        _selectedMapEventPlacementKey = placementKey ?? "";
        Invalidate();
        MapEventMarkerInteractionChanged?.Invoke();
    }

    public EditorTool ActiveTool { get; set; } = EditorTool.Brush;

    /// <summary>
    /// Pot : écrire la région sur les couches visibles et déverrouillées (pas Attributs, sauf si elle est active).
    /// Défaut faux. Ctrl+clic active aussi cette passe pour le clic en cours.
    /// </summary>
    public bool FillVisibleUnlockedLayers { get; set; }

    /// <summary>
    /// Pot : ne pas franchir une case dont la collision ou les attributs diffèrent de la graine. Défaut faux.
    /// </summary>
    public bool FillRespectAttributes { get; set; }

    /// <summary>
    /// Rectangle : ne peindre que le bord. Défaut faux (plein).
    /// Maj pendant le tracé force aussi le contour pour ce geste.
    /// </summary>
    public bool RectangleOutline { get; set; }

    /// <summary>Rectangle : ellipse inscrite dans le rectangle tracé. Défaut faux.</summary>
    public bool RectangleEllipse { get; set; }

    /// <summary>Tuile de spawn playtest / départ affichée sur le canevas (mémo éditeur).</summary>
    public Point? PlaytestSpawnTile { get; private set; }

    /// <summary>Le spawn a été posé ou restauré (UI / workstate).</summary>
    public event Action<Point>? PlaytestSpawnChanged;

    /// <summary>Type posé par le prochain clic de l’outil Entités. Défaut : PNJ.</summary>
    public MapPlacedKind PlaceKind { get; set; } = MapPlacedKind.Npc;

    private readonly List<MapPlacedEntity> _placedEntities = new();

    public IReadOnlyList<MapPlacedEntity> PlacedEntities => _placedEntities;

    public Guid? SelectedPlacedEntityId { get; private set; }

    public MapPlacedEntity? SelectedPlacedEntity
    {
        get
        {
            if (SelectedPlacedEntityId is not Guid id)
            {
                return null;
            }

            for (var i = 0; i < _placedEntities.Count; i++)
            {
                if (_placedEntities[i].Id == id)
                {
                    return _placedEntities[i];
                }
            }

            return null;
        }
    }

    /// <summary>Liste des entités posées modifiée (pose, déplacement, suppression, restauration).</summary>
    public event Action? PlacedEntitiesChanged;

    /// <summary>Sélection d’entité posée modifiée.</summary>
    public event Action? PlacedEntitySelectionChanged;

    private Guid? _draggingPlacedId;
    private Point _placedDragOrigin;
    private bool _placedDragMoved;

    /// <summary>Catalogue utilisé pour poser / dessiner les prefabs.</summary>
    public PrefabCatalog PrefabCatalog { get; set; } = BuiltInPrefabCatalog.Create();

    public string SelectedPrefabId { get; set; } = BuiltInPrefabCatalog.SofaId;

    public PrefabFacing SelectedPrefabFacing { get; set; } = PrefabFacing.South;

    private readonly List<PrefabPlacement> _prefabPlacements = new();

    public IReadOnlyList<PrefabPlacement> PrefabPlacements => _prefabPlacements;

    /// <summary>Placements prefab ajoutés, retirés ou remplacés.</summary>
    public event Action? PrefabPlacementsChanged;

    /// <summary>Pipette : l’outil prefab a copié id / facing depuis une instance.</summary>
    public event Action<string, PrefabFacing>? PrefabSelectionPicked;

    private PrefabPlacement? _selectedPrefabPlacement;
    private PrefabPlacement? _draggingPrefab;

    /// <summary>Instance posée actuellement sélectionnée, ou null si elle n’est plus sur la carte.</summary>
    public PrefabPlacement? SelectedPrefabPlacement
    {
        get
        {
            if (_selectedPrefabPlacement is null)
            {
                return null;
            }

            for (var i = 0; i < _prefabPlacements.Count; i++)
            {
                if (ReferenceEquals(_prefabPlacements[i], _selectedPrefabPlacement))
                {
                    return _selectedPrefabPlacement;
                }
            }

            _selectedPrefabPlacement = null;
            return null;
        }
    }
    private bool _prefabDragMoved;

    private bool _panning;
    private Point _lastMouse;
    private bool _paintStroke;
    private Point? _rectPaintOrigin;
    private Point? _linePaintOrigin;
    private Point _hoverTile;

    /// <summary>Dernière tuile sous le curseur (coordonnées carte).</summary>
    public Point HoveredTile => _hoverTile;
    private Point? _selectionMarqueeAnchor;
    private Rectangle? _committedSelectionTiles;

    /// <summary>Bloque le gommage au clic droit tant que le bouton n’est pas relâché (Ctrl+clic droit = menu).</summary>
    private bool _suppressRightButtonErase;

    public MapCanvas()
    {
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = EditorChrome.MapCanvasBg;
        Cursor = Cursors.Cross;
        Dock = DockStyle.Fill;

        MouseWheel += OnMouseWheelZoom;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        MouseLeave += (_, _) => ClearMapEventMarkerHover();
        KeyDown += (_, e) => RefreshShapePreview(e.KeyCode);
        KeyUp += (_, e) => RefreshShapePreview(e.KeyCode);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TilesetAnimCatalog.Changed -= OnTilesetAnimChanged;
        TilesetAnimCatalog.PreviewFrameChanged -= OnTilesetAnimChanged;
        TilesetAnimCatalog.Changed += OnTilesetAnimChanged;
        TilesetAnimCatalog.PreviewFrameChanged += OnTilesetAnimChanged;
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        TilesetAnimCatalog.Changed -= OnTilesetAnimChanged;
        TilesetAnimCatalog.PreviewFrameChanged -= OnTilesetAnimChanged;
        base.OnHandleDestroyed(e);
    }

    private void OnTilesetAnimChanged()
    {
        _tilesetAnimEpoch++;
        if (IsHandleCreated)
        {
            Invalidate();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeLayerCache();
        }

        base.Dispose(disposing);
    }

    /// <summary>Le geste ligne / rectangle a changé (départ, Maj, annulation).</summary>
    public event Action? PaintGestureChanged;

    /// <summary>Coin haut-gauche et coin bas-droit visibles, en coordonnées « monde » (pixels carte avant zoom).</summary>
    public void GetViewportWorldBounds(out PointF topLeft, out PointF bottomRight)
    {
        var c = ClientSize;
        topLeft = ScreenToWorld(Point.Empty);
        bottomRight = ScreenToWorld(new Point(c.Width, c.Height));
    }

    /// <summary>Tuiles visibles (indices carte), avec marge <see cref="ViewportPadTiles"/>.</summary>
    public void GetViewportTileBounds(out int tx0, out int ty0, out int tx1, out int ty1)
        => ComputeVisibleTileRange(out tx0, out ty0, out tx1, out ty1);

    /// <summary>Centre la vue sur le centre de la tuile (<paramref name="tileX"/>, <paramref name="tileY"/>).</summary>
    public void CenterViewOnTile(int tileX, int tileY)
    {
        if (Map is null)
        {
            return;
        }

        tileX = Math.Clamp(tileX, 0, Map.Width - 1);
        tileY = Math.Clamp(tileY, 0, Map.Height - 1);
        var wx = tileX * TileSize + TileSize * 0.5f;
        var wy = tileY * TileSize + TileSize * 0.5f;
        var cx = ClientSize.Width * 0.5f;
        var cy = ClientSize.Height * 0.5f;
        Pan = new PointF(cx - wx * Zoom, cy - wy * Zoom);
        NotifyViewTransformChanged();
        Invalidate();
    }

    private void NotifyViewTransformChanged() => ViewTransformChanged?.Invoke();

    /// <summary>Zoom 100 % et coin haut-gauche de la carte aligné sur le coin haut-gauche du canevas.</summary>
    public void ResetViewTransform()
    {
        Zoom = 1f;
        Pan = new PointF(0f, 0f);
        NotifyViewTransformChanged();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        NotifyViewTransformChanged();
    }

    public bool HasCommittedSelection => _committedSelectionTiles is { Width: > 0, Height: > 0 };

    public void ClearSelection()
    {
        _selectionMarqueeAnchor = null;
        _committedSelectionTiles = null;
        NotifyPaintGesture();
    }

    /// <summary>
    /// Copie la zone figée, ou le rectangle encore sous le pointeur.
    /// Toutes les couches par défaut. Les numéros de région restent en place : le sidecar
    /// n’entre pas dans l’annulation des tuiles, et la table de rencontres n’est pas un rectangle.
    /// </summary>
    public bool TryCopyTileSelection(bool activeLayerOnly = false)
    {
        if (Map is null)
        {
            return false;
        }

        FreezeLiveSelection();
        if (!TryGetCommittedSelectionNormalized(out var rect))
        {
            return false;
        }

        if (activeLayerOnly)
        {
            EditorTileClipboard.CopyFromLayer(Map, ActiveLayerIndex, rect);
        }
        else
        {
            EditorTileClipboard.CopyAllLayers(Map, rect);
        }

        return EditorTileClipboard.HasContent;
    }

    public bool TryCutTileSelection(bool activeLayerOnly = false)
    {
        if (Map is null)
        {
            return false;
        }

        FreezeLiveSelection();
        if (!TryGetCommittedSelectionNormalized(out var rect))
        {
            return false;
        }

        var only = activeLayerOnly ? ActiveLayerIndex : (int?)null;
        if (!MapEditOperations.HasEditableTilesInRect(Map, rect.X, rect.Y, rect.Width, rect.Height, only))
        {
            return false;
        }

        if (!TryCopyTileSelection(activeLayerOnly))
        {
            return false;
        }

        BeginEditTransaction();
        EraseSelection(rect, activeLayerOnly);
        Invalidate();
        return true;
    }

    public bool TryPasteAtHover(bool activeLayerOnly = false)
    {
        if (Map is null || !TryGetPasteFootprint(_hoverTile.X, _hoverTile.Y, out _))
        {
            return false;
        }

        var singleLayer = activeLayerOnly || EditorTileClipboard.IsSingleLayer;
        if (singleLayer)
        {
            if (!IsActiveLayerEditable())
            {
                return false;
            }

            if (!EditorTileClipboard.IsSingleLayer && !EditorTileClipboard.CapturesLayer(ActiveLayerIndex))
            {
                return false;
            }

            BeginEditTransaction();
            EnsureLayerExists();
            var result = EditorTileClipboard.PasteToLayer(
                Map,
                ActiveLayerIndex,
                _hoverTile.X,
                _hoverTile.Y,
                Map.Width,
                Map.Height);
            Invalidate();
            RaiseTileClicked(_hoverTile.X, _hoverTile.Y);
            return result.Changed;
        }

        if (!EditorTileClipboard.CanPasteAllLayers(Map))
        {
            return false;
        }

        BeginEditTransaction();
        var pasted = EditorTileClipboard.PasteAllLayers(Map, _hoverTile.X, _hoverTile.Y, Map.Width, Map.Height);
        Invalidate();
        RaiseTileClicked(_hoverTile.X, _hoverTile.Y);
        return pasted.Changed;
    }

    internal bool TryGetCommittedSelectionBounds(out Rectangle rect)
        => TryGetCommittedSelectionNormalized(out rect);

    /// <summary>
    /// Pose un modèle à l’ancre (coin haut-gauche). Les tuiles passent par l’annulation
    /// déjà en place. Les prefabs restent hors de cette pile, comme une pose directe.
    /// </summary>
    public bool TryApplyMapTemplate(
        MapStampTemplate template,
        int anchorX,
        int anchorY,
        out string? status,
        out string? error)
    {
        status = null;
        if (Map is null)
        {
            error = "Aucune carte chargée.";
            return false;
        }

        if (!MapStampTemplateOperations.TryValidateForStamp(Map, template, anchorX, anchorY, out error))
        {
            return false;
        }

        var tiles = MapStampTemplateOperations.ApplyTiles(Map, template, anchorX, anchorY, BeginEditTransaction);
        var placed = 0;
        var skipped = 0;
        foreach (var prefab in MapStampTemplateOperations.PrefabsAt(template, anchorX, anchorY))
        {
            if (PrefabPlacementService.TryPlace(
                    _prefabPlacements,
                    PrefabCatalog,
                    prefab.PrefabId,
                    prefab.Facing,
                    prefab.TileX,
                    prefab.TileY,
                    Map.Width,
                    Map.Height,
                    out _,
                    out _))
            {
                placed++;
            }
            else
            {
                skipped++;
            }
        }

        if (!tiles.Changed && placed == 0)
        {
            error = MapStampTemplateOperations.FormatRejected(tiles, skipped);
            return false;
        }

        if (placed > 0)
        {
            PrefabPlacementsChanged?.Invoke();
        }

        Invalidate();
        status = MapStampTemplateOperations.FormatStamped(template, anchorX, anchorY, tiles, placed, skipped);
        error = null;
        return true;
    }

    public bool TryDeleteSelectedTiles(bool activeLayerOnly = false)
    {
        if (Map is null)
        {
            return false;
        }

        FreezeLiveSelection();
        if (!TryGetCommittedSelectionNormalized(out var rect))
        {
            return false;
        }

        var only = activeLayerOnly ? ActiveLayerIndex : (int?)null;
        if (!MapEditOperations.HasEditableTilesInRect(Map, rect.X, rect.Y, rect.Width, rect.Height, only))
        {
            return false;
        }

        BeginEditTransaction();
        EraseSelection(rect, activeLayerOnly);
        Invalidate();
        return true;
    }

    public bool HandleEditorShortcuts(Keys keyData)
    {
        var ctrl = (keyData & Keys.Control) == Keys.Control;
        var alt = (keyData & Keys.Alt) == Keys.Alt;
        var shift = (keyData & Keys.Shift) == Keys.Shift;
        var code = keyData & Keys.KeyCode;

        switch (code)
        {
            case Keys.C when ctrl && !alt:
                return TryCopyTileSelection(activeLayerOnly: shift);
            case Keys.X when ctrl && !alt:
                return TryCutTileSelection(activeLayerOnly: shift);
            case Keys.V when ctrl && !alt:
                return TryPasteAtHover(activeLayerOnly: shift);
            case Keys.V when !ctrl && !alt:
                return TryTransformSelection(TileSelectionTransformKind.MirrorVertical, activeLayerOnly: shift);
            case Keys.Delete when !ctrl && !alt:
                if (ActiveTool == EditorTool.Place && TryRemoveSelectedPlacedEntity())
                {
                    return true;
                }

                return TryDeleteSelectedTiles(activeLayerOnly: shift);
            case Keys.Q when !ctrl && !alt:
                return TryTransformSelection(TileSelectionTransformKind.Rotate90Clockwise, activeLayerOnly: shift);
            case Keys.H when !ctrl && !alt:
                return TryTransformSelection(TileSelectionTransformKind.MirrorHorizontal, activeLayerOnly: shift);
            case Keys.I when !ctrl && !alt && !shift:
                return TryPipetteAtHover(switchToBrush: true);
            case Keys.D when ctrl && !alt && !shift:
                return TryDuplicateSelectedPrefab(out _, out _);
            default:
                return false;
        }
    }

    public bool TryTransformSelection(TileSelectionTransformKind kind, bool activeLayerOnly = false)
    {
        if (Map is null)
        {
            return false;
        }

        FreezeLiveSelection();
        if (TryGetCommittedSelectionNormalized(out var rect))
        {
            var only = activeLayerOnly ? ActiveLayerIndex : (int?)null;
            if (MapEditOperations.HasEditableTilesInRect(Map, rect.X, rect.Y, rect.Width, rect.Height, only))
            {
                BeginEditTransaction();
                if (!TileSelectionService.TryTransform(Map, ActiveLayerIndex, rect, kind, out var next, activeLayerOnly))
                {
                    return false;
                }

                _committedSelectionTiles = next;
                NotifyPaintGesture();
                return true;
            }
        }

        if (!EditorTileClipboard.TryTransform(kind))
        {
            return false;
        }

        Invalidate();
        return true;
    }

    public bool TryPipetteAtHover(bool switchToBrush) => TryPipetteAt(_hoverTile.X, _hoverTile.Y, switchToBrush);

    public bool TryPipetteAt(int tileX, int tileY, bool switchToBrush)
    {
        if (Map is null)
        {
            return false;
        }

        if (!MapTilePipette.TrySample(Map, tileX, tileY, ActiveLayerIndex, out var sample))
        {
            return false;
        }

        ActiveTilesetId = sample.TilesetId;
        SelectedSrc = new Point(sample.SrcX, sample.SrcY);
        SelectedStampInTiles = new Size(1, 1);
        SelectedTileType = sample.Type;
        if (!sample.AssetId.IsNone)
        {
            ActiveTileAssetId = sample.AssetId;
            TileAssetSampled?.Invoke(sample.AssetId);
        }
        if (switchToBrush)
        {
            ActiveTool = EditorTool.Brush;
        }

        BrushSampled?.Invoke(new BrushSample(sample.TilesetId, sample.SrcX, sample.SrcY, sample.Type, switchToBrush));
        Invalidate();
        return true;
    }

    public void PerformUndo()
    {
        if (Map is null)
        {
            return;
        }

        var regions = Map.Regions;
        var restored = History.TryUndo(Map);
        if (restored is null)
        {
            return;
        }

        KeepRegions(restored, regions);
        Map = restored;
        MapEdited?.Invoke();
        MapReplaced?.Invoke();
        Invalidate();
    }

    public void PerformRedo()
    {
        if (Map is null)
        {
            return;
        }

        var regions = Map.Regions;
        var restored = History.TryRedo(Map);
        if (restored is null)
        {
            return;
        }

        KeepRegions(restored, regions);
        Map = restored;
        MapEdited?.Invoke();
        MapReplaced?.Invoke();
        Invalidate();
    }

    public void ClearHistory() => History.Clear();

    private static void KeepRegions(Map restored, MapRegionDocument? regions)
    {
        if (regions is null)
        {
            return;
        }

        regions.AdoptMapSize(restored.Width, restored.Height);
        restored.Regions = regions;
    }

    private void PaintRegionAt(int tx, int ty, bool erase)
    {
        if (Map is null || tx < 0 || ty < 0 || tx >= Map.Width || ty >= Map.Height)
        {
            return;
        }

        var id = erase ? (byte)0 : ActiveRegionId;
        if (!MapRegionEdit.TryPaint(Map, tx, ty, id, out _))
        {
            return;
        }

        if (!_regionStroke)
        {
            _regionStroke = true;
            MapEdited?.Invoke();
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);

        ComputeVisibleTileRange(out var tx0, out var ty0, out var tx1, out var ty1);

        var mw = Math.Max(1, Map?.Width ?? 20);
        var mh = Math.Max(1, Map?.Height ?? 15);

        var prevInterp = g.InterpolationMode;
        var state = g.Save();
        try
        {
            g.TranslateTransform(Pan.X, Pan.Y);
            g.ScaleTransform(Zoom, Zoom);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;

            if (!TryBlitLayerCache(g, mw, mh, tx0, ty0, tx1, ty1))
            {
                DrawStaticTileLayers(g, mw, mh, tx0, ty0, tx1, ty1);
            }

            if (Map is not null)
            {
                DrawPlacedPrefabs(g);
                DrawTileTypeOverlay(g, tx0, ty0, tx1, ty1);
                DrawRegionOverlay(g, tx0, ty0, tx1, ty1);
                DrawMapEventMarkerOverlay(g, tx0, ty0, tx1, ty1);
                DrawTransferIssueOverlay(g, tx0, ty0, tx1, ty1);
                DrawPlaytestSpawnMarker(g, tx0, ty0, tx1, ty1);
                DrawPlacedEntities(g, tx0, ty0, tx1, ty1);
            }

            if (Map is not null && ActiveTool == EditorTool.Region)
            {
                DrawTileRectPixels(g, _hoverTile.X, _hoverTile.Y, _hoverTile.X, _hoverTile.Y, Color.FromArgb(255, 120, 196, 255), dash: true);
            }

            if (Map is not null && ActiveTool == EditorTool.Spawn)
            {
                DrawTileRectPixels(g, _hoverTile.X, _hoverTile.Y, _hoverTile.X, _hoverTile.Y, Color.DeepSkyBlue, dash: true);
            }

            if (Map is not null && ActiveTool == EditorTool.Place)
            {
                DrawPlaceGhost(g);
            }

            if (Map is not null && ActiveTool == EditorTool.Prefab)
            {
                DrawPrefabGhost(g, mw, mh);
            }

            if (ActiveTool == EditorTool.Selection && Map is not null && _selectionMarqueeAnchor is { } sa)
            {
                var xa = Math.Min(sa.X, _hoverTile.X);
                var ya = Math.Min(sa.Y, _hoverTile.Y);
                var xb = Math.Max(sa.X, _hoverTile.X);
                var yb = Math.Max(sa.Y, _hoverTile.Y);
                DrawTileRectPixels(g, xa, ya, xb, yb, Color.LimeGreen, dash: true);
            }

            if (Map is not null && _committedSelectionTiles is { Width: > 0, Height: > 0 } sel)
            {
                DrawTileRectPixels(g,
                    sel.Left,
                    sel.Top,
                    sel.Left + sel.Width - 1,
                    sel.Top + sel.Height - 1,
                    Color.LightGreen,
                    dash: true);
            }

            if (Map is not null
                && ActiveTool == EditorTool.Selection
                && _selectionMarqueeAnchor is null
                && TryGetPasteFootprint(_hoverTile.X, _hoverTile.Y, out var pasteFootprint)
                && _committedSelectionTiles != pasteFootprint)
            {
                DrawTileRectPixels(
                    g,
                    pasteFootprint.Left,
                    pasteFootprint.Top,
                    pasteFootprint.Right - 1,
                    pasteFootprint.Bottom - 1,
                    Color.FromArgb(255, 255, 176, 64),
                    dash: true,
                    wash: false);
            }

            _joinPreview = null;
            if (BrushGhostVisible() && IsTileAssetMap)
            {
                _joinPreview = BuildJoinPreview(out var stamp);
                if (JoinAutotiles)
                {
                    DrawAutotileJoinPreview(g, stamp, mw, mh);
                }
            }

            if (Map is not null && ActiveTool == EditorTool.Rectangle && _rectPaintOrigin is { } ro)
            {
                var shape = CurrentShapeOptions(ShapeShiftOutline());
                var cells = MapEditOperations.EnumerateShape(ro.X, ro.Y, _hoverTile.X, _hoverTile.Y, shape);
                if (shape.Outline || shape.Ellipse)
                {
                    DrawTileRectPixels(g, ro.X, ro.Y, _hoverTile.X, _hoverTile.Y, Color.Cyan, dash: true, wash: false);
                    DrawShapeRubberBand(g, cells, mw, mh);
                }
                else
                {
                    DrawTileRectPixels(g, ro.X, ro.Y, _hoverTile.X, _hoverTile.Y, Color.Cyan, dash: false);
                }
            }

            if (Map is not null && ActiveTool == EditorTool.Line && _linePaintOrigin is not null)
            {
                DrawLineRubberBand(g, CurrentLineCells(LineAxisConstrained()), mw, mh);
            }

            if (Map is not null && ActiveTool == EditorTool.Eraser && IsActiveLayerPaintable())
            {
                var ew = Math.Max(1, SelectedStampInTiles.Width);
                var eh = Math.Max(1, SelectedStampInTiles.Height);
                DrawTileRectPixels(
                    g,
                    _hoverTile.X,
                    _hoverTile.Y,
                    _hoverTile.X + ew - 1,
                    _hoverTile.Y + eh - 1,
                    Color.FromArgb(255, 220, 96, 96),
                    dash: true);
            }

            if (BrushGhostVisible() && _linePaintOrigin is null)
            {
                var tx = _hoverTile.X;
                var ty = _hoverTile.Y;
                var ts = TileSize;
                var sw = Math.Max(1, SelectedStampInTiles.Width);
                var sh = Math.Max(1, SelectedStampInTiles.Height);
                if (IsTileAssetMap)
                {
                    for (var dy = 0; dy < sh; dy++)
                    {
                        for (var dx = 0; dx < sw; dx++)
                        {
                            var mtx = tx + dx;
                            var mty = ty + dy;
                            if (mtx < 0 || mty < 0 || mtx >= mw || mty >= mh)
                            {
                                continue;
                            }

                            DrawTileAssetImage(g, PreviewAssetId(mtx, mty), new Rectangle(mtx * ts, mty * ts, ts, ts), 0.45f);
                        }
                    }
                }
                else if (TilesetCache.TryGet(ActiveTilesetId, out var bmpG) && bmpG is not null)
                {
                    using var attrs = new System.Drawing.Imaging.ImageAttributes();
                    attrs.SetColorMatrix(
                        new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.45f },
                        System.Drawing.Imaging.ColorMatrixFlag.Default,
                        System.Drawing.Imaging.ColorAdjustType.Bitmap);
                    for (var dy = 0; dy < sh; dy++)
                    {
                        for (var dx = 0; dx < sw; dx++)
                        {
                            var sx = SelectedSrc.X + dx * ts;
                            var sy = SelectedSrc.Y + dy * ts;
                            CanonicalizeStoredSource(ref sx, ref sy);
                            if (sx < 0 || sy < 0 || sx + ts > bmpG.Width || sy + ts > bmpG.Height)
                            {
                                continue;
                            }

                            PreviewSource(ActiveTilesetId, bmpG, ref sx, ref sy);

                            var mtx = tx + dx;
                            var mty = ty + dy;
                            if (mtx < 0 || mty < 0 || mtx >= mw || mty >= mh)
                            {
                                continue;
                            }

                            var dst = new Rectangle(mtx * ts, mty * ts, ts, ts);
                            g.DrawImage(bmpG, dst, sx, sy, ts, ts, GraphicsUnit.Pixel, attrs);
                        }
                    }
                }
            }
        }
        finally
        {
            _joinPreview = null;
            g.Restore(state);
            g.InterpolationMode = prevInterp;
        }
    }

    private bool IsTileAssetMap => Map?.GraphicIdentity == TileGraphicIdentity.TileAsset;

    private bool HasTileAssetBrush() =>
        IsTileAssetMap
        && TileAssets is not null
        && !ActiveTileAssetId.IsNone
        && TileAssets.TryGet(ActiveTileAssetId, out _);

    private bool BrushGhostVisible() =>
        Map is not null
        && IsActiveLayerEditable()
        && (ActiveTool is EditorTool.Brush or EditorTool.Rectangle or EditorTool.Line or EditorTool.Fill)
        && (IsTileAssetMap ? HasTileAssetBrush() : ActiveTilesetId > 0);

    private void DrawStaticTileLayers(Graphics g, int mapW, int mapH, int tx0, int ty0, int tx1, int ty1)
    {
        DrawGridCells(g, mapW, mapH, tx0, ty0, tx1, ty1);
        if (Map is null)
        {
            return;
        }

        for (var i = 0; i < Map.Layers.Count; i++)
        {
            var alpha = LayerDrawAlpha(i);
            if (alpha <= 0.001f)
            {
                continue;
            }

            DrawLayer(g, Map.Layers[i], tx0, ty0, tx1, ty1, alpha);
        }
    }

    /// <summary>
    /// Bitmap monde des tuiles et de la grille. Un panoramique dans la marge ne redessine pas chaque tuile.
    /// Au-delà de <see cref="LayerCacheMaxEdge"/> pixels, le dessin direct reprend.
    /// </summary>
    private bool TryBlitLayerCache(Graphics g, int mapW, int mapH, int tx0, int ty0, int tx1, int ty1)
    {
        if (Map is null || TileSize <= 0 || Zoom <= 0 || tx1 < tx0 || ty1 < ty0)
        {
            return false;
        }

        var key = LayerCacheKey();
        var covers = _layerCache is not null
            && ReferenceEquals(_layerCacheMap, Map)
            && _layerCacheTileSize == TileSize
            && _layerCacheKey == key
            && _layerCacheTx0 <= tx0
            && _layerCacheTy0 <= ty0
            && _layerCacheTx1 >= tx1
            && _layerCacheTy1 >= ty1;
        if (!covers && !RebuildLayerCache(mapW, mapH, tx0, ty0, tx1, ty1, key))
        {
            return false;
        }

        var ts = TileSize;
        var worldX = _layerCacheTx0 * ts;
        var worldY = _layerCacheTy0 * ts;
        var worldW = (_layerCacheTx1 - _layerCacheTx0 + 1) * ts;
        var worldH = (_layerCacheTy1 - _layerCacheTy0 + 1) * ts;
        g.DrawImage(_layerCache!, worldX, worldY, worldW, worldH);
        return true;
    }

    private bool RebuildLayerCache(int mapW, int mapH, int tx0, int ty0, int tx1, int ty1, int key)
    {
        if (!TryPickCacheTileBounds(mapW, mapH, tx0, ty0, tx1, ty1, out var cx0, out var cy0, out var cx1, out var cy1))
        {
            return false;
        }

        var ts = TileSize;
        var bmpW = (cx1 - cx0 + 1) * ts;
        var bmpH = (cy1 - cy0 + 1) * ts;
        var bmp = new Bitmap(bmpW, bmpH, PixelFormat.Format32bppPArgb);
        try
        {
            using var cg = Graphics.FromImage(bmp);
            cg.Clear(BackColor);
            cg.InterpolationMode = InterpolationMode.NearestNeighbor;
            cg.TranslateTransform(-cx0 * ts, -cy0 * ts);
            DrawStaticTileLayers(cg, mapW, mapH, cx0, cy0, cx1, cy1);
        }
        catch
        {
            bmp.Dispose();
            throw;
        }

        _layerCache?.Dispose();
        _layerCache = bmp;
        _layerCacheMap = Map;
        _layerCacheTx0 = cx0;
        _layerCacheTy0 = cy0;
        _layerCacheTx1 = cx1;
        _layerCacheTy1 = cy1;
        _layerCacheTileSize = ts;
        _layerCacheKey = key;
        return true;
    }

    private bool TryPickCacheTileBounds(
        int mapW,
        int mapH,
        int tx0,
        int ty0,
        int tx1,
        int ty1,
        out int cx0,
        out int cy0,
        out int cx1,
        out int cy1)
    {
        cx0 = tx0;
        cy0 = ty0;
        cx1 = tx1;
        cy1 = ty1;
        if (mapW <= 0 || mapH <= 0 || TileSize <= 0)
        {
            return false;
        }

        for (var pad = LayerCachePadTiles; pad >= 0; pad -= 4)
        {
            var x0 = Math.Max(0, tx0 - pad);
            var y0 = Math.Max(0, ty0 - pad);
            var x1 = Math.Min(mapW - 1, tx1 + pad);
            var y1 = Math.Min(mapH - 1, ty1 + pad);
            var px = (x1 - x0 + 1) * TileSize;
            var py = (y1 - y0 + 1) * TileSize;
            if (px > 0 && py > 0 && px <= LayerCacheMaxEdge && py <= LayerCacheMaxEdge)
            {
                cx0 = x0;
                cy0 = y0;
                cx1 = x1;
                cy1 = y1;
                return true;
            }

            if (pad == 0)
            {
                break;
            }
        }

        return false;
    }

    private int LayerCacheKey()
    {
        var hash = new HashCode();
        hash.Add(BackColor.ToArgb());
        hash.Add(_tilesetAnimEpoch);
        hash.Add(TileSize);
        if (Map is null)
        {
            return hash.ToHashCode();
        }

        hash.Add(Map.Layers.Count);
        for (var i = 0; i < Map.Layers.Count; i++)
        {
            var layer = Map.Layers[i];
            hash.Add(layer.CellEditEpoch);
            hash.Add(layer.Tiles.Count);
            hash.Add(layer.Tiles.Count == 0 ? 0 : RuntimeHelpers.GetHashCode(layer.Tiles[0]));
            hash.Add(BitConverter.SingleToInt32Bits(LayerDrawAlpha(i)));
        }

        return hash.ToHashCode();
    }

    private void DisposeLayerCache()
    {
        _layerCache?.Dispose();
        _layerCache = null;
        _layerCacheMap = null;
    }

    private void DrawGridCells(Graphics g, int mapW, int mapH, int tx0, int ty0, int tx1, int ty1)
    {
        var ts = TileSize;
        using var penMajor = new Pen(Color.FromArgb(92, 98, 108), 1f);
        using var penMinor = new Pen(Color.FromArgb(58, 62, 74), 1f);
        using var light = new SolidBrush(Color.FromArgb(48, 52, 60));
        using var dark = new SolidBrush(Color.FromArgb(54, 58, 66));

        var yTop = ty0 * ts;
        var yBot = Math.Min(mapH * ts, (ty1 + 1) * ts);
        var xLeft = tx0 * ts;
        var xRight = Math.Min(mapW * ts, (tx1 + 1) * ts);

        for (var y = ty0; y <= ty1 && y < mapH; y++)
        {
            for (var x = tx0; x <= tx1 && x < mapW; x++)
            {
                var r = new Rectangle(x * ts, y * ts, ts, ts);
                g.FillRectangle(((x + y) % 2 == 0) ? light : dark, r);
            }
        }

        for (var x = tx0; x <= mapW && x <= tx1 + 1; x++)
        {
            var px = x * ts;
            g.DrawLine(penMinor, px, yTop, px, yBot);
        }

        for (var y = ty0; y <= mapH && y <= ty1 + 1; y++)
        {
            var py = y * ts;
            g.DrawLine(penMinor, xLeft, py, xRight, py);
        }

        g.DrawRectangle(penMajor, 0, 0, mapW * ts, mapH * ts);
    }

    private void DrawLayer(Graphics g, Layer layer, int tx0, int ty0, int tx1, int ty1, float alpha)
    {
        if (alpha <= 0.001f)
        {
            return;
        }

        var fade = alpha < 0.999f;
        using var attrs = fade ? new ImageAttributes() : null;
        if (attrs is not null)
        {
            attrs.SetColorMatrix(
                new ColorMatrix { Matrix33 = alpha },
                ColorMatrixFlag.Default,
                ColorAdjustType.Bitmap);
        }

        ForEachVisibleTile(layer, tx0, ty0, tx1, ty1, tile => DrawPlacedTile(g, tile, attrs));
    }

    private void DrawPlacedTile(Graphics g, Tile t, ImageAttributes? attrs)
    {
        if (!t.AssetId.IsNone)
        {
            DrawTileAssetImage(
                g,
                t.AssetId,
                new Rectangle(t.X * TileSize, t.Y * TileSize, TileSize, TileSize),
                sharedAttrs: attrs);
            return;
        }

        if (!TilesetCache.TryGet(t.TilesetId, out var bmp) || bmp is null)
        {
            return;
        }

        var srcX = t.SrcX;
        var srcY = t.SrcY;
        PreviewSource(PlacedAnimTilesetId(t), bmp, ref srcX, ref srcY);
        var src = new Rectangle(srcX, srcY, TileSize, TileSize);
        var dst = new Rectangle(t.X * TileSize, t.Y * TileSize, TileSize, TileSize);
        if (src.Right > bmp.Width || src.Bottom > bmp.Height)
        {
            return;
        }

        if (attrs is not null)
        {
            g.DrawImage(bmp, dst, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
        }
        else
        {
            g.DrawImage(bmp, dst, src, GraphicsUnit.Pixel);
        }
    }

    /// <summary>
    /// Couche pleine : cases visibles seulement. Couche clairsemée : la liste reste plus courte que le viewport.
    /// </summary>
    private static void ForEachVisibleTile(Layer layer, int tx0, int ty0, int tx1, int ty1, Action<Tile> draw)
    {
        var spanX = tx1 - tx0 + 1;
        var spanY = ty1 - ty0 + 1;
        if (spanX <= 0 || spanY <= 0)
        {
            return;
        }

        var visible = (long)spanX * spanY;
        if (layer.Tiles.Count > visible)
        {
            for (var y = ty0; y <= ty1; y++)
            {
                for (var x = tx0; x <= tx1; x++)
                {
                    var tile = layer.TileAt(x, y);
                    if (tile is not null)
                    {
                        draw(tile);
                    }
                }
            }

            return;
        }

        foreach (var tile in layer.Tiles)
        {
            if (tile.X < tx0 || tile.X > tx1 || tile.Y < ty0 || tile.Y > ty1)
            {
                continue;
            }

            draw(tile);
        }
    }

    private float LayerDrawAlpha(int index)
    {
        if (Map is null || (uint)index >= (uint)Map.Layers.Count)
        {
            return 0f;
        }

        _layerPreview.Fit(Map.Layers.Count);
        var active = Math.Clamp(ActiveLayerIndex, 0, Map.Layers.Count - 1);
        return _layerPreview.DrawAlpha(index, Map.Layers[index].Visible, active);
    }

    internal float LayerDrawAlphaForTest(int layerIndex) => LayerDrawAlpha(layerIndex);

    private static int ScalePreviewAlpha(int alpha, float factor)
    {
        var scaled = (int)Math.Round(alpha * factor, MidpointRounding.AwayFromZero);
        if (scaled < 0)
        {
            return 0;
        }

        return scaled > 255 ? 255 : scaled;
    }

    private void DrawTileTypeOverlay(Graphics g, int tx0, int ty0, int tx1, int ty1)
    {
        if (Map is null)
        {
            return;
        }

        for (var i = 0; i < Map.Layers.Count; i++)
        {
            var alpha = LayerDrawAlpha(i);
            if (alpha <= 0.001f)
            {
                continue;
            }

            var layer = Map.Layers[i];
            ForEachVisibleTile(layer, tx0, ty0, tx1, ty1, t =>
            {
                var rect = new Rectangle(t.X * TileSize, t.Y * TileSize, TileSize, TileSize);
                switch (t.Type)
                {
                    case TileType.Block:
                        using (var b = new SolidBrush(Color.FromArgb(ScalePreviewAlpha(80, alpha), Color.Red)))
                        {
                            g.FillRectangle(b, rect);
                        }

                        break;

                    case TileType.Warp:
                    {
                        using var p = new Pen(Color.FromArgb(ScalePreviewAlpha(255, alpha), Color.Lime), 2);
                        g.DrawRectangle(p, rect);
                        break;
                    }

                    case TileType.Resource:
                        using (var br = new SolidBrush(Color.FromArgb(ScalePreviewAlpha(160, alpha), Color.Gold)))
                        {
                            var cx = rect.X + TileSize / 4;
                            var cy = rect.Y + TileSize / 4;
                            var d = TileSize / 2;
                            g.FillEllipse(br, cx, cy, d, d);
                        }

                        break;
                }
            });
        }
    }

    private void DrawRegionOverlay(Graphics g, int tx0, int ty0, int tx1, int ty1)
    {
        if (ActiveTool != EditorTool.Region || Map?.Regions is not { } regions)
        {
            return;
        }

        var ts = TileSize;
        using var font = new Font("Segoe UI", Math.Max(11f, ts * 0.42f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var text = new SolidBrush(Color.White);
        using var shadow = new SolidBrush(Color.FromArgb(190, 12, 16, 24));
        for (var y = ty0; y <= ty1 && y < Map.Height; y++)
        {
            for (var x = tx0; x <= tx1 && x < Map.Width; x++)
            {
                var id = regions.Get(x, y);
                var label = MapRegionEdit.OverlayText(id);
                if (label.Length == 0)
                {
                    continue;
                }

                var rect = new Rectangle(x * ts, y * ts, ts, ts);
                var tint = RegionTint(id);
                using (var wash = new SolidBrush(Color.FromArgb(88, tint)))
                {
                    g.FillRectangle(wash, rect);
                }

                using (var pen = new Pen(Color.FromArgb(210, tint), 2f))
                {
                    g.DrawRectangle(pen, rect.X + 1, rect.Y + 1, rect.Width - 3, rect.Height - 3);
                }

                var size = g.MeasureString(label, font);
                var px = rect.X + (ts - size.Width) / 2f;
                var py = rect.Y + (ts - size.Height) / 2f;
                g.DrawString(label, font, shadow, px + 1f, py + 1f);
                g.DrawString(label, font, text, px, py);
            }
        }
    }

    private static readonly byte[] RegionPalette =
    [
        96, 176, 255,
        255, 176, 72,
        120, 214, 140,
        214, 120, 196,
        255, 112, 112,
        176, 140, 255,
        255, 214, 96,
        96, 214, 214,
    ];

    private static Color RegionTint(byte regionId)
    {
        var slot = ((regionId - 1) & 7);
        var i = slot * 3;
        return Color.FromArgb(RegionPalette[i], RegionPalette[i + 1], RegionPalette[i + 2]);
    }

    private static void FillDiamond(Graphics g, Brush brush, Rectangle r)
    {
        var pts = new[]
        {
            new Point(r.X + r.Width / 2, r.Y),
            new Point(r.Right, r.Y + r.Height / 2),
            new Point(r.X + r.Width / 2, r.Bottom),
            new Point(r.Left, r.Y + r.Height / 2),
        };
        g.FillPolygon(brush, pts);
    }

    private static void DrawDiamond(Graphics g, Pen pen, Rectangle r)
    {
        var pts = new[]
        {
            new Point(r.X + r.Width / 2, r.Y),
            new Point(r.Right, r.Y + r.Height / 2),
            new Point(r.X + r.Width / 2, r.Bottom),
            new Point(r.Left, r.Y + r.Height / 2),
        };
        g.DrawPolygon(pen, pts);
    }

    private void DrawTransferIssueOverlay(Graphics g, int tx0, int ty0, int tx1, int ty1)
    {
        if (_transferIssueTiles.Count == 0 || Map is null)
        {
            return;
        }

        var ts = TileSize;
        var prev = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            foreach (var (tileX, tileY) in _transferIssueTiles)
            {
                if (tileX < tx0 || tileX > tx1 || tileY < ty0 || tileY > ty1)
                {
                    continue;
                }

                if (tileX < 0 || tileX >= Map.Width || tileY < 0 || tileY >= Map.Height)
                {
                    continue;
                }

                var rect = new Rectangle(tileX * ts, tileY * ts, ts, ts);
                using (var pen = new Pen(EditorChrome.WarningAmber, Math.Max(1.6f, ts / 14f))
                {
                    DashStyle = DashStyle.Dash,
                })
                {
                    g.DrawRectangle(pen, rect.X, rect.Y, Math.Max(1, rect.Width - 1), Math.Max(1, rect.Height - 1));
                }

                var badge = Math.Max(8, ts * 2 / 5);
                var badgeRect = new RectangleF(rect.X + 1, rect.Y + 1, badge, badge);
                using (var badgeBrush = new SolidBrush(Color.FromArgb(235, EditorChrome.WarningAmber)))
                {
                    g.FillEllipse(badgeBrush, badgeRect);
                }

                using var font = new Font(Font.FontFamily, Math.Max(7f, badge * 0.72f), FontStyle.Bold, GraphicsUnit.Pixel);
                using var text = new SolidBrush(Color.FromArgb(42, 24, 8));
                using var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };
                g.DrawString("!", font, text, badgeRect, format);
            }
        }
        finally
        {
            g.SmoothingMode = prev;
        }
    }

    private void DrawMapEventMarkerOverlay(Graphics g, int tx0, int ty0, int tx1, int ty1)
    {
        if (!ShowMapEventMarkers || _mapEventMarkers is null || Map is null || _mapEventMarkers.Count == 0)
        {
            return;
        }

        var ts = TileSize;
        var visible = new List<MapEventMarkerView>();
        foreach (var m in _mapEventMarkers)
        {
            if (m.TileX < tx0 || m.TileX > tx1 || m.TileY < ty0 || m.TileY > ty1)
            {
                continue;
            }

            if (m.TileX < 0 || m.TileX >= Map.Width || m.TileY < 0 || m.TileY >= Map.Height)
            {
                continue;
            }

            visible.Add(m);
        }

        if (visible.Count == 0)
        {
            return;
        }

        var prevSmooth = g.SmoothingMode;
        var prevText = g.TextRenderingHint;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        try
        {
            foreach (var m in visible)
            {
                DrawMapEventMarkerBody(g, m, ts);
            }

            foreach (var m in visible)
            {
                if (!ShouldDrawNameFor(m))
                {
                    continue;
                }

                DrawMapEventMarkerName(g, m, ts);
            }
        }
        finally
        {
            g.SmoothingMode = prevSmooth;
            g.TextRenderingHint = prevText;
        }
    }

    private void DrawMapEventMarkerBody(Graphics g, MapEventMarkerView marker, int ts)
    {
        var selected = MarkerMatchesSelection(marker);
        var hovered = _hoveredMapEventMarker is { } h && h.Equals(marker);
        var fill = MapEventMarkerColors.TintFromSlug(marker.PrimarySlug);
        var tile = new Rectangle(marker.TileX * ts, marker.TileY * ts, ts, ts);
        var washAlpha = selected ? 70 : hovered ? 48 : 26;
        using (var wash = new SolidBrush(Color.FromArgb(washAlpha, fill)))
        {
            g.FillRectangle(wash, tile);
        }

        var diamond = MapEventMarkerLayout.DiamondBounds(marker.TileX, marker.TileY, ts);
        var fillAlpha = selected || hovered ? 230 : 200;
        using (var brush = new SolidBrush(Color.FromArgb(fillAlpha, fill)))
        {
            FillDiamond(g, brush, diamond);
        }

        var autorun = MapEventMarkerColors.IsAutorunTrigger(marker.PrimaryTriggerKind);
        var parallel = MapEventMarkerColors.IsParallelTrigger(marker.PrimaryTriggerKind);
        var legacyPage = MapEventMarkerColors.IsLegacyPageTrigger(marker.PrimaryTriggerKind);
        var transferIssue = _transferIssueTiles.Contains((marker.TileX, marker.TileY));
        var penWidth = selected || transferIssue ? Math.Max(2.2f, ts / 10f) : Math.Max(1.4f, ts / 16f);
        var edgeColor = transferIssue ? EditorChrome.WarningAmber : selected ? Color.Gold : Color.White;
        using (var shadow = new Pen(Color.FromArgb(190, 16, 14, 22), penWidth + 1.6f))
        {
            DrawDiamond(g, shadow, diamond);
        }

        using (var edge = new Pen(Color.FromArgb(235, edgeColor), penWidth))
        {
            if (autorun)
            {
                edge.DashStyle = DashStyle.Dash;
            }

            DrawDiamond(g, edge, diamond);
        }

        if (parallel)
        {
            var inner = InsetRectangle(diamond, Math.Max(2, diamond.Width / 5));
            using var innerPen = new Pen(Color.FromArgb(230, edgeColor), Math.Max(1f, penWidth * 0.65f));
            DrawDiamond(g, innerPen, inner);
        }
        else if (legacyPage)
        {
            var r = Math.Max(2f, ts * 0.08f);
            var cx = diamond.X + diamond.Width / 2f;
            var cy = diamond.Y + diamond.Height / 2f;
            using var dot = new SolidBrush(Color.FromArgb(230, Color.White));
            g.FillEllipse(dot, cx - r, cy - r, r * 2f, r * 2f);
        }
        else
        {
            DrawTriggerGlyph(g, diamond, MapEventMarkerLayout.TriggerGlyph(marker.PrimaryTriggerKind));
        }

        if (marker.PlacementCount > 1 && !ShouldDrawNameFor(marker))
        {
            DrawPlacementCountBadge(g, tile, marker.PlacementCount, ts);
        }
    }

    private static void DrawTriggerGlyph(Graphics g, Rectangle diamond, string glyph)
    {
        using var font = new Font("Segoe UI", Math.Max(7f, diamond.Width * 0.46f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb(245, 255, 255, 255));
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        g.DrawString(glyph, font, brush, diamond, format);
    }

    private static void DrawPlacementCountBadge(Graphics g, Rectangle tile, int count, int ts)
    {
        var label = count > 9 ? "9+" : count.ToString();
        var diameter = Math.Max(11f, ts * 0.36f);
        var badge = new RectangleF(tile.Right - diameter - 1f, tile.Y + 1f, diameter, diameter);
        using (var bg = new SolidBrush(Color.FromArgb(230, 16, 14, 22)))
        {
            g.FillEllipse(bg, badge);
        }

        using var font = new Font("Segoe UI", Math.Max(6f, diameter * 0.62f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        g.DrawString(label, font, brush, badge, format);
    }

    private void DrawMapEventMarkerName(Graphics g, MapEventMarkerView marker, int ts)
    {
        var label = MapEventMarkerLayout.FormatLabel(marker.PrimaryDisplayName, marker.PrimarySlug, marker.PlacementCount);
        var bounds = MapEventMarkerLayout.NameLabelBounds(marker.TileX, marker.TileY, ts, label);
        var fill = MapEventMarkerColors.TintFromSlug(marker.PrimarySlug);
        using (var bg = new SolidBrush(Color.FromArgb(225, 16, 18, 24)))
        {
            g.FillRectangle(bg, bounds);
        }

        var accentW = Math.Max(2f, ts / 12f);
        using (var accent = new SolidBrush(fill))
        {
            g.FillRectangle(accent, bounds.X, bounds.Y, accentW, bounds.Height);
        }

        if (MarkerMatchesSelection(marker))
        {
            using var gold = new Pen(Color.Gold, Math.Max(1f, ts / 18f));
            g.DrawRectangle(gold, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        var textBounds = new RectangleF(bounds.X + accentW + 1f, bounds.Y, Math.Max(1f, bounds.Width - accentW - 2f), bounds.Height);
        using var font = new Font(Font.FontFamily, Math.Max(7f, bounds.Height * 0.62f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var tb = new SolidBrush(Color.White);
        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        g.DrawString(label, font, tb, textBounds, sf);
    }

    private static Rectangle InsetRectangle(Rectangle r, int inset)
    {
        var side = Math.Max(4, Math.Min(r.Width, r.Height) - inset * 2);
        var x = r.X + (r.Width - side) / 2;
        var y = r.Y + (r.Height - side) / 2;
        return new Rectangle(x, y, side, side);
    }

    private void DrawPlaytestSpawnMarker(Graphics g, int tx0, int ty0, int tx1, int ty1)
    {
        if (Map is null || PlaytestSpawnTile is not { } sp)
        {
            return;
        }

        if (sp.X < tx0 || sp.X > tx1 || sp.Y < ty0 || sp.Y > ty1)
        {
            return;
        }

        if (sp.X < 0 || sp.X >= Map.Width || sp.Y < 0 || sp.Y >= Map.Height)
        {
            return;
        }

        var ts = TileSize;
        var rect = new Rectangle(sp.X * ts, sp.Y * ts, ts, ts);
        var accent = Color.FromArgb(255, 80, 200, 255);
        using (var fill = new SolidBrush(Color.FromArgb(70, accent)))
        using (var pen = new Pen(accent, Math.Max(1.5f, ts / 16f)) { DashStyle = DashStyle.Dash })
        {
            g.FillRectangle(fill, rect);
            g.DrawRectangle(pen, rect);
        }

        var inset = Math.Max(3, ts / 6);
        var diamond = Rectangle.Inflate(rect, -inset, -inset);
        var prev = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            using (var brush = new SolidBrush(Color.FromArgb(210, accent)))
            {
                FillDiamond(g, brush, diamond);
            }

            using var edge = new Pen(Color.White, Math.Max(1f, ts / 20f));
            DrawDiamond(g, edge, diamond);
        }
        finally
        {
            g.SmoothingMode = prev;
        }
    }

    public bool TrySetPlaytestSpawn(int tileX, int tileY)
    {
        if (Map is null || !MapPlaytestSpawn.TryClamp(Map, tileX, tileY, out var x, out var y))
        {
            return false;
        }

        var next = new Point(x, y);
        if (PlaytestSpawnTile == next)
        {
            return true;
        }

        PlaytestSpawnTile = next;
        PlaytestSpawnChanged?.Invoke(next);
        Invalidate();
        return true;
    }

    public void ClearPlaytestSpawn()
    {
        if (PlaytestSpawnTile is null)
        {
            return;
        }

        PlaytestSpawnTile = null;
        Invalidate();
    }

    public void ReplacePlacedEntities(IReadOnlyList<MapPlacedEntity>? entities)
    {
        _placedEntities.Clear();
        _draggingPlacedId = null;
        _placedDragMoved = false;
        if (Map is not null)
        {
            var seen = new HashSet<Guid>();
            foreach (var entity in MapPlacedEntityEdit.Clone(entities))
            {
                if (!seen.Add(entity.Id) || !MapPlacedEntityEdit.TryValidate(entity, Map, out _))
                {
                    continue;
                }

                if (MapPlacedEntityEdit.FindAt(_placedEntities, entity.TileX, entity.TileY) is not null)
                {
                    continue;
                }

                _placedEntities.Add(entity);
            }
        }

        if (SelectedPlacedEntityId is Guid selected && SelectedPlacedEntity is null)
        {
            SelectedPlacedEntityId = null;
        }

        PlacedEntitiesChanged?.Invoke();
        PlacedEntitySelectionChanged?.Invoke();
        Invalidate();
    }

    public void SelectPlacedEntity(Guid id)
    {
        MapPlacedEntity? match = null;
        for (var i = 0; i < _placedEntities.Count; i++)
        {
            if (_placedEntities[i].Id == id)
            {
                match = _placedEntities[i];
                break;
            }
        }

        if (match is null)
        {
            return;
        }

        if (SelectedPlacedEntityId == id)
        {
            Invalidate();
            return;
        }

        SelectedPlacedEntityId = id;
        PlacedEntitySelectionChanged?.Invoke();
        Invalidate();
    }

    public bool TryUpdateSelectedPlacedEntity(
        MapPlacedKind kind,
        string? name,
        string? notes,
        MapPlacedFacing facing,
        int respawnSeconds,
        int level,
        out string? error)
    {
        error = "Aucune entité sélectionnée.";
        if (Map is null || SelectedPlacedEntity is not { } entity)
        {
            return false;
        }

        if (!MapPlacedEntityEdit.TryApply(entity, Map, kind, name, notes, facing, respawnSeconds, level, out error))
        {
            return false;
        }

        PlacedEntitiesChanged?.Invoke();
        PlacedEntitySelectionChanged?.Invoke();
        Invalidate();
        return true;
    }

    public bool TryRemoveSelectedPlacedEntity()
    {
        if (SelectedPlacedEntity is not { } entity)
        {
            return false;
        }

        return TryRemovePlacedEntityAt(entity.TileX, entity.TileY);
    }

    public int ClipPlacedEntitiesToMap()
    {
        if (Map is null)
        {
            return 0;
        }

        var removed = MapPlacedEntityEdit.DropOutside(_placedEntities, Map);
        if (removed == 0)
        {
            return 0;
        }

        if (SelectedPlacedEntityId is Guid && SelectedPlacedEntity is null)
        {
            SelectedPlacedEntityId = null;
            PlacedEntitySelectionChanged?.Invoke();
        }

        PlacedEntitiesChanged?.Invoke();
        Invalidate();
        return removed;
    }

    internal bool TryApplyPlaceToolAtTileForTest(int tileX, int tileY)
    {
        if (Map is null)
        {
            return false;
        }

        ActiveTool = EditorTool.Place;
        return TryBeginPlaceGesture(tileX, tileY);
    }

    internal bool TryHandlePlaceToolRightClickForTest(int tileX, int tileY, bool control)
    {
        if (Map is null || tileX < 0 || tileY < 0 || tileX >= Map.Width || tileY >= Map.Height)
        {
            return false;
        }

        ActiveTool = EditorTool.Place;
        if (control)
        {
            _suppressRightButtonErase = true;
            TileContextMenuRequested?.Invoke(new Point(tileX, tileY));
            return true;
        }

        return TryRemovePlacedEntityAt(tileX, tileY);
    }

    internal bool TryMovePlacedEntityForTest(Guid id, int tileX, int tileY)
    {
        if (Map is null || !MapPlacedEntityEdit.TryMove(_placedEntities, Map, id, tileX, tileY))
        {
            return false;
        }

        PlacedEntitiesChanged?.Invoke();
        Invalidate();
        return true;
    }

    private bool TryBeginPlaceGesture(int tileX, int tileY)
    {
        if (Map is null)
        {
            return false;
        }

        if (!MapPlacedEntityEdit.TryPlace(_placedEntities, Map, PlaceKind, tileX, tileY, out var entity, out var created))
        {
            return false;
        }

        var selectionChanged = SelectedPlacedEntityId != entity.Id;
        SelectedPlacedEntityId = entity.Id;
        _draggingPlacedId = entity.Id;
        _placedDragOrigin = new Point(entity.TileX, entity.TileY);
        _placedDragMoved = false;
        if (created)
        {
            PlacedEntitiesChanged?.Invoke();
        }

        if (selectionChanged || created)
        {
            PlacedEntitySelectionChanged?.Invoke();
        }

        Invalidate();
        return true;
    }

    private bool TryRemovePlacedEntityAt(int tileX, int tileY)
    {
        if (!MapPlacedEntityEdit.TryRemoveAt(_placedEntities, tileX, tileY, out var removed) || removed is null)
        {
            return false;
        }

        if (SelectedPlacedEntityId == removed.Id)
        {
            SelectedPlacedEntityId = null;
            PlacedEntitySelectionChanged?.Invoke();
        }

        if (_draggingPlacedId == removed.Id)
        {
            _draggingPlacedId = null;
            _placedDragMoved = false;
        }

        PlacedEntitiesChanged?.Invoke();
        Invalidate();
        return true;
    }

    private void DrawPlacedEntities(Graphics g, int tx0, int ty0, int tx1, int ty1)
    {
        if (Map is null)
        {
            return;
        }

        for (var i = 0; i < _placedEntities.Count; i++)
        {
            var entity = _placedEntities[i];
            if (entity.TileX < tx0 || entity.TileX > tx1 || entity.TileY < ty0 || entity.TileY > ty1)
            {
                continue;
            }

            if (entity.TileX < 0 || entity.TileX >= Map.Width || entity.TileY < 0 || entity.TileY >= Map.Height)
            {
                continue;
            }

            DrawPlacedEntityMarker(g, entity, selected: entity.Id == SelectedPlacedEntityId);
        }
    }

    private void DrawPlacedEntityMarker(Graphics g, MapPlacedEntity entity, bool selected)
    {
        var ts = TileSize;
        var rect = new Rectangle(entity.TileX * ts, entity.TileY * ts, ts, ts);
        var accent = entity.Kind switch
        {
            MapPlacedKind.Spawn => Color.FromArgb(255, 80, 200, 255),
            MapPlacedKind.Npc => Color.FromArgb(255, 130, 170, 255),
            _ => Color.FromArgb(255, 176, 196, 214),
        };
        var glyph = entity.Kind switch
        {
            MapPlacedKind.Spawn => "A",
            MapPlacedKind.Npc => "P",
            _ => "O",
        };

        using (var fill = new SolidBrush(Color.FromArgb(selected ? 120 : 70, accent)))
        using (var pen = new Pen(selected ? Color.White : accent, Math.Max(1.5f, ts / 14f)))
        {
            g.FillRectangle(fill, rect);
            g.DrawRectangle(pen, rect);
        }

        var prev = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            var inset = Math.Max(4, ts / 5);
            var bubble = Rectangle.Inflate(rect, -inset, -inset);
            using var brush = new SolidBrush(Color.FromArgb(230, accent));
            g.FillEllipse(brush, bubble);
            using var font = new Font(Font.FontFamily, Math.Max(8f, bubble.Height * 0.62f), FontStyle.Bold, GraphicsUnit.Pixel);
            using var text = new SolidBrush(Color.FromArgb(18, 22, 28));
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString(glyph, font, text, bubble, format);
        }
        finally
        {
            g.SmoothingMode = prev;
        }
    }

    private void DrawPlaceGhost(Graphics g)
    {
        if (Map is null)
        {
            return;
        }

        var tx = _hoverTile.X;
        var ty = _hoverTile.Y;
        if (tx < 0 || ty < 0 || tx >= Map.Width || ty >= Map.Height)
        {
            return;
        }

        if (MapPlacedEntityEdit.FindAt(_placedEntities, tx, ty) is not null)
        {
            return;
        }

        var color = PlaceKind switch
        {
            MapPlacedKind.Spawn => Color.FromArgb(255, 80, 200, 255),
            MapPlacedKind.Npc => Color.FromArgb(255, 130, 170, 255),
            _ => Color.FromArgb(255, 176, 196, 214),
        };
        DrawTileRectPixels(g, tx, ty, tx, ty, color, dash: true);
    }

    internal bool TryHandleSpawnToolRightClickForTest(int tileX, int tileY, bool control)
    {
        if (Map is null || tileX < 0 || tileY < 0 || tileX >= Map.Width || tileY >= Map.Height)
        {
            return false;
        }

        ActiveTool = EditorTool.Spawn;
        if (control)
        {
            _suppressRightButtonErase = true;
            TileContextMenuRequested?.Invoke(new Point(tileX, tileY));
            return true;
        }

        return false;
    }

    internal bool TryApplySpawnToolAtTileForTest(int tileX, int tileY)
    {
        if (Map is null)
        {
            return false;
        }

        ActiveTool = EditorTool.Spawn;
        return TrySetPlaytestSpawn(tileX, tileY);
    }

    public void ReplacePrefabPlacements(IEnumerable<PrefabPlacement>? placements)
    {
        // Cloner avant Clear : l’appelant peut passer la liste vivante (PrefabPlacements).
        var cloned = PrefabPlacementService.ClonePlacements(placements);
        _prefabPlacements.Clear();
        _selectedPrefabPlacement = null;
        _draggingPrefab = null;
        _prefabPlacements.AddRange(cloned);
        PrefabPlacementsChanged?.Invoke();
        Invalidate();
    }

    public PrefabPlacement? FindPrefabAt(int tileX, int tileY)
        => PrefabPlacementService.TryFindAt(_prefabPlacements, PrefabCatalog, tileX, tileY);

    public bool TrySelectPrefabPlacement(PrefabPlacement? placement)
    {
        if (placement is null)
        {
            return false;
        }

        for (var i = 0; i < _prefabPlacements.Count; i++)
        {
            if (!ReferenceEquals(_prefabPlacements[i], placement))
            {
                continue;
            }

            _selectedPrefabPlacement = placement;
            Invalidate();
            return true;
        }

        return false;
    }

    public bool TrySelectPrefabAt(int tileX, int tileY)
        => TrySelectPrefabPlacement(FindPrefabAt(tileX, tileY));

    public bool TryPlaceSelectedPrefab(int tileX, int tileY)
    {
        if (Map is null)
        {
            return false;
        }

        if (_draggingPrefab is not null)
        {
            return false;
        }

        var before = _prefabPlacements.Count;
        var ok = PrefabPlacementService.TryPlace(
            _prefabPlacements,
            PrefabCatalog,
            SelectedPrefabId,
            SelectedPrefabFacing,
            tileX,
            tileY,
            Map.Width,
            Map.Height,
            out var placed,
            out _);
        if (ok)
        {
            _selectedPrefabPlacement = placed;
            PrefabPlacementsChanged?.Invoke();
            Invalidate();
        }

        return ok || _prefabPlacements.Count != before;
    }

    public int TryErasePrefabAt(int tileX, int tileY)
    {
        var removed = PrefabPlacementService.EraseAt(_prefabPlacements, PrefabCatalog, tileX, tileY);
        if (removed > 0)
        {
            PrefabPlacementsChanged?.Invoke();
            Invalidate();
        }

        return removed;
    }

    public bool TryPipettePrefabAt(int tileX, int tileY)
    {
        var hit = PrefabPlacementService.TryFindAt(_prefabPlacements, PrefabCatalog, tileX, tileY);
        if (hit is null)
        {
            return false;
        }

        SelectedPrefabId = hit.PrefabId;
        SelectedPrefabFacing = hit.Facing;
        PrefabSelectionPicked?.Invoke(hit.PrefabId, hit.Facing);
        return true;
    }

    public bool TryBeginPrefabMoveAt(int tileX, int tileY)
    {
        var hit = PrefabPlacementService.TryFindAt(_prefabPlacements, PrefabCatalog, tileX, tileY);
        if (hit is null)
        {
            return false;
        }

        _draggingPrefab = hit;
        _prefabDragMoved = false;
        _selectedPrefabPlacement = hit;
        return true;
    }

    public bool TryMoveDraggingPrefabTo(int tileX, int tileY)
    {
        if (Map is null || _draggingPrefab is null)
        {
            return false;
        }

        if (!PrefabPlacementService.TryMove(
                _prefabPlacements,
                PrefabCatalog,
                _draggingPrefab,
                tileX,
                tileY,
                Map.Width,
                Map.Height,
                out _))
        {
            return false;
        }

        _prefabDragMoved = true;
        Invalidate();
        return true;
    }

    public void EndPrefabMove()
    {
        if (_draggingPrefab is null)
        {
            return;
        }

        _draggingPrefab = null;
        if (_prefabDragMoved)
        {
            PrefabPlacementsChanged?.Invoke();
        }

        _prefabDragMoved = false;
        Invalidate();
    }

    public bool TryDuplicateLastPrefab(out PrefabPlacement? placed, out string? error)
    {
        placed = null;
        if (Map is null)
        {
            error = "Aucune carte.";
            return false;
        }

        var ok = PrefabPlacementService.TryDuplicateLast(
            _prefabPlacements,
            PrefabCatalog,
            Map.Width,
            Map.Height,
            out placed,
            out error);
        if (ok)
        {
            _selectedPrefabPlacement = placed;
            PrefabPlacementsChanged?.Invoke();
            Invalidate();
        }

        return ok;
    }

    /// <summary>
    /// Duplique l’instance sélectionnée. Sans sélection, reprend la dernière posée
    /// (même chemin que le bouton historique). La sélection passe sur la copie.
    /// Les prefabs restent hors de la pile d’annulation des tuiles, comme une pose.
    /// </summary>
    public bool TryDuplicateSelectedPrefab(out PrefabPlacement? placed, out string? error)
    {
        placed = null;
        if (Map is null)
        {
            error = "Aucune carte.";
            return false;
        }

        var source = SelectedPrefabPlacement;
        var ok = source is null
            ? PrefabPlacementService.TryDuplicateLast(
                _prefabPlacements,
                PrefabCatalog,
                Map.Width,
                Map.Height,
                out placed,
                out error)
            : PrefabPlacementService.TryDuplicate(
                _prefabPlacements,
                PrefabCatalog,
                source,
                Map.Width,
                Map.Height,
                out placed,
                out error);
        if (ok)
        {
            _selectedPrefabPlacement = placed;
            PrefabPlacementsChanged?.Invoke();
            Invalidate();
        }

        return ok;
    }

    internal bool TryApplyPrefabToolAtTileForTest(int tileX, int tileY)
    {
        if (Map is null)
        {
            return false;
        }

        ActiveTool = EditorTool.Prefab;
        return TryPlaceSelectedPrefab(tileX, tileY);
    }

    internal int TryErasePrefabAtForTest(int tileX, int tileY)
    {
        ActiveTool = EditorTool.Prefab;
        return TryErasePrefabAt(tileX, tileY);
    }

    private void DrawPlacedPrefabs(Graphics g)
    {
        if (Map is null || _prefabPlacements.Count == 0)
        {
            return;
        }

        var ts = TileSize;
        foreach (var placement in _prefabPlacements)
        {
            if (!PrefabPlacementService.TryGetDefinition(PrefabCatalog, placement.PrefabId, out var definition)
                || !PrefabPlacementService.TryResolveVariant(definition, placement.Facing, out var variant)
                || !PrefabPlacementService.TryResolveFootprint(definition, variant, out var w, out var h))
            {
                continue;
            }

            var dest = new Rectangle(placement.TileX * ts, placement.TileY * ts, w * ts, h * ts);
            if (PrefabSpriteCache.TryGet(variant.SpriteFileName, out var bmp) && bmp is not null)
            {
                g.DrawImage(bmp, dest, new Rectangle(0, 0, bmp.Width, bmp.Height), GraphicsUnit.Pixel);
            }
            else
            {
                using var fill = new SolidBrush(Color.FromArgb(170, 168, 92, 58));
                using var pen = new Pen(Color.FromArgb(230, 80, 48, 28), 2f);
                g.FillRectangle(fill, dest);
                g.DrawRectangle(pen, dest);
            }

            if (ReferenceEquals(placement, _selectedPrefabPlacement))
            {
                using var selectPen = new Pen(EditorChrome.RibbonAccent, 2f);
                g.DrawRectangle(selectPen, dest);
            }
        }
    }

    private void DrawPrefabGhost(Graphics g, int mapW, int mapH)
    {
        if (!PrefabPlacementService.TryGetDefinition(PrefabCatalog, SelectedPrefabId, out var definition)
            || !PrefabPlacementService.TryResolveVariant(definition, SelectedPrefabFacing, out var variant)
            || !PrefabPlacementService.TryResolveFootprint(definition, variant, out var w, out var h))
        {
            DrawTileRectPixels(g, _hoverTile.X, _hoverTile.Y, _hoverTile.X, _hoverTile.Y, Color.SandyBrown, dash: true);
            return;
        }

        var x1 = Math.Min(mapW - 1, _hoverTile.X + w - 1);
        var y1 = Math.Min(mapH - 1, _hoverTile.Y + h - 1);
        DrawTileRectPixels(g, _hoverTile.X, _hoverTile.Y, x1, y1, Color.SandyBrown, dash: true);

        if (PrefabSpriteCache.TryGet(variant.SpriteFileName, out var bmp) && bmp is not null)
        {
            var ts = TileSize;
            var dest = new Rectangle(_hoverTile.X * ts, _hoverTile.Y * ts, w * ts, h * ts);
            using var attrs = new System.Drawing.Imaging.ImageAttributes();
            attrs.SetColorMatrix(
                new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.45f },
                System.Drawing.Imaging.ColorMatrixFlag.Default,
                System.Drawing.Imaging.ColorAdjustType.Bitmap);
            g.DrawImage(bmp, dest, 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attrs);
        }

        DrawPrefabNameTag(g, definition, _hoverTile.X, _hoverTile.Y);
    }

    private void DrawPrefabNameTag(Graphics g, PrefabDefinition definition, int tileX, int tileY)
    {
        var label = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Id : definition.DisplayName.Trim();
        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        using var font = CreatePrefabTagFont();
        var size = g.MeasureString(label, font);
        var x = tileX * TileSize;
        var y = Math.Max(0, tileY * TileSize - size.Height - 2f);
        var rect = new RectangleF(x, y, size.Width + 8f, size.Height + 2f);
        using var back = new SolidBrush(Color.FromArgb(220, 40, 32, 24));
        using var fore = new SolidBrush(Color.FromArgb(255, 255, 228, 180));
        g.FillRectangle(back, rect);
        g.DrawString(label, font, fore, rect.X + 4f, rect.Y + 1f);
    }

    private static Font CreatePrefabTagFont()
    {
        try
        {
            return new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Point);
        }
        catch (ArgumentException)
        {
            return new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold, GraphicsUnit.Point);
        }
    }

    private void DrawTileRectPixels(Graphics g, int ax, int ay, int bx, int by, Color color, bool dash, bool wash = true)
    {
        var x0 = Math.Min(ax, bx);
        var y0 = Math.Min(ay, by);
        var x1 = Math.Max(ax, bx);
        var y1 = Math.Max(ay, by);
        var ts = TileSize;
        var r = new Rectangle(x0 * ts, y0 * ts, (x1 - x0 + 1) * ts, (y1 - y0 + 1) * ts);
        if (wash)
        {
            using var b = new SolidBrush(Color.FromArgb(dash ? 50 : 55, color));
            g.FillRectangle(b, r);
        }

        using var p = new Pen(color, 2) { DashStyle = dash ? DashStyle.Dash : DashStyle.Solid };
        g.DrawRectangle(p, r);
    }

    private void DrawShapeRubberBand(Graphics g, IReadOnlyList<(int X, int Y)> cells, int mapW, int mapH)
    {
        if (cells.Count == 0)
        {
            return;
        }

        var ts = TileSize;
        var accent = EditorChrome.RibbonAccent;
        using var wash = new SolidBrush(Color.FromArgb(72, accent));
        using var edge = new Pen(Color.FromArgb(230, accent), 1.6f);
        for (var i = 0; i < cells.Count; i++)
        {
            var (x, y) = cells[i];
            if (x < 0 || y < 0 || x >= mapW || y >= mapH)
            {
                continue;
            }

            DrawBrushTileGhost(g, x, y, mapW, mapH, 0.72f);
            var rect = new Rectangle(x * ts + 1, y * ts + 1, Math.Max(1, ts - 2), Math.Max(1, ts - 2));
            g.FillRectangle(wash, rect);
            g.DrawRectangle(edge, rect);
        }
    }

    private void DrawLineRubberBand(Graphics g, IReadOnlyList<(int X, int Y)> cells, int mapW, int mapH)
    {
        if (cells.Count == 0)
        {
            return;
        }

        var ts = TileSize;
        var accent = EditorChrome.RibbonAccent;
        using var wash = new SolidBrush(Color.FromArgb(72, accent));
        using var spine = new Pen(Color.FromArgb(235, 236, 246, 255), 2.2f)
        {
            DashStyle = DashStyle.Dash,
            DashPattern = new[] { 5f, 3.5f },
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        using var midEdge = new Pen(Color.FromArgb(210, accent), 1.4f) { DashStyle = DashStyle.Dash };
        using var endEdge = new Pen(Color.FromArgb(245, 236, 246, 255), 2.2f);
        using var handleFill = new SolidBrush(Color.FromArgb(230, accent));
        using var handleRing = new Pen(Color.White, 1.6f);

        var centers = new List<PointF>(cells.Count);
        for (var i = 0; i < cells.Count; i++)
        {
            var (x, y) = cells[i];
            if (x < 0 || y < 0 || x >= mapW || y >= mapH)
            {
                continue;
            }

            DrawBrushTileGhost(g, x, y, mapW, mapH, 0.72f);
            var rect = new Rectangle(x * ts + 1, y * ts + 1, Math.Max(1, ts - 2), Math.Max(1, ts - 2));
            g.FillRectangle(wash, rect);
            var last = i == cells.Count - 1;
            g.DrawRectangle(i == 0 || last ? endEdge : midEdge, rect);
            centers.Add(new PointF(x * ts + (ts / 2f), y * ts + (ts / 2f)));
        }

        if (centers.Count >= 2)
        {
            g.DrawLines(spine, centers.ToArray());
        }

        if (centers.Count > 0)
        {
            DrawLineHandle(g, centers[0], ts, handleFill, handleRing, filled: true);
            if (centers.Count > 1)
            {
                DrawLineHandle(g, centers[^1], ts, handleFill, handleRing, filled: false);
            }
        }
    }

    private void DrawBrushTileGhost(Graphics g, int tx, int ty, int mapW, int mapH, float alpha)
    {
        if (IsTileAssetMap)
        {
            if (!HasTileAssetBrush() || tx < 0 || ty < 0 || tx >= mapW || ty >= mapH)
            {
                return;
            }

            DrawTileAssetImage(g, PreviewAssetId(tx, ty), new Rectangle(tx * TileSize, ty * TileSize, TileSize, TileSize), alpha);
            return;
        }

        if (!TilesetCache.TryGet(ActiveTilesetId, out var bmp) || bmp is null)
        {
            return;
        }

        var ts = TileSize;
        var sx = SelectedSrc.X;
        var sy = SelectedSrc.Y;
        PreviewSource(ActiveTilesetId, bmp, ref sx, ref sy);
        if (sx < 0 || sy < 0 || sx + ts > bmp.Width || sy + ts > bmp.Height)
        {
            return;
        }

        if (tx < 0 || ty < 0 || tx >= mapW || ty >= mapH)
        {
            return;
        }

        using var attrs = new System.Drawing.Imaging.ImageAttributes();
        attrs.SetColorMatrix(
            new System.Drawing.Imaging.ColorMatrix { Matrix33 = alpha },
            System.Drawing.Imaging.ColorMatrixFlag.Default,
            System.Drawing.Imaging.ColorAdjustType.Bitmap);
        g.DrawImage(bmp, new Rectangle(tx * ts, ty * ts, ts, ts), sx, sy, ts, ts, GraphicsUnit.Pixel, attrs);
    }

    private static void DrawLineHandle(Graphics g, PointF center, int tileSize, Brush fill, Pen ring, bool filled)
    {
        var d = Math.Clamp(tileSize * 0.28f, 6f, 14f);
        var r = new RectangleF(center.X - (d / 2f), center.Y - (d / 2f), d, d);
        if (filled)
        {
            g.FillEllipse(fill, r);
        }

        g.DrawEllipse(ring, r);
    }

    private void ComputeVisibleTileRange(out int tx0, out int ty0, out int tx1, out int ty1)
    {
        tx0 = ty0 = 0;
        tx1 = Math.Max(0, (Map?.Width ?? 20) - 1);
        ty1 = Math.Max(0, (Map?.Height ?? 15) - 1);
        var mw = Map?.Width ?? 20;
        var mh = Map?.Height ?? 15;

        var c = ClientSize;
        if (mw <= 0 || mh <= 0 || c.Width <= 0 || c.Height <= 0 || Zoom <= 0 || TileSize <= 0)
        {
            return;
        }

        var corners = new[]
        {
            ScreenToWorld(Point.Empty),
            ScreenToWorld(new Point(c.Width, 0)),
            ScreenToWorld(new Point(0, c.Height)),
            ScreenToWorld(new Point(c.Width, c.Height)),
        };

        float minWx = corners[0].X, maxWx = corners[0].X;
        float minWy = corners[0].Y, maxWy = corners[0].Y;
        foreach (var p in corners)
        {
            minWx = Math.Min(minWx, p.X);
            maxWx = Math.Max(maxWx, p.X);
            minWy = Math.Min(minWy, p.Y);
            maxWy = Math.Max(maxWy, p.Y);
        }

        var ts = TileSize;
        tx0 = (int)Math.Floor(minWx / ts) - ViewportPadTiles;
        ty0 = (int)Math.Floor(minWy / ts) - ViewportPadTiles;
        tx1 = (int)Math.Floor(maxWx / ts) + ViewportPadTiles;
        ty1 = (int)Math.Floor(maxWy / ts) + ViewportPadTiles;
        tx0 = Math.Clamp(tx0, 0, mw - 1);
        ty0 = Math.Clamp(ty0, 0, mh - 1);
        tx1 = Math.Clamp(tx1, 0, mw - 1);
        ty1 = Math.Clamp(ty1, 0, mh - 1);
        if (tx1 < tx0)
        {
            (tx0, tx1) = (tx1, tx0);
        }

        if (ty1 < ty0)
        {
            (ty0, ty1) = (ty1, ty0);
        }
    }

    private const float MinZoom = 0.125f;
    private const float MaxZoom = 16f;

    private void OnMouseWheelZoom(object? sender, MouseEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.1f : 1f / 1.1f;
        ApplyZoomFactorAtScreenPoint(factor, e.Location);
    }

    /// <summary>Zoom avant (centre du contrôle), pour menu / raccourcis.</summary>
    public void ZoomInTowardCenter()
        => ApplyZoomFactorAtScreenPoint(1.1f, new Point(ClientSize.Width / 2, Math.Max(0, ClientSize.Height / 2)));

    /// <summary>Zoom arrière (centre du contrôle).</summary>
    public void ZoomOutTowardCenter()
        => ApplyZoomFactorAtScreenPoint(1f / 1.1f, new Point(ClientSize.Width / 2, Math.Max(0, ClientSize.Height / 2)));

    private void ApplyZoomFactorAtScreenPoint(float factor, Point screenPt)
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        var before = ScreenToWorld(screenPt);
        var newZoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - Zoom) < 0.0001f)
        {
            return;
        }

        Zoom = newZoom;
        var after = ScreenToWorld(screenPt);
        Pan = new PointF(
            Pan.X + (screenPt.X - (after.X - before.X)),
            Pan.Y + (screenPt.Y - (after.Y - before.Y)));
        NotifyViewTransformChanged();
        Invalidate();
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Middle)
        {
            Focus();
        }

        if (e.Button == MouseButtons.Middle)
        {
            _panning = true;
            _lastMouse = e.Location;
            Cursor = Cursors.SizeAll;
            return;
        }

        if (Map is null)
        {
            return;
        }

        var world = ScreenToWorld(e.Location);
        var tx = (int)Math.Floor(world.X / TileSize);
        var ty = (int)Math.Floor(world.Y / TileSize);
        var inMap = tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height;

        if (QuickNpcPlacementClick is { } placeNpc)
        {
            if (e.Button == MouseButtons.Left && inMap && placeNpc(new Point(tx, ty)))
            {
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                return;
            }
        }

        if (e.Button == MouseButtons.Left && inMap && (ModifierKeys & Keys.Alt) == Keys.Alt)
        {
            TryPipetteAt(tx, ty, switchToBrush: false);
            return;
        }

        if (e.Button == MouseButtons.Left && TryPickMapEventMarker(world.X, world.Y))
        {
            _mapEventMarkerGesture = true;
            Capture = true;
            return;
        }

        if (!inMap)
        {
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            if (ActiveTool == EditorTool.Region)
            {
                PaintRegionAt(tx, ty, erase: true);
                Capture = true;
                return;
            }

            if (ActiveTool == EditorTool.Spawn)
            {
                if ((ModifierKeys & Keys.Control) == Keys.Control)
                {
                    _suppressRightButtonErase = true;
                    TileContextMenuRequested?.Invoke(new Point(tx, ty));
                }

                return;
            }

            if (ActiveTool == EditorTool.Place)
            {
                if ((ModifierKeys & Keys.Control) == Keys.Control)
                {
                    _suppressRightButtonErase = true;
                    TileContextMenuRequested?.Invoke(new Point(tx, ty));
                    return;
                }

                TryRemovePlacedEntityAt(tx, ty);
                Capture = true;
                return;
            }

            if (ActiveTool == EditorTool.Prefab)
            {
                if ((ModifierKeys & Keys.Control) == Keys.Control)
                {
                    _suppressRightButtonErase = true;
                    TileContextMenuRequested?.Invoke(new Point(tx, ty));
                    return;
                }

                TryErasePrefabAt(tx, ty);
                Capture = true;
                Invalidate();
                RaiseTileClicked(tx, ty);
                return;
            }

            if (ActiveTool == EditorTool.Rectangle)
            {
                _rectPaintOrigin = null;
                NotifyPaintGesture();
                return;
            }

            if (ActiveTool == EditorTool.Line)
            {
                _linePaintOrigin = null;
                NotifyPaintGesture();
                return;
            }

            if (ActiveTool == EditorTool.Selection)
            {
                ClearSelection();
                return;
            }

            if (ActiveTool == EditorTool.Fill)
            {
                if ((ModifierKeys & Keys.Control) == Keys.Control)
                {
                    _suppressRightButtonErase = true;
                    TileContextMenuRequested?.Invoke(new Point(tx, ty));
                    return;
                }

                if (!IsActiveLayerPaintable())
                {
                    return;
                }

                FloodFill(tx, ty, erase: true);
                Invalidate();
                RaiseTileClicked(tx, ty);
                return;
            }

            if (ActiveTool == EditorTool.Eraser)
            {
                if ((ModifierKeys & Keys.Control) == Keys.Control)
                {
                    _suppressRightButtonErase = true;
                    TileContextMenuRequested?.Invoke(new Point(tx, ty));
                    return;
                }

                if (!IsActiveLayerPaintable())
                {
                    return;
                }

                ApplyEraser(tx, ty);
                Capture = true;
                Invalidate();
                RaiseTileClicked(tx, ty);
                return;
            }

            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                _suppressRightButtonErase = true;
                TileContextMenuRequested?.Invoke(new Point(tx, ty));
                return;
            }

            if (!IsActiveLayerEditable())
            {
                return;
            }

            BeginPaintStroke();
            EraseStamp(tx, ty);
            Capture = true;
            Invalidate();
            RaiseTileClicked(tx, ty);
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            switch (ActiveTool)
            {
                case EditorTool.Brush:
                    if (!IsActiveLayerEditable())
                    {
                        break;
                    }

                    BeginPaintStroke();
                    ApplyBrush(tx, ty);
                    Capture = true;
                    Invalidate();
                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Eraser:
                    if (!IsActiveLayerPaintable())
                    {
                        break;
                    }

                    ApplyEraser(tx, ty);
                    Capture = true;
                    Invalidate();
                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Cursor:
                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Fill:
                    if (!IsActiveLayerPaintable())
                    {
                        break;
                    }

                    FloodFill(tx, ty, erase: false);
                    Invalidate();
                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Rectangle:
                    if (!IsActiveLayerPaintable())
                    {
                        break;
                    }

                    _rectPaintOrigin = new Point(tx, ty);
                    _hoverTile = new Point(tx, ty);
                    Capture = true;
                    Focus();
                    NotifyPaintGesture();
                    break;

                case EditorTool.Line:
                    if (!IsActiveLayerEditable())
                    {
                        break;
                    }

                    _linePaintOrigin = new Point(tx, ty);
                    _hoverTile = new Point(tx, ty);
                    Capture = true;
                    Focus();
                    NotifyPaintGesture();
                    break;

                case EditorTool.Selection:
                    _selectionMarqueeAnchor = new Point(tx, ty);
                    _hoverTile = new Point(tx, ty);
                    Capture = true;
                    NotifyPaintGesture();
                    break;

                case EditorTool.Spawn:
                    TrySetPlaytestSpawn(tx, ty);
                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Place:
                    if (TryBeginPlaceGesture(tx, ty))
                    {
                        Capture = true;
                    }

                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Region:
                    PaintRegionAt(tx, ty, erase: false);
                    Capture = true;
                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Prefab:
                    if ((ModifierKeys & Keys.Alt) == Keys.Alt)
                    {
                        TryPipettePrefabAt(tx, ty);
                        RaiseTileClicked(tx, ty);
                        break;
                    }

                    if (TryBeginPrefabMoveAt(tx, ty))
                    {
                        Capture = true;
                        Invalidate();
                        RaiseTileClicked(tx, ty);
                        break;
                    }

                    TryPlaceSelectedPrefab(tx, ty);
                    Capture = true;
                    Invalidate();
                    RaiseTileClicked(tx, ty);
                    break;
            }
        }
    }

    private void BeginPaintStroke()
    {
        if (Map is null || _paintStroke)
        {
            return;
        }

        BeginEditTransaction();
        _paintStroke = true;
    }

    private void BeginEditTransaction()
    {
        if (Map is null)
        {
            return;
        }

        History.PushBeforeChange(Map);
        MapEdited?.Invoke();
        UndoHistoryChanged?.Invoke();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (_panning)
        {
            Pan = new PointF(Pan.X + (e.Location.X - _lastMouse.X), Pan.Y + (e.Location.Y - _lastMouse.Y));
            _lastMouse = e.Location;
            NotifyViewTransformChanged();
            Invalidate();
            return;
        }

        if (Map is null)
        {
            Cursor = Cursors.Cross;
            return;
        }

        var w = ScreenToWorld(e.Location);
        UpdateMapEventMarkerHover(w.X, w.Y);
        var tx = (int)Math.Floor(w.X / TileSize);
        var ty = (int)Math.Floor(w.Y / TileSize);
        if (tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height)
        {
            var nextHover = new Point(tx, ty);
            var hoverMoved = nextHover != _hoverTile;
            HoveredTileChanged?.Invoke(nextHover);
            _hoverTile = nextHover;
            if (hoverMoved && ActiveTool == EditorTool.Place && (e.Button & MouseButtons.Left) == 0)
            {
                Invalidate();
            }
            else if (hoverMoved && BrushGhostVisible())
            {
                Invalidate();
            }
        }
        else if (ActiveTool == EditorTool.Selection && _selectionMarqueeAnchor is not null)
        {
            var clamped = new Point(Math.Clamp(tx, 0, Map.Width - 1), Math.Clamp(ty, 0, Map.Height - 1));
            if (clamped != _hoverTile)
            {
                HoveredTileChanged?.Invoke(clamped);
                _hoverTile = clamped;
            }
        }

        UpdateEditCursorForHover();

        if (_mapEventMarkerGesture)
        {
            return;
        }

        if (QuickNpcPlacementClick is not null)
        {
            return;
        }

        if ((ModifierKeys & Keys.Alt) == Keys.Alt)
        {
            return;
        }

        if ((e.Button & MouseButtons.Left) != 0)
        {
            if (ActiveTool == EditorTool.Region && tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height)
            {
                PaintRegionAt(tx, ty, erase: false);
            }
            else if (ActiveTool == EditorTool.Brush && tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height && IsActiveLayerEditable())
            {
                ApplyBrush(tx, ty);
                Invalidate();
            }
            else if (ActiveTool == EditorTool.Eraser && tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height && IsActiveLayerPaintable())
            {
                ApplyEraser(tx, ty);
                Invalidate();
            }
            else if (ActiveTool == EditorTool.Prefab && tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height)
            {
                if (_draggingPrefab is not null)
                {
                    TryMoveDraggingPrefabTo(tx, ty);
                }
                else
                {
                    TryPlaceSelectedPrefab(tx, ty);
                    Invalidate();
                }
            }
            else if (ActiveTool == EditorTool.Place
                     && _draggingPlacedId is Guid dragId
                     && tx >= 0
                     && ty >= 0
                     && tx < Map.Width
                     && ty < Map.Height
                     && MapPlacedEntityEdit.TryMove(_placedEntities, Map, dragId, tx, ty))
            {
                var moved = SelectedPlacedEntity;
                if (moved is not null && (moved.TileX != _placedDragOrigin.X || moved.TileY != _placedDragOrigin.Y))
                {
                    _placedDragMoved = true;
                }

                Invalidate();
            }
            else if (ActiveTool is EditorTool.Rectangle or EditorTool.Selection or EditorTool.Line
                     && (_rectPaintOrigin is not null || _selectionMarqueeAnchor is not null || _linePaintOrigin is not null))
            {
                Invalidate();
            }
        }

        if (!_suppressRightButtonErase &&
            ActiveTool == EditorTool.Prefab &&
            (e.Button & MouseButtons.Right) != 0 &&
            tx >= 0 &&
            ty >= 0 &&
            tx < Map.Width &&
            ty < Map.Height)
        {
            TryErasePrefabAt(tx, ty);
            Invalidate();
        }

        if (!_suppressRightButtonErase &&
            ActiveTool == EditorTool.Eraser &&
            (e.Button & MouseButtons.Right) != 0 &&
            tx >= 0 &&
            ty >= 0 &&
            tx < Map.Width &&
            ty < Map.Height &&
            IsActiveLayerPaintable())
        {
            ApplyEraser(tx, ty);
            Invalidate();
        }

        if (!_suppressRightButtonErase &&
            ActiveTool == EditorTool.Place &&
            (e.Button & MouseButtons.Right) != 0 &&
            tx >= 0 &&
            ty >= 0 &&
            tx < Map.Width &&
            ty < Map.Height)
        {
            TryRemovePlacedEntityAt(tx, ty);
        }

        if (!_suppressRightButtonErase &&
            ActiveTool == EditorTool.Region &&
            (e.Button & MouseButtons.Right) != 0 &&
            tx >= 0 &&
            ty >= 0 &&
            tx < Map.Width &&
            ty < Map.Height)
        {
            PaintRegionAt(tx, ty, erase: true);
        }

        if (!_suppressRightButtonErase &&
            ActiveTool != EditorTool.Region &&
            ActiveTool != EditorTool.Spawn &&
            ActiveTool != EditorTool.Prefab &&
            ActiveTool != EditorTool.Place &&
            ActiveTool != EditorTool.Line &&
            ActiveTool != EditorTool.Fill &&
            ActiveTool != EditorTool.Rectangle &&
            ActiveTool != EditorTool.Selection &&
            ActiveTool != EditorTool.Eraser &&
            (e.Button & MouseButtons.Right) != 0 &&
            tx >= 0 &&
            ty >= 0 &&
            tx < Map.Width &&
            ty < Map.Height &&
            IsActiveLayerEditable())
        {
            EraseStamp(tx, ty);
            Invalidate();
        }
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Middle)
        {
            _panning = false;
            Cursor = Cursors.Cross;
            NotifyViewTransformChanged();
        }

            if (e.Button == MouseButtons.Left && _mapEventMarkerGesture)
            {
                _mapEventMarkerGesture = false;
                Capture = false;
                return;
            }

            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
            if (_paintStroke)
            {
                _paintStroke = false;
                Capture = false;
            }

            if (_draggingPrefab is not null && e.Button == MouseButtons.Left)
            {
                EndPrefabMove();
                Capture = false;
            }

            if (_draggingPlacedId is not null && e.Button == MouseButtons.Left)
            {
                var moved = _placedDragMoved;
                _draggingPlacedId = null;
                _placedDragMoved = false;
                Capture = false;
                if (moved)
                {
                    PlacedEntitiesChanged?.Invoke();
                    PlacedEntitySelectionChanged?.Invoke();
                }
            }

            if (e.Button == MouseButtons.Right)
            {
                _suppressRightButtonErase = false;
            }

            if (ActiveTool == EditorTool.Region)
            {
                _regionStroke = false;
                Capture = false;
            }

            if (Map is null)
            {
                return;
            }

            if (ActiveTool == EditorTool.Rectangle && e.Button == MouseButtons.Left && _rectPaintOrigin is not null)
            {
                var world = ScreenToWorld(e.Location);
                var ex = (int)Math.Floor(world.X / TileSize);
                var ey = (int)Math.Floor(world.Y / TileSize);
                CommitRectangle(ex, ey, ShapeShiftOutline());
            }

            if (ActiveTool == EditorTool.Line && e.Button == MouseButtons.Left && _linePaintOrigin is not null)
            {
                var world = ScreenToWorld(e.Location);
                var ex = (int)Math.Floor(world.X / TileSize);
                var ey = (int)Math.Floor(world.Y / TileSize);
                CommitLineAt(ex, ey, LineAxisConstrained());
            }

            if (ActiveTool == EditorTool.Selection && e.Button == MouseButtons.Left && _selectionMarqueeAnchor is { } sa)
            {
                var world = ScreenToWorld(e.Location);
                var ex = Math.Clamp((int)Math.Floor(world.X / TileSize), 0, Map.Width - 1);
                var ey = Math.Clamp((int)Math.Floor(world.Y / TileSize), 0, Map.Height - 1);
                _hoverTile = new Point(ex, ey);
                TryCommitSelectionTiles(sa.X, sa.Y, ex, ey);
                Capture = false;
                NotifyPaintGesture();
                RaiseTileClicked(ex, ey);
            }
        }
    }

    private bool IsActiveLayerEditable()
    {
        if (Map is null || ActiveLayerIndex < 0 || ActiveLayerIndex >= Map.Layers.Count)
        {
            return false;
        }

        return !Map.Layers[ActiveLayerIndex].Locked;
    }

    /// <summary>Le pot refuse une couche masquée (œil) ou verrouillée (cadenas).</summary>
    private bool IsActiveLayerPaintable()
    {
        if (!IsActiveLayerEditable() || Map is null)
        {
            return false;
        }

        return Map.Layers[ActiveLayerIndex].Visible;
    }

    private void UpdateEditCursorForHover()
    {
        if (_panning)
        {
            return;
        }

        if (Map is null)
        {
            Cursor = Cursors.Cross;
            return;
        }

        if (ShowMapEventMarkers && _hoveredMapEventMarker is not null)
        {
            Cursor = Cursors.Hand;
            return;
        }

        if (ActiveTool is EditorTool.Cursor or EditorTool.Selection or EditorTool.Spawn or EditorTool.Place)
        {
            Cursor = ActiveTool == EditorTool.Place && _draggingPlacedId is not null
                ? Cursors.SizeAll
                : Cursors.Cross;
            return;
        }

        if (ActiveTool == EditorTool.Prefab)
        {
            Cursor = _draggingPrefab is not null ? Cursors.SizeAll : Cursors.Cross;
            return;
        }

        var blocked = ActiveTool is EditorTool.Rectangle or EditorTool.Fill or EditorTool.Eraser
            ? !IsActiveLayerPaintable()
            : !IsActiveLayerEditable();
        Cursor = blocked ? Cursors.No : Cursors.Cross;
    }

    private void ApplyBrush(int tx, int ty)
    {
        if (Map is null || !IsActiveLayerEditable())
        {
            return;
        }

        if (IsTileAssetMap)
        {
            ApplyTileAssetStamp(tx, ty);
            return;
        }

        if (!TilesetCache.TryGet(ActiveTilesetId, out var bmp) || bmp is null)
        {
            return;
        }

        EnsureLayerExists();
        var ts = TileSize;
        var sw = Math.Max(1, SelectedStampInTiles.Width);
        var sh = Math.Max(1, SelectedStampInTiles.Height);
        for (var dy = 0; dy < sh; dy++)
        {
            for (var dx = 0; dx < sw; dx++)
            {
                var sx = SelectedSrc.X + dx * ts;
                var sy = SelectedSrc.Y + dy * ts;
                CanonicalizeStoredSource(ref sx, ref sy);
                if (sx < 0 || sy < 0 || sx + ts > bmp.Width || sy + ts > bmp.Height)
                {
                    continue;
                }

                var mx = tx + dx;
                var my = ty + dy;
                if (mx < 0 || my < 0 || mx >= Map.Width || my >= Map.Height)
                {
                    continue;
                }

                MapEditOperations.PaintTile(Map, ActiveLayerIndex, mx, my, CreateBrushTile(mx, my, sx, sy));
            }
        }
    }

    private Tile CreateBrushTile(int tx, int ty, int srcX, int srcY)
    {
        var tile = new Tile
        {
            X = tx,
            Y = ty,
            TilesetId = ActiveTilesetId,
            SrcX = srcX,
            SrcY = srcY,
            Type = SelectedTileType
        };
        if (SelectedTileType == TileType.Warp)
        {
            tile.WarpTargetMapId = DefaultWarpTargetMapId ?? Guid.Empty;
            tile.WarpTargetX = 0;
            tile.WarpTargetY = 0;
        }

        if (SelectedTileType == TileType.Script)
        {
            tile.ScriptId = string.Empty;
        }

        return tile;
    }

    private bool CommitRectangle(int ex, int ey, bool shiftOutline)
    {
        if (Map is null || _rectPaintOrigin is not { } origin)
        {
            return false;
        }

        ex = Math.Clamp(ex, 0, Math.Max(0, Map.Width - 1));
        ey = Math.Clamp(ey, 0, Math.Max(0, Map.Height - 1));
        var painted = false;
        if (IsActiveLayerPaintable())
        {
            var options = CurrentShapeOptions(shiftOutline);
            painted = ApplyShape(origin.X, origin.Y, ex, ey, options);
            if (painted)
            {
                RaiseTileClicked(ex, ey);
            }
        }

        _rectPaintOrigin = null;
        Capture = false;
        NotifyPaintGesture();
        return painted;
    }

    private ShapeStampOptions CurrentShapeOptions(bool shiftOutline) => new()
    {
        Outline = RectangleOutline || shiftOutline,
        Ellipse = RectangleEllipse,
    };

    private static bool ShapeShiftOutline() => (ModifierKeys & Keys.Shift) == Keys.Shift;

    private void ApplyTileAssetStamp(int tx, int ty)
    {
        if (Map is null || !HasTileAssetBrush())
        {
            return;
        }

        EnsureLayerExists();
        var sw = Math.Max(1, SelectedStampInTiles.Width);
        var sh = Math.Max(1, SelectedStampInTiles.Height);
        for (var dy = 0; dy < sh; dy++)
        {
            for (var dx = 0; dx < sw; dx++)
            {
                var mx = tx + dx;
                var my = ty + dy;
                TileAssetMapEditing.TryPaint(Map, ActiveLayerIndex, mx, my, CreateTileAssetBrushTile(mx, my), JoinAutotiles);
            }
        }
    }

    private Tile CreateTileAssetBrushTile(int tx, int ty)
    {
        var tile = TileAssetMapEditing.CreateBrushTile(tx, ty, ActiveTileAssetId, SelectedTileType);
        if (SelectedTileType == TileType.Warp)
        {
            tile.WarpTargetMapId = DefaultWarpTargetMapId ?? Guid.Empty;
            tile.WarpTargetX = 0;
            tile.WarpTargetY = 0;
        }

        if (SelectedTileType == TileType.Script)
        {
            tile.ScriptId = string.Empty;
        }

        return tile;
    }

    private void DrawTileAssetImage(
        Graphics g,
        TileAssetId id,
        Rectangle destination,
        float? alpha = null,
        ImageAttributes? sharedAttrs = null)
    {
        if (TileAssets is null || id.IsNone)
        {
            return;
        }

        var bitmap = TileAssetThumbnails.Get(TileAssets, id);
        if (bitmap is null)
        {
            return;
        }

        if (sharedAttrs is not null)
        {
            g.DrawImage(bitmap, destination, 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, sharedAttrs);
            return;
        }

        if (alpha is null)
        {
            g.DrawImage(bitmap, destination);
            return;
        }

        using var attrs = new System.Drawing.Imaging.ImageAttributes();
        attrs.SetColorMatrix(
            new System.Drawing.Imaging.ColorMatrix { Matrix33 = alpha.Value },
            System.Drawing.Imaging.ColorMatrixFlag.Default,
            System.Drawing.Imaging.ColorAdjustType.Bitmap);
        g.DrawImage(bitmap, destination, 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attrs);
    }

    /// <summary>
    /// Gomme : retire le tampon sur la couche active seulement.
    /// Couche masquée ou verrouillée : aucun effet. Un glisser = un pas d'annulation,
    /// posé seulement si au moins une tuile disparaît.
    /// </summary>
    private int ApplyEraser(int tx, int ty)
    {
        if (Map is null || !IsActiveLayerPaintable())
        {
            return 0;
        }

        var sw = Math.Max(1, SelectedStampInTiles.Width);
        var sh = Math.Max(1, SelectedStampInTiles.Height);
        var removed = MapEditOperations.EraseStamp(Map, ActiveLayerIndex, tx, ty, sw, sh, BeginPaintStroke);
        if (removed > 0)
        {
            ReconcileAutotileLayer();
        }

        return removed;
    }

    private void EraseAt(int tx, int ty)
    {
        if (Map is null)
        {
            return;
        }

        MapEditOperations.EraseTile(Map, ActiveLayerIndex, tx, ty);
    }

    private void EraseStamp(int tx, int ty)
    {
        if (Map is null || ActiveLayerIndex < 0 || ActiveLayerIndex >= Map.Layers.Count || !IsActiveLayerEditable())
        {
            return;
        }

        var sw = Math.Max(1, SelectedStampInTiles.Width);
        var sh = Math.Max(1, SelectedStampInTiles.Height);
        for (var dy = 0; dy < sh; dy++)
        {
            for (var dx = 0; dx < sw; dx++)
            {
                var mx = tx + dx;
                var my = ty + dy;
                if (mx >= 0 && my >= 0 && mx < Map.Width && my < Map.Height)
                {
                    EraseAt(mx, my);
                }
            }
        }
    }

    /// <summary>
    /// Un glisser = un pas d'annulation, seulement si au moins une case est peinte.
    /// Couche masquée ou verrouillée : aucun effet. Feuille : le tampon se répète.
    /// TileAsset : le même id sur chaque case de la forme.
    /// </summary>
    private bool ApplyShape(int x0, int y0, int x1, int y1, ShapeStampOptions options)
    {
        if (Map is null || !IsActiveLayerPaintable())
        {
            return false;
        }

        var cells = MapEditOperations.EnumerateShape(x0, y0, x1, y1, options);
        var minX = Math.Min(x0, x1);
        var minY = Math.Min(y0, y1);
        if (IsTileAssetMap)
        {
            if (!HasTileAssetBrush())
            {
                return false;
            }

            var paint = new List<(int X, int Y)>();
            foreach (var (x, y) in cells)
            {
                if (x >= 0 && y >= 0 && x < Map.Width && y < Map.Height)
                {
                    paint.Add((x, y));
                }
            }

            if (paint.Count == 0)
            {
                return false;
            }

            BeginEditTransaction();
            EnsureLayerExists();
            foreach (var (x, y) in paint)
            {
                TileAssetMapEditing.TryPaint(Map, ActiveLayerIndex, x, y, CreateTileAssetBrushTile(x, y), JoinAutotiles);
            }

            return true;
        }

        if (!TilesetCache.TryGet(ActiveTilesetId, out var bmpR) || bmpR is null)
        {
            return false;
        }

        var ts = TileSize;
        var stw = Math.Max(1, SelectedStampInTiles.Width);
        var sth = Math.Max(1, SelectedStampInTiles.Height);
        var stamps = new List<(int X, int Y, int SrcX, int SrcY)>();
        foreach (var (x, y) in cells)
        {
            if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height)
            {
                continue;
            }

            var dx = (x - minX) % stw;
            var dy = (y - minY) % sth;
            var sx = SelectedSrc.X + dx * ts;
            var sy = SelectedSrc.Y + dy * ts;
            CanonicalizeStoredSource(ref sx, ref sy);
            if (sx < 0 || sy < 0 || sx + ts > bmpR.Width || sy + ts > bmpR.Height)
            {
                continue;
            }

            stamps.Add((x, y, sx, sy));
        }

        if (stamps.Count == 0)
        {
            return false;
        }

        BeginEditTransaction();
        EnsureLayerExists();
        foreach (var (x, y, sx, sy) in stamps)
        {
            MapEditOperations.PaintTile(Map, ActiveLayerIndex, x, y, CreateBrushTile(x, y, sx, sy));
        }

        return true;
    }

    private void RefreshShapePreview(Keys key)
    {
        if (key != Keys.ShiftKey || (_linePaintOrigin is null && _rectPaintOrigin is null))
        {
            return;
        }

        NotifyPaintGesture();
    }

    internal void RefreshShapePreviewForTest() => NotifyPaintGesture();

    /// <summary>Annule le trait ou le rectangle en cours sans peindre. Échap.</summary>
    internal bool TryCancelShapeGesture()
    {
        if (_linePaintOrigin is null && _rectPaintOrigin is null)
        {
            return false;
        }

        _linePaintOrigin = null;
        _rectPaintOrigin = null;
        Capture = false;
        NotifyPaintGesture();
        return true;
    }

    /// <summary>Phrase française pour la barre d'état, geste compris.</summary>
    internal string GetPaintStatusHint()
    {
        if (ActiveTool == EditorTool.Line && _linePaintOrigin is { } origin)
        {
            var axis = LineAxisConstrained();
            var end = ResolveLineEnd(origin, _hoverTile, axis);
            var count = MapEditOperations.EnumerateLine(origin.X, origin.Y, end.X, end.Y).Count;
            return EditorToolHotkeys.FormatLineGesture(origin.X, origin.Y, end.X, end.Y, count, axis);
        }

        if (ActiveTool == EditorTool.Rectangle && _rectPaintOrigin is { } ro)
        {
            var shape = CurrentShapeOptions(ShapeShiftOutline());
            return EditorToolHotkeys.FormatRectangleGesture(ro.X, ro.Y, _hoverTile.X, _hoverTile.Y, shape.Outline, shape.Ellipse);
        }

        if (ActiveTool == EditorTool.Selection && _selectionMarqueeAnchor is { } anchor)
        {
            return WithZoneClipboard(EditorToolHotkeys.FormatSelectionGesture(anchor.X, anchor.Y, _hoverTile.X, _hoverTile.Y));
        }

        if (ActiveTool == EditorTool.Selection && TryGetCommittedSelectionNormalized(out var selection))
        {
            return WithZoneClipboard(EditorToolHotkeys.FormatSelectionCommitted(selection.Width, selection.Height));
        }

        var hint = ActiveTool switch
        {
            EditorTool.Fill => EditorToolHotkeys.FormatFillStatus(FillVisibleUnlockedLayers, FillRespectAttributes),
            EditorTool.Rectangle => EditorToolHotkeys.FormatRectangleStatus(RectangleOutline, RectangleEllipse),
            EditorTool.Place => EditorToolHotkeys.FormatPlaceStatus(PlaceKind, SelectedPlacedEntity?.Name),
            EditorTool.Region => MapRegionLabels.FormatStatus(ActiveRegionId),
            _ => EditorToolHotkeys.StatusHint(ActiveTool),
        };
        if (IsTileAssetMap)
        {
            hint += ActiveTileAssetId.IsNone
                ? " · TileAsset"
                : " · TileAsset " + ActiveTileAssetId.ToHex()[..8];
            if (!ActiveTileAssetId.IsNone
                && Map?.TileFlags is { } flags
                && flags.TryGetExplicit(ActiveTileAssetId, out var brushFlags))
            {
                hint += " · " + TileAssetFlagLabels.FormatBrush(brushFlags);
            }
        }

        if (!IsTileAssetMap
            && TilesetAnimCatalog.TryFrameCount(ActiveTilesetId, SelectedSrc.X, SelectedSrc.Y, out var frames)
            && ActiveTool is EditorTool.Brush or EditorTool.Fill or EditorTool.Rectangle or EditorTool.Line)
        {
            return hint + " · " + EditorToolHotkeys.FormatAnimatedTilePreview(frames, TilesetAnimCatalog.PreviewEnabled);
        }

        if (ActiveTool == EditorTool.Selection)
        {
            hint = WithZoneClipboard(hint);
        }

        return hint + JoinPreviewStatusSuffix();
    }

    private void CanonicalizeStoredSource(ref int sx, ref int sy)
    {
        TilesetAnimCatalog.CanonicalizePaintSource(
            ActiveTilesetId,
            SelectedSrc.X,
            SelectedSrc.Y,
            Math.Max(1, SelectedStampInTiles.Width),
            Math.Max(1, SelectedStampInTiles.Height),
            TileSize,
            ref sx,
            ref sy);
    }

    private void PreviewSource(int tilesetId, Bitmap bmp, ref int sx, ref int sy)
    {
        if (TilesetAnimCatalog.TryResolveDrawSource(
                tilesetId,
                sx,
                sy,
                TileSize,
                TilesetAnimCatalog.PreviewElapsedMs,
                bmp.Width,
                bmp.Height,
                out var drawX,
                out var drawY))
        {
            sx = drawX;
            sy = drawY;
        }
    }

    private static int PlacedAnimTilesetId(Tile tile) => tile.TilesetId;

    private Dictionary<(int X, int Y), TileAssetId>? _joinPreview;

    private Dictionary<(int X, int Y), TileAssetId> BuildJoinPreview(out HashSet<(int X, int Y)> stamp)
    {
        var cells = GhostStampCells();
        stamp = new HashSet<(int X, int Y)>(cells);
        var dict = new Dictionary<(int X, int Y), TileAssetId>();
        if (Map is null || !IsTileAssetMap || ActiveTileAssetId.IsNone || cells.Count == 0)
        {
            return dict;
        }

        if (!JoinAutotiles)
        {
            foreach (var cell in cells)
            {
                dict[cell] = ActiveTileAssetId;
            }

            return dict;
        }

        foreach (var cell in global::Frog.Core.Maps.AutotileJoin.PreviewStamp(Map, ActiveLayerIndex, ActiveTileAssetId, cells))
        {
            dict[(cell.X, cell.Y)] = cell.Id;
        }

        return dict;
    }

    private List<(int X, int Y)> GhostStampCells()
    {
        var cells = new List<(int X, int Y)>();
        if (Map is null)
        {
            return cells;
        }

        if (ActiveTool == EditorTool.Line && _linePaintOrigin is not null)
        {
            foreach (var cell in CurrentLineCells(LineAxisConstrained()))
            {
                if (InMap(cell.X, cell.Y))
                {
                    cells.Add(cell);
                }
            }

            return cells;
        }

        if (ActiveTool == EditorTool.Rectangle && _rectPaintOrigin is { } origin)
        {
            foreach (var cell in MapEditOperations.EnumerateShape(
                         origin.X,
                         origin.Y,
                         _hoverTile.X,
                         _hoverTile.Y,
                         CurrentShapeOptions(ShapeShiftOutline())))
            {
                if (InMap(cell.X, cell.Y))
                {
                    cells.Add(cell);
                }
            }

            return cells;
        }

        var sw = Math.Max(1, SelectedStampInTiles.Width);
        var sh = Math.Max(1, SelectedStampInTiles.Height);
        for (var dy = 0; dy < sh; dy++)
        {
            for (var dx = 0; dx < sw; dx++)
            {
                var x = _hoverTile.X + dx;
                var y = _hoverTile.Y + dy;
                if (InMap(x, y))
                {
                    cells.Add((x, y));
                }
            }
        }

        return cells;
    }

    private bool InMap(int x, int y) =>
        Map is not null && x >= 0 && y >= 0 && x < Map.Width && y < Map.Height;

    private TileAssetId PreviewAssetId(int x, int y)
    {
        if (_joinPreview is not null && _joinPreview.TryGetValue((x, y), out var id) && !id.IsNone)
        {
            return id;
        }

        return ActiveTileAssetId;
    }

    private void DrawAutotileJoinPreview(Graphics g, HashSet<(int X, int Y)> stamp, int mapW, int mapH)
    {
        if (_joinPreview is null || _joinPreview.Count == 0)
        {
            return;
        }

        var ts = TileSize;
        var shape = CurrentShapeOptions(ShapeShiftOutline());
        var filledRect = ActiveTool == EditorTool.Rectangle
                         && _rectPaintOrigin is not null
                         && !shape.Outline
                         && !shape.Ellipse;
        using var pen = new Pen(EditorChrome.RibbonAccent, 1.5f) { DashStyle = DashStyle.Dash };
        foreach (var pair in _joinPreview)
        {
            var x = pair.Key.X;
            var y = pair.Key.Y;
            if (x < 0 || y < 0 || x >= mapW || y >= mapH || pair.Value.IsNone)
            {
                continue;
            }

            var rect = new Rectangle(x * ts, y * ts, ts, ts);
            if (!stamp.Contains(pair.Key))
            {
                DrawTileAssetImage(g, pair.Value, rect, 0.9f);
                g.DrawRectangle(pen, rect);
                continue;
            }

            if (filledRect && pair.Value != ActiveTileAssetId && !CoveredByHoverStamp(x, y))
            {
                DrawTileAssetImage(g, pair.Value, rect, 0.72f);
            }
        }
    }

    private bool CoveredByHoverStamp(int x, int y)
    {
        var sw = Math.Max(1, SelectedStampInTiles.Width);
        var sh = Math.Max(1, SelectedStampInTiles.Height);
        return x >= _hoverTile.X && x < _hoverTile.X + sw && y >= _hoverTile.Y && y < _hoverTile.Y + sh;
    }

    private string JoinPreviewStatusSuffix()
    {
        if (!IsTileAssetMap
            || !JoinAutotiles
            || Map?.TileFlags is not { } flags
            || ActiveTileAssetId.IsNone
            || ActiveTool is not (EditorTool.Brush or EditorTool.Fill or EditorTool.Rectangle or EditorTool.Line)
            || !flags.TryGetExplicit(ActiveTileAssetId, out var brushFlags)
            || brushFlags.AutotileRole == AutotileRole.None
            || string.IsNullOrEmpty(brushFlags.AutotileGroup))
        {
            return string.Empty;
        }

        var preview = BuildJoinPreview(out _);
        if (!preview.TryGetValue((_hoverTile.X, _hoverTile.Y), out var id) || id == ActiveTileAssetId)
        {
            return string.Empty;
        }

        if (!flags.TryGetExplicit(id, out var previewFlags) || previewFlags.AutotileRole == brushFlags.AutotileRole)
        {
            return string.Empty;
        }

        return " · " + TileAssetFlagLabels.FormatJoinPreview(previewFlags.AutotileRole);
    }

    internal IReadOnlyDictionary<(int X, int Y), TileAssetId> PreviewAutotileGhostForTest() => BuildJoinPreview(out _);

    internal bool TryResolveDrawnTileForTest(
        int tileX,
        int tileY,
        long elapsedMs,
        int sheetWidth,
        int sheetHeight,
        out int drawX,
        out int drawY)
    {
        drawX = 0;
        drawY = 0;
        if (Map is null || (uint)ActiveLayerIndex >= (uint)Map.Layers.Count)
        {
            return false;
        }

        var tile = Map.Layers[ActiveLayerIndex].TileAt(tileX, tileY);

        if (tile is null)
        {
            return false;
        }

        return TilesetAnimCatalog.TryResolveDrawSource(
            PlacedAnimTilesetId(tile),
            tile.SrcX,
            tile.SrcY,
            TileSize,
            elapsedMs,
            sheetWidth,
            sheetHeight,
            out drawX,
            out drawY);
    }

    internal bool TryResolveAnimDrawSourceForTest(
        int tilesetId,
        int srcX,
        int srcY,
        long elapsedMs,
        int sheetWidth,
        int sheetHeight,
        out int drawX,
        out int drawY) =>
        TilesetAnimCatalog.TryResolveDrawSource(
            tilesetId,
            srcX,
            srcY,
            TileSize,
            elapsedMs,
            sheetWidth,
            sheetHeight,
            out drawX,
            out drawY);

    private void NotifyPaintGesture()
    {
        Invalidate();
        PaintGestureChanged?.Invoke();
    }

    private static bool LineAxisConstrained() => (ModifierKeys & Keys.Shift) == Keys.Shift;

    private static Point ResolveLineEnd(Point origin, Point end, bool axisAligned)
    {
        if (!axisAligned)
        {
            return end;
        }

        var constrained = MapEditOperations.ConstrainToDominantAxis(origin.X, origin.Y, end.X, end.Y);
        return new Point(constrained.X, constrained.Y);
    }

    private IReadOnlyList<(int X, int Y)> CurrentLineCells(bool axisAligned)
    {
        if (_linePaintOrigin is not { } origin)
        {
            return Array.Empty<(int, int)>();
        }

        var end = ResolveLineEnd(origin, _hoverTile, axisAligned);
        return MapEditOperations.EnumerateLine(origin.X, origin.Y, end.X, end.Y);
    }

    private bool CommitLineAt(int ex, int ey, bool axisAligned)
    {
        if (Map is null || _linePaintOrigin is not { } origin)
        {
            return false;
        }

        ex = Math.Clamp(ex, 0, Map.Width - 1);
        ey = Math.Clamp(ey, 0, Map.Height - 1);
        var end = ResolveLineEnd(origin, new Point(ex, ey), axisAligned);
        var painted = false;
        if (IsActiveLayerEditable())
        {
            BeginEditTransaction();
            ApplyLine(origin.X, origin.Y, end.X, end.Y);
            painted = Map.Layers[ActiveLayerIndex].TileAt(origin.X, origin.Y) is not null;
            RaiseTileClicked(end.X, end.Y);
        }

        _linePaintOrigin = null;
        Capture = false;
        NotifyPaintGesture();
        return painted;
    }

    private void ApplyLine(int x0, int y0, int x1, int y1)
    {
        if (Map is null || !IsActiveLayerEditable())
        {
            return;
        }

        if (IsTileAssetMap)
        {
            if (!HasTileAssetBrush())
            {
                return;
            }

            EnsureLayerExists();
            MapEditOperations.PaintLine(Map, ActiveLayerIndex, x0, y0, x1, y1, CreateTileAssetBrushTile(x0, y0));
            ReconcileAutotileLayer();
            return;
        }

        if (!TilesetCache.TryGet(ActiveTilesetId, out var bmp) || bmp is null)
        {
            return;
        }

        var ts = TileSize;
        var sx = SelectedSrc.X;
        var sy = SelectedSrc.Y;
        if (sx < 0 || sy < 0 || sx + ts > bmp.Width || sy + ts > bmp.Height)
        {
            return;
        }

        EnsureLayerExists();
        MapEditOperations.PaintLine(Map, ActiveLayerIndex, x0, y0, x1, y1, CreateBrushTile(x0, y0, sx, sy));
    }

    /// <summary>
    /// Pot de peinture. <paramref name="erase"/> vrai (clic droit) efface la région.
    /// Un clic gauche sans tuile sélectionnée (TileAsset vide, ou feuille hors tampon) efface aussi.
    /// Un seul pas d'annulation, posé seulement si au moins une case change.
    /// </summary>
    private int FloodFill(int sx, int sy, bool erase)
    {
        if (Map is null || !IsActiveLayerPaintable())
        {
            return 0;
        }

        if (sx < 0 || sy < 0 || sx >= Map.Width || sy >= Map.Height)
        {
            return 0;
        }

        var replacement = erase ? null : TryCreateFillStamp();
        var multi = FillVisibleUnlockedLayers || (ModifierKeys & Keys.Control) == Keys.Control;
        var options = new FloodFillOptions
        {
            VisibleUnlockedLayers = multi,
            RespectAttributes = FillRespectAttributes,
        };
        var filled = MapEditOperations.FloodFill(
            Map,
            ActiveLayerIndex,
            sx,
            sy,
            replacement,
            options,
            BeginEditTransaction);
        if (filled > 0 && IsTileAssetMap && JoinAutotiles)
        {
            if (multi)
            {
                for (var i = 0; i < Map.Layers.Count; i++)
                {
                    if (MapEditOperations.IsLayerPaintable(Map, i))
                    {
                        global::Frog.Core.Maps.AutotileJoin.ReconcileLayer(Map, i);
                    }
                }
            }
            else
            {
                ReconcileAutotileLayer();
            }
        }

        return filled;
    }

    private void ReconcileAutotileLayer()
    {
        if (!JoinAutotiles || Map is null || !IsTileAssetMap)
        {
            return;
        }

        global::Frog.Core.Maps.AutotileJoin.ReconcileLayer(Map, ActiveLayerIndex);
    }

    /// <summary>Tampon courant, ou null si la sélection est vide (le pot efface alors la région).</summary>
    private Tile? TryCreateFillStamp()
    {
        if (IsTileAssetMap)
        {
            return HasTileAssetBrush() ? CreateTileAssetBrushTile(0, 0) : null;
        }

        if (ActiveTilesetId <= 0 || !TilesetCache.TryGet(ActiveTilesetId, out var bmp) || bmp is null)
        {
            return null;
        }

        var ts = TileSize;
        var srcX = SelectedSrc.X;
        var srcY = SelectedSrc.Y;
        if (srcX < 0 || srcY < 0 || srcX + ts > bmp.Width || srcY + ts > bmp.Height)
        {
            return null;
        }

        return CreateBrushTile(0, 0, srcX, srcY);
    }

    internal int TryFloodFillForTest(int x, int y, bool erase = false)
    {
        ActiveTool = EditorTool.Fill;
        return FloodFill(x, y, erase);
    }

    /// <summary>Un clic de gomme : efface le tampon puis termine le trait (un pas d'annulation).</summary>
    internal int TryEraseStampForTest(int x, int y)
    {
        ActiveTool = EditorTool.Eraser;
        var removed = ApplyEraser(x, y);
        EndPaintStrokeForTest();
        return removed;
    }

    /// <summary>Poursuit le trait de gomme en cours, sans nouveau pas d'annulation.</summary>
    internal int TryContinueEraserStrokeForTest(int x, int y)
    {
        ActiveTool = EditorTool.Eraser;
        return ApplyEraser(x, y);
    }

    internal void EndPaintStrokeForTest()
    {
        _paintStroke = false;
        Capture = false;
    }

    internal bool TryPaintTileForTest(int x, int y)
    {
        if (Map is null || !IsActiveLayerEditable())
        {
            return false;
        }

        if (!TilesetCache.TryGet(ActiveTilesetId, out var bmp) || bmp is null)
        {
            return false;
        }

        BeginEditTransaction();
        ApplyBrush(x, y);
        Invalidate();
        return Map.Layers[ActiveLayerIndex].TileAt(x, y) is not null;
    }

    internal bool TryBeginRectangleDragForTest(int x, int y)
    {
        if (Map is null || !IsActiveLayerPaintable())
        {
            return false;
        }

        if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height)
        {
            return false;
        }

        ActiveTool = EditorTool.Rectangle;
        _rectPaintOrigin = new Point(x, y);
        _hoverTile = new Point(x, y);
        return true;
    }

    internal IReadOnlyList<(int X, int Y)> GetRectanglePreviewCellsForTest(bool shiftOutline = false)
    {
        if (_rectPaintOrigin is not { } origin)
        {
            return Array.Empty<(int, int)>();
        }

        return MapEditOperations.EnumerateShape(
            origin.X,
            origin.Y,
            _hoverTile.X,
            _hoverTile.Y,
            CurrentShapeOptions(shiftOutline));
    }

    internal bool TryCommitRectangleDragForTest(int x, int y, bool shiftOutline = false)
    {
        if (Map is null || _rectPaintOrigin is null)
        {
            return false;
        }

        return CommitRectangle(x, y, shiftOutline);
    }

    internal bool TryBeginLineDragForTest(int x, int y)
    {
        if (Map is null || !IsActiveLayerEditable())
        {
            return false;
        }

        if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height)
        {
            return false;
        }

        ActiveTool = EditorTool.Line;
        _linePaintOrigin = new Point(x, y);
        _hoverTile = new Point(x, y);
        return true;
    }

    internal IReadOnlyList<(int X, int Y)> GetLinePreviewCellsForTest(bool axisAligned) => CurrentLineCells(axisAligned);

    internal bool TryCommitLineDragForTest(int x, int y, bool axisAligned = false)
    {
        if (Map is null || _linePaintOrigin is null)
        {
            return false;
        }

        if (!TilesetCache.TryGet(ActiveTilesetId, out var bmp) || bmp is null)
        {
            _linePaintOrigin = null;
            return false;
        }

        return CommitLineAt(x, y, axisAligned);
    }

    internal void SetHoverTileForTest(int x, int y) => _hoverTile = new Point(x, y);

    internal Rectangle? GetPasteFootprintForTest()
        => TryGetPasteFootprint(_hoverTile.X, _hoverTile.Y, out var rect) ? rect : null;

    internal void CommitSelectionForTest(int x, int y, int width, int height)
        => _committedSelectionTiles = new Rectangle(x, y, width, height);

    internal Rectangle? GetCommittedSelectionForTest() => _committedSelectionTiles;

    internal bool TryPipetteAtForTest(int x, int y, bool switchToBrush) => TryPipetteAt(x, y, switchToBrush);

    internal bool TryHandleAltLeftClickForTest(int tileX, int tileY)
    {
        if (Map is null || tileX < 0 || tileY < 0 || tileX >= Map.Width || tileY >= Map.Height)
        {
            return false;
        }

        return TryPipetteAt(tileX, tileY, switchToBrush: false);
    }

    internal void SetBlockTileForTest(int x, int y)
    {
        if (Map is null || !IsActiveLayerEditable())
        {
            return;
        }

        BeginEditTransaction();
        MapEditOperations.SetBlockTile(Map, ActiveLayerIndex, x, y);
        Invalidate();
    }

    internal void SetWarpTileForTest(int x, int y, Guid targetMapId, int targetX, int targetY)
    {
        if (Map is null || !IsActiveLayerEditable())
        {
            return;
        }

        BeginEditTransaction();
        MapEditOperations.SetWarpDestination(Map, ActiveLayerIndex, x, y, targetMapId, targetX, targetY);
        Invalidate();
    }

    internal void SetLayerVisibilityForTest(int layerIndex, bool visible)
    {
        if (Map is null || layerIndex < 0 || layerIndex >= Map.Layers.Count)
        {
            return;
        }

        BeginEditTransaction();
        MapEditOperations.SetLayerVisibility(Map, layerIndex, visible);
        Invalidate();
    }

    internal void SetMapNameForTest(string name)
    {
        if (Map is null)
        {
            return;
        }

        BeginEditTransaction();
        Map.Name = name;
        Invalidate();
    }

    private void EnsureLayerExists()
    {
        if (Map is null)
        {
            return;
        }

        while (Map.Layers.Count <= ActiveLayerIndex)
        {
            Map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        }
    }

    private bool TryGetCommittedSelectionNormalized(out Rectangle rect)
    {
        rect = default;
        if (_committedSelectionTiles is not { Width: > 0, Height: > 0 } r)
        {
            return false;
        }

        rect = r;
        return true;
    }

    private bool TryGetPasteFootprint(int anchorX, int anchorY, out Rectangle rect)
    {
        rect = default;
        if (Map is null || !EditorTileClipboard.HasContent || EditorTileClipboard.Width <= 0 || EditorTileClipboard.Height <= 0)
        {
            return false;
        }

        var x0 = Math.Max(0, anchorX);
        var y0 = Math.Max(0, anchorY);
        var x1 = Math.Min(Map.Width, anchorX + EditorTileClipboard.Width);
        var y1 = Math.Min(Map.Height, anchorY + EditorTileClipboard.Height);
        if (x1 <= x0 || y1 <= y0)
        {
            return false;
        }

        rect = new Rectangle(x0, y0, x1 - x0, y1 - y0);
        return true;
    }

    private string WithZoneClipboard(string hint)
    {
        if (!EditorTileClipboard.HasContent || EditorTileClipboard.Width <= 0 || EditorTileClipboard.Height <= 0)
        {
            return hint;
        }

        return hint + " · " + EditorToolHotkeys.FormatZoneClipboard(
            EditorTileClipboard.Width,
            EditorTileClipboard.Height,
            EditorTileClipboard.IsSingleLayer);
    }

    private void FreezeLiveSelection()
    {
        if (_selectionMarqueeAnchor is not { } anchor)
        {
            return;
        }

        TryCommitSelectionTiles(anchor.X, anchor.Y, _hoverTile.X, _hoverTile.Y);
        Capture = false;
        NotifyPaintGesture();
    }

    private bool TryCommitSelectionTiles(int ax, int ay, int bx, int by)
    {
        if (Map is null || Map.Width <= 0 || Map.Height <= 0)
        {
            return false;
        }

        ax = Math.Clamp(ax, 0, Map.Width - 1);
        ay = Math.Clamp(ay, 0, Map.Height - 1);
        bx = Math.Clamp(bx, 0, Map.Width - 1);
        by = Math.Clamp(by, 0, Map.Height - 1);
        var x0 = Math.Min(ax, bx);
        var y0 = Math.Min(ay, by);
        var x1 = Math.Max(ax, bx);
        var y1 = Math.Max(ay, by);
        _committedSelectionTiles = new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        _selectionMarqueeAnchor = null;
        return true;
    }

    private void EraseSelection(Rectangle tileRect, bool activeLayerOnly)
    {
        if (Map is null)
        {
            return;
        }

        if (activeLayerOnly)
        {
            MapEditOperations.EraseRectangle(Map, ActiveLayerIndex, tileRect.X, tileRect.Y, tileRect.Width, tileRect.Height);
            return;
        }

        for (var i = 0; i < Map.Layers.Count; i++)
        {
            MapEditOperations.EraseRectangle(Map, i, tileRect.X, tileRect.Y, tileRect.Width, tileRect.Height);
        }
    }

    private void RaiseTileClicked(int tileX, int tileY)
    {
        if (Map?.Layers is null || Map.Layers.Count == 0)
        {
            TileClicked?.Invoke(null);
            return;
        }

        if (ActiveLayerIndex < 0 || ActiveLayerIndex >= Map.Layers.Count)
        {
            TileClicked?.Invoke(null);
            return;
        }

        var layer = Map.Layers[ActiveLayerIndex];
        var tile = layer.TileAt(tileX, tileY);
        TileClicked?.Invoke(tile);
    }

    private bool TryPickMapEventMarker(float worldX, float worldY)
    {
        if (!TryHitMapEventMarker(worldX, worldY, out var hit))
        {
            return false;
        }

        HighlightMapEventMarker(hit.TileX, hit.TileY, hit.PrimaryPlacementKey);
        MapEventMarkerPicked?.Invoke(hit);
        return true;
    }

    private bool TryHitMapEventMarker(float worldX, float worldY, out MapEventMarkerView hit)
    {
        hit = default;
        if (!ShowMapEventMarkers || _mapEventMarkers is null || Map is null)
        {
            return false;
        }

        for (var i = _mapEventMarkers.Count - 1; i >= 0; i--)
        {
            var marker = _mapEventMarkers[i];
            if (marker.TileX < 0 || marker.TileY < 0 || marker.TileX >= Map.Width || marker.TileY >= Map.Height)
            {
                continue;
            }

            var label = MapEventMarkerLayout.FormatLabel(marker.PrimaryDisplayName, marker.PrimarySlug, marker.PlacementCount);
            if (MapEventMarkerLayout.HitTest(
                    marker.TileX,
                    marker.TileY,
                    TileSize,
                    worldX,
                    worldY,
                    ShouldDrawNameFor(marker),
                    label))
            {
                hit = marker;
                return true;
            }
        }

        return false;
    }

    private void UpdateMapEventMarkerHover(float worldX, float worldY)
    {
        MapEventMarkerView? next = null;
        if (TryHitMapEventMarker(worldX, worldY, out var hit))
        {
            next = hit;
        }

        if (Nullable.Equals(_hoveredMapEventMarker, next))
        {
            return;
        }

        _hoveredMapEventMarker = next;
        Invalidate();
        MapEventMarkerInteractionChanged?.Invoke();
    }

    private void ClearMapEventMarkerHover()
    {
        if (_hoveredMapEventMarker is null)
        {
            return;
        }

        _hoveredMapEventMarker = null;
        Invalidate();
        MapEventMarkerInteractionChanged?.Invoke();
        UpdateEditCursorForHover();
    }

    private bool ShouldDrawNameFor(MapEventMarkerView marker)
    {
        var hovered = _hoveredMapEventMarker is { } h && h.Equals(marker);
        return ShowMapEventMarkers && MapEventMarkerLayout.ShouldDrawName(
            ShowMapEventNames,
            Zoom,
            TileSize,
            hovered,
            MarkerMatchesSelection(marker));
    }

    private bool MarkerMatchesSelection(MapEventMarkerView marker)
    {
        if (!_hasSelectedMapEvent)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(_selectedMapEventPlacementKey))
        {
            return string.Equals(_selectedMapEventPlacementKey, marker.PrimaryPlacementKey, StringComparison.OrdinalIgnoreCase);
        }

        return marker.TileX == _selectedMapEventTileX && marker.TileY == _selectedMapEventTileY;
    }

    private bool MarkerListContainsSelection(IReadOnlyList<MapEventMarkerView>? markers)
    {
        if (!_hasSelectedMapEvent || markers is null)
        {
            return false;
        }

        foreach (var marker in markers)
        {
            if (MarkerMatchesSelection(marker))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MarkerListContains(IReadOnlyList<MapEventMarkerView>? markers, MapEventMarkerView marker)
    {
        if (markers is null)
        {
            return false;
        }

        foreach (var candidate in markers)
        {
            if (candidate.Equals(marker))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFindSelectedMarker(out MapEventMarkerView marker)
    {
        marker = default;
        if (!_hasSelectedMapEvent || _mapEventMarkers is null)
        {
            return false;
        }

        foreach (var candidate in _mapEventMarkers)
        {
            if (!MarkerMatchesSelection(candidate))
            {
                continue;
            }

            marker = candidate;
            return true;
        }

        return false;
    }

    internal string? SelectedMapEventPlacementKeyForTest =>
        _hasSelectedMapEvent ? _selectedMapEventPlacementKey : null;

    internal bool TryPickMapEventMarkerAtWorldForTest(float worldX, float worldY) =>
        TryPickMapEventMarker(worldX, worldY);

    internal void SetZoomForTest(float zoom)
    {
        Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        Invalidate();
    }

    internal bool ShouldDrawMapEventNameForTest(MapEventMarkerView marker) => ShouldDrawNameFor(marker);

    internal void SetHoveredMapEventAtWorldForTest(float worldX, float worldY) =>
        UpdateMapEventMarkerHover(worldX, worldY);

    internal void RaiseMouseDownForTest(MouseButtons button, int x, int y) =>
        OnMouseDown(this, new MouseEventArgs(button, 1, x, y, 0));

    internal void RaiseMouseMoveForTest(MouseButtons button, int x, int y) =>
        OnMouseMove(this, new MouseEventArgs(button, 1, x, y, 0));

    internal void RaiseMouseUpForTest(MouseButtons button, int x, int y) =>
        OnMouseUp(this, new MouseEventArgs(button, 1, x, y, 0));

    private PointF ScreenToWorld(Point p)
    {
        var x = (p.X - Pan.X) / Zoom;
        var y = (p.Y - Pan.Y) / Zoom;
        return new PointF(x, y);
    }
}

