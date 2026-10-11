# mapobjectdefinition-validate

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Vérifie le nom, le chemin, l’empreinte et le SHA-256.

*Source : `MapObjectDefinition.Validate`*

**Signature :** `validate(out error) → bool`

**Entrées :**
- `error` (`out string?`) — motif si faux

**Sorties :**
- `ok` (`bool`) — vrai si publiable

**Refus :**
- PlacementId hors motif minuscules/tirets
- empreinte hors 1–32
- SHA-256 pas 64 hex

**Exemple :**
```csharp
if (!definition.Validate(out var error)) return;
// Vérifie le nom, le chemin, l’empreinte et le SHA-256.
// validate(out error) → bool
```
