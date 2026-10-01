# accounttrycreateasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Crée un compte (hash mot de passe géré en amont / dans le repo).

*Source : `PostgresAccountRepository.TryCreateAsync` (Auth)*

**Signature :** `accounttrycreateasync(username, password)`

**Entrées :**
- `username` (`string`) — login à créer
- `password` (`string`) — secret ; jamais loggé dans la doc

**Sorties :**
- (`AccountCreateResult`) — succès / conflit / invalide

**Exemple :**
```csharp
var result = await accounts.TryCreateAsync("Netsun", password: "••••••••", ct);
if (result.Status != AccountCreateStatus.Created) return; // conflit / invalide
// Crée un compte (hash mot de passe géré en amont / dans le repo)
```
