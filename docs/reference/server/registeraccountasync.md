# registeraccountasync

← [Server](README.md) · [Référence](../README.md)

Crée un compte si les règles d’entrée passent.

*Source : `AuthService.RegisterAccountAsync`*

**Signature :** `registeraccountasync(username, password)`

**Entrées :**
- `username` (`string`) — nouvel identifiant
- `password` (`string`) — secret

**Sorties :**
- (`AccountCreateResult`) — statut création (OK / InvalidInput / …)

**Refus :**
- username/password hors règles

**Exemple :**
```csharp
var created = await auth.RegisterAccountAsync("Netsun", "••••••••");
if (created.Status != AccountCreateStatus.Created) return;
// Crée un compte si les règles d’entrée passent
```
