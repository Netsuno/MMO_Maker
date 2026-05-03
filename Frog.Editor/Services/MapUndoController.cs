using System.Collections.Generic;
using Frog.Core.IO;
using Frog.Core.Models;

namespace Frog.Editor.Services;

/// <summary>Undo / redo par instantanés sérialisés (.fmap), profondeur limitée.</summary>
public sealed class MapUndoController
{
    private const int MaxDepth = 40;
    private readonly MapSerializer _serializer = new();
    private readonly List<byte[]> _undo = new();
    private readonly List<byte[]> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Enregistre l’état courant avant une modification ; vide la pile redo.</summary>
    public void PushBeforeChange(Map map)
    {
        _redo.Clear();
        _undo.Add(_serializer.Serialize(map));
        if (_undo.Count > MaxDepth)
        {
            _undo.RemoveAt(0);
        }
    }

    public Map? TryUndo(Map current)
    {
        if (_undo.Count == 0)
        {
            return null;
        }

        _redo.Add(_serializer.Serialize(current));
        var bytes = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        return _serializer.Deserialize(bytes);
    }

    public Map? TryRedo(Map current)
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        _undo.Add(_serializer.Serialize(current));
        var bytes = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        return _serializer.Deserialize(bytes);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
