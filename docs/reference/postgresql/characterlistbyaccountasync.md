# characterlistbyaccountasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Liste les personnages d’un compte.

*Source : `PostgresCharacterRepository.ListByAccountAsync`*

**Signature :** `characterlistbyaccountasync(accountId)`

**Entrées :**
- `accountId` (`Guid`)

**Sorties :**
- (`IReadOnlyList<CharacterSummary>`) — liste (vide si aucun)
