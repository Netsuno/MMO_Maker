# characterlistbyaccountasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Liste les personnages d’un compte.

*Source : `PostgresCharacterRepository.ListByAccountAsync`*

**Signature :** `characterlistbyaccountasync(accountId)`

**Entrées :**
- `accountId` (`Guid`) — compte à lister

**Sorties :**
- (`IReadOnlyList<CharacterRecord>`) — liste (vide si aucun)

**Exemple :**
```csharp
var list = await characters.ListByAccountAsync(accountId, ct);
// Liste les personnages d’un compte
// (IReadOnlyList<CharacterRecord>) — liste (vide si aucun)
```
