using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Application.Playtest;

/// <summary>Alloue des identifiants runtime int stables pour des <see cref="Guid"/> canoniques PostgreSQL.</summary>
public sealed class RuntimeMapIdAllocator
{
    private readonly Dictionary<Guid, int> _canonicalToRuntime = new();
    private readonly Dictionary<int, Guid> _runtimeToCanonical = new();
    private int _next = 1;

    public int Allocate(Guid canonicalMapId)
    {
        if (canonicalMapId == Guid.Empty)
        {
            throw new ArgumentException("MapId canonique vide.", nameof(canonicalMapId));
        }

        if (_canonicalToRuntime.TryGetValue(canonicalMapId, out var existing))
        {
            return existing;
        }

        var runtimeId = _next++;
        _canonicalToRuntime[canonicalMapId] = runtimeId;
        _runtimeToCanonical[runtimeId] = canonicalMapId;
        return runtimeId;
    }

    public bool TryGetRuntimeId(Guid canonicalMapId, out int runtimeId)
        => _canonicalToRuntime.TryGetValue(canonicalMapId, out runtimeId);

    public bool TryGetCanonicalId(int runtimeMapId, out Guid canonicalMapId)
        => _runtimeToCanonical.TryGetValue(runtimeMapId, out canonicalMapId!);

    /// <summary>
    /// Réécrit les warps pour que <see cref="MapSamples.RuntimeMapIdToGuid"/> / le serveur
    /// résolvent les destinations via les ints alloués (pas les Guid PostgreSQL bruts).
    /// </summary>
    public Map RewriteWarpsToRuntimeGuids(Map source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var clone = CloneMap(source);
        foreach (var layer in clone.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.WarpTargetMapId == Guid.Empty)
                {
                    continue;
                }

                if (!_canonicalToRuntime.TryGetValue(tile.WarpTargetMapId, out var runtimeId))
                {
                    runtimeId = Allocate(tile.WarpTargetMapId);
                }

                tile.WarpTargetMapId = MapSamples.RuntimeMapIdToGuid(runtimeId);
            }
        }

        return clone;
    }

    private static Map CloneMap(Map source)
    {
        var clone = new Map
        {
            Name = source.Name,
            Width = source.Width,
            Height = source.Height,
            AllowPlayerOverlap = source.AllowPlayerOverlap,
        };
        foreach (var layer in source.Layers)
        {
            var layerClone = new Layer
            {
                LayerType = layer.LayerType,
                DisplayName = layer.DisplayName,
                Visible = layer.Visible,
                Locked = layer.Locked,
            };
            foreach (var tile in layer.Tiles)
            {
                layerClone.Tiles.Add(new Tile
                {
                    X = tile.X,
                    Y = tile.Y,
                    Type = tile.Type,
                    SrcX = tile.SrcX,
                    SrcY = tile.SrcY,
                    TilesetId = tile.TilesetId,
                    WarpTargetMapId = tile.WarpTargetMapId,
                    WarpTargetX = tile.WarpTargetX,
                    WarpTargetY = tile.WarpTargetY,
                    ScriptId = tile.ScriptId,
                });
            }

            clone.Layers.Add(layerClone);
        }

        return clone;
    }
}
