# mapviewportcamera-dampfocus

← [Core](README.md) · Tip : `a6edd821` · #29

Suivi caméra exponentiel — évite qu’un gros pas predict yank le bitmap.

*Source : `MapViewportCamera.DampFocus`*

**Signature :** `dampfocus(currentX, currentY, targetX, targetY, dtSeconds, convergencePerSec?, snapEps?)`

**Entrées :**
- focus courant / cible (px monde)
- `dtSeconds` — passé dans `ClampVisualDt`
- défauts : convergence **16**/s, snap eps camera

**Sorties :**
- `(FocusX, FocusY)` interpolé via `MovementFluidity.StepToward` (maxStep ∞)

**Exemple :**
```csharp
var (fx, fy) = MapViewportCamera.DampFocus(
    currentX, currentY, targetX, targetY, dtSeconds);
// Suivi caméra exponentiel — évite qu’un gros pas predict yank le bitmap
```

**Notes :** first paint / warp / map load = passer `current = target` pour snap. Voir aussi `ComputeDrawOffset` (offset viewport).
