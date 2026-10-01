# soundservice-playuiclick

← [Client](README.md) · [Référence](../README.md)

Joue le SFX clic UI du MVP.

*Source : `SoundService.PlayUiClick`* · Tip : `a6edd821` · #28

**Signature :** `playuiclick()`

**Entrées :** —

**Sorties :**
- (`bool`) — `true` si le mixer a demandé la lecture

**Refus :**
- muet / volume 0 / backend indisponible → `false` (no-op)

**Exemple :**
```csharp
if (!sound.PlayUiClick())
    return; // muet / volume 0 / backend indisponible
// Joue le SFX clic UI du MVP
```
