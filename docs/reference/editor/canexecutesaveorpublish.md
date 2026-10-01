# canexecutesaveorpublish

← [Editor](README.md) · [Référence](../README.md)

Indique si Enregistrer / Publier sont activables.

*Source : `MainForm.CanExecuteSaveOrPublish`*

**Signature :** `canexecutesaveorpublish()`

**Entrées :** —
*(état workspace / capabilities)*

**Sorties :**
- (`bool`) — menus Save/Publish enabled

**Exemple :**
```csharp
bool can = form.CanExecuteSaveOrPublish();
// Indique si Enregistrer / Publier sont activables
// (bool) — menus Save/Publish enabled
```
