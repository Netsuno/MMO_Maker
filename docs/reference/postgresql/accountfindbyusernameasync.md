# accountfindbyusernameasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Cherche un compte par nom d’utilisateur.

*Source : `PostgresAccountRepository.FindByUsernameAsync` (Auth)

**Signature :** `accountfindbyusernameasync(username)`

**Entrées :**
- `username` (`string`) — login

**Sorties :**
- (`AccountRecord?`) — compte ou null
