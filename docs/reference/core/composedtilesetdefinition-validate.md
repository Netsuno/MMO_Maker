# composedtilesetdefinition-validate

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Vérifie le nom, le chemin et des tuiles 48×48 dont l’id colle aux pixels.

*Source : `ComposedTilesetDefinition.Validate`*

**Signature :** `validate(out error) → bool`

**Entrées :**
- `error` (`out string?`) — motif si faux

**Sorties :**
- `ok` (`bool`) — vrai si publiable

**Refus :**
- aucune tuile
- pixels autres que 48×48
- TileAssetId incohérent
- chemin avec .. ou absolu

**Exemple :**
```csharp
if (!definition.Validate(out var error)) return;
// Vérifie le nom, le chemin et des tuiles 48×48 dont l’id colle aux pixels.
// validate(out error) → bool
```
