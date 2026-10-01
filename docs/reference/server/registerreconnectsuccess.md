# registerreconnectsuccess

← [Server](README.md) · [Référence](../README.md)

Enregistre un succès de reconnexion (reset partiel rate-limit).

*Source : `AuthService.RegisterReconnectSuccess`*

**Signature :** `registerreconnectsuccess(remoteEndPoint)`

**Entrées :**
- `remoteEndPoint` (`string`) — extrémité distante (port ignoré par le seau)
- `username` (`string?`, optionnel) — compte, si déjà connu

**Sorties :** —

**Exemple :**
```csharp
auth.RegisterReconnectSuccess(remoteEndPoint: "203.0.113.10:443");
// Enregistre un succès de reconnexion (reset partiel rate-limit)
// username optionnel ; le port n’entre pas dans le seau
```
