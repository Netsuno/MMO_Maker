# composedtileseteditorpanel

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Panneau Données de jeu des tuiles composées 48×48.

*Source : `ComposedTilesetEditorPanel`*

**Signature :** `composedtileseteditorpanel(session, capabilities) → ComposedTilesetEditorPanel`

**Membres :**
- `IsDirty` (`bool`) — brouillon modifié
- `StatusChanged` (`event Action<string>?`) — texte de statut

**Entrées :**
- `session` (`ComposedTilesetWorkspaceSession`) — catalogue et brouillon
- `capabilities` (`ContentRepositoryCapabilities`) — écriture durable ou non

**Sorties :**
- `panel` (`ComposedTilesetEditorPanel`) — contrôle WinForms

**Exemple :**
```csharp
await panel.InitializeAsync();
// Panneau Données de jeu des tuiles composées 48×48.
// composedtileseteditorpanel(session, capabilities) → ComposedTilesetEditorPanel
```
