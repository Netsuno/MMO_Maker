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
        Rectangle = 4,
        /// <summary>Trait d'une tuile de large (Bresenham) entre deux cases. Majuscule : axe horizontal ou vertical.</summary>
        Line = 5,
        /// <summary>Sélection rectangulaire (copier / coller tuiles sur la couche active).</summary>
        Selection = 6,
        /// <summary>Clic = tuile de spawn playtest / départ (mémo locale, pas de peinture).</summary>
        Spawn = 7,
        /// <summary>Clic = poser un prefab (sidecar / workstate, pas de peinture tuile).</summary>
        Prefab = 8
    }
}
