namespace Frog.Editor.Enums
{
    public enum EditorTool
    {
        Brush = 0,
        /// <summary>Efface le tampon sur la couche active visible et déverrouillée. Les autres couches restent.</summary>
        Eraser = 1,
        Cursor = 2,
        /// <summary>Remplissage 4-connexe (pot). Clic droit ou sélection vide : efface la région.</summary>
        Fill = 3,
        /// <summary>Rectangle ou ellipse entre deux cases. Défaut : rectangle plein. Maj ou case Contour : bord seulement.</summary>
        Rectangle = 4,
        /// <summary>Trait d'une tuile de large (Bresenham) entre deux cases. Majuscule : axe horizontal ou vertical.</summary>
        Line = 5,
        /// <summary>Sélection rectangulaire (copier / coller tuiles sur la couche active).</summary>
        Selection = 6,
        /// <summary>Clic = tuile de spawn playtest / départ (mémo locale, pas de peinture).</summary>
        Spawn = 7,
        /// <summary>Clic = poser un prefab (sidecar / workstate, pas de peinture tuile).</summary>
        Prefab = 8,
        /// <summary>Clic = poser une apparition, un PNJ ou un objet (mémo locale, pas de SQL ni de peinture).</summary>
        Place = 9
    }
}
