# soundservice-playuiclick

← [Client](README.md) · [Référence](../README.md)

Joue le SFX clic UI du MVP.

*Source : `SoundService.PlayUiClick`* · Tip : `d6e59759` · #28

**Signature :** `playuiclick()`

**Entrées :** —

**Sorties :**
- (`bool`) — `true` si le mixer a demandé la lecture

**Refus :**
- muet / volume 0 / backend indisponible → `false` (no-op)
