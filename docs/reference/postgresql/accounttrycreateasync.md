# accounttrycreateasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Crée un compte (hash mot de passe géré en amont / dans le repo).

*Source : `PostgresAccountRepository.TryCreateAsync` (Auth)

**Signature :** `accounttrycreateasync(username, password)`

**Entrées :**
- `username` (`string`)
- `password` (`string`) — secret ; jamais loggé dans la doc

**Sorties :**
- (`AccountCreateResult`) — succès / conflit / invalide
