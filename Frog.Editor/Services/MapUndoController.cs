using System.Collections.Generic;
using Frog.Core.IO;
using Frog.Core.Models;

namespace Frog.Editor.Services;

/// <summary>
/// Undo / redo par instantanés sérialisés (.fmap), profondeur limitée.
/// Un sidecar optionnel (octets opaques) suit chaque instantané : taille/décalage y range entités et événements.
/// </summary>
public sealed class MapUndoController
{
    private const int MaxDepth = 40;
    private readonly MapSerializer _serializer = new();
    private readonly List<byte[]> _undo = new();
    private readonly List<byte[]?> _undoSidecar = new();
    private readonly List<byte[]> _redo = new();
    private readonly List<byte[]?> _redoSidecar = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Sidecar de l’instantané que le dernier undo ou redo vient de restaurer.</summary>
    public byte[]? LastRestoredSidecar { get; private set; }

    /// <summary>Enregistre l’état courant avant une modification ; vide la pile redo.</summary>
    public void PushBeforeChange(Map map, byte[]? sidecar = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        PushUndo(_serializer.Serialize(map), sidecar);
    }

    /// <summary>Empile un instantané déjà sérialisé (état d’avant), sans relire la carte courante.</summary>
    public void PushSerializedPrior(byte[] serializedPrior, byte[]? sidecar = null)
    {
        ArgumentNullException.ThrowIfNull(serializedPrior);
        PushUndo(serializedPrior, sidecar);
    }

    public byte[]? PeekUndoSidecar() => _undo.Count == 0 ? null : _undoSidecar[^1];

    public byte[]? PeekRedoSidecar() => _redo.Count == 0 ? null : _redoSidecar[^1];

    public Map? TryUndo(Map current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (_undo.Count == 0)
        {
            return null;
        }

        _redo.Add(_serializer.Serialize(current));
        _redoSidecar.Add(_undoSidecar[^1]);
        LastRestoredSidecar = _undoSidecar[^1];
        var bytes = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _undoSidecar.RemoveAt(_undoSidecar.Count - 1);
        return _serializer.Deserialize(bytes);
    }

    public Map? TryRedo(Map current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (_redo.Count == 0)
        {
            return null;
        }

        _undo.Add(_serializer.Serialize(current));
        _undoSidecar.Add(_redoSidecar[^1]);
        if (_undo.Count > MaxDepth)
        {
            _undo.RemoveAt(0);
            _undoSidecar.RemoveAt(0);
        }

        LastRestoredSidecar = _redoSidecar[^1];
        var bytes = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _redoSidecar.RemoveAt(_redoSidecar.Count - 1);
        return _serializer.Deserialize(bytes);
    }

    public void Clear()
    {
        _undo.Clear();
        _undoSidecar.Clear();
        _redo.Clear();
        _redoSidecar.Clear();
        LastRestoredSidecar = null;
    }

    private void PushUndo(byte[] bytes, byte[]? sidecar)
    {
        _redo.Clear();
        _redoSidecar.Clear();
        _undo.Add(bytes);
        _undoSidecar.Add(sidecar);
        if (_undo.Count > MaxDepth)
        {
            _undo.RemoveAt(0);
            _undoSidecar.RemoveAt(0);
        }
    }
}
