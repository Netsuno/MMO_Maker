# characterlistbyaccountasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Liste les personnages d’un compte.

*Source : `PostgresCharacterRepository.ListByAccountAsync`*

**Signature :** `characterlistbyaccountasync(accountId)`

**Entrées :**
- `accountId` (`Guid`) — compte à lister

**Sorties :**
- (`IReadOnlyList<CharacterSummary>`) — liste (vide si aucun)
