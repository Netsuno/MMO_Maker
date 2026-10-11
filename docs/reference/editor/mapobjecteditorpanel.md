# mapobjecteditorpanel

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Panneau Données de jeu des objets de carte.

*Source : `MapObjectEditorPanel`*

**Signature :** `mapobjecteditorpanel(session, capabilities) → MapObjectEditorPanel`

**Membres :**
- `IsDirty` (`bool`) — brouillon modifié
- `StatusChanged` (`event Action<string>?`) — texte de statut

**Entrées :**
- `session` (`MapObjectWorkspaceSession`) — catalogue et brouillon
- `capabilities` (`ContentRepositoryCapabilities`) — écriture durable ou non

**Sorties :**
- `panel` (`MapObjectEditorPanel`) — contrôle WinForms

**Exemple :**
```csharp
await panel.InitializeAsync();
bool dirty = panel.IsDirty;
// Panneau Données de jeu des objets de carte.
// mapobjecteditorpanel(session, capabilities) → MapObjectEditorPanel
```
