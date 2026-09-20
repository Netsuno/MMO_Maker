# mapviewportcamera-dampfocus

← [Core](README.md) · Tip : `d6e59759` · #29

Suivi caméra exponentiel — évite qu’un gros pas predict yank le bitmap.

*Source : `MapViewportCamera.DampFocus`*

**Signature :** `dampfocus(currentX, currentY, targetX, targetY, dtSeconds, convergencePerSec?, snapEps?)`

**Entrées :**
- focus courant / cible (px monde)
- `dtSeconds` — passé dans `ClampVisualDt`
- défauts : convergence **16**/s, snap eps camera

**Sorties :**
- `(FocusX, FocusY)` interpolé via `MovementFluidity.StepToward` (maxStep ∞)

**Notes :** first paint / warp / map load = passer `current = target` pour snap. Voir aussi `ComputeDrawOffset` (offset viewport).
