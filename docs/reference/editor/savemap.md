# savemap

← [Editor](README.md) · [Référence](../README.md)

Enregistre la carte courante en **brouillon** PostgreSQL.

*Source : `MainForm.SaveMap` → `SaveMapCoreAsync` / `SaveMapIntent.SaveDraft`*

**Signature :** `savemap()`

**Entrées :** —
*(carte courante du workspace)*

**Sorties :**
- opération async via `RunSaveOperationAsync`
- résultat `SaveMapResult` (Success / ValidationFailed / …)

**Refus :**
- `CanExecuteSaveOrPublish()` = false
