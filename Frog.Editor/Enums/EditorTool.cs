namespace Frog.Editor.Enums
{
    public enum EditorTool
    {
        Brush = 0,
        Eraser = 1,
        Cursor = 2,
        /// <summary>Remplissage (même tuile / case vide connectés en 4-directions).</summary>
        Fill = 3,
        /// <summary>Rectangle plein entre deux cases (clic départ, clic fin).</summary>
        Rectangle = 4
    }
}
