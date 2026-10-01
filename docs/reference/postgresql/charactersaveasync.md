# charactersaveasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Persiste l’état d’un personnage.

*Source : `PostgresCharacterRepository.SaveAsync`*

**Signature :** `charactersaveasync(character)`

**Entrées :**
- `character` (`CharacterRecord`) — état à écrire

**Sorties :**
- (`Task`) — complète si écriture OK

**Refus :**
- personnage introuvable (comportement runtime / exception selon implémentation)

**Exemple :**
```csharp
await characters.SaveAsync(character, ct);
// Persiste l’état d’un personnage
// (Task) — complète si écriture OK
```
