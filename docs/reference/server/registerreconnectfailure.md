# registerreconnectfailure

← [Server](README.md) · [Référence](../README.md)

Enregistre un échec de reconnexion pour le rate-limit.

*Source : `AuthService.RegisterReconnectFailure`*

**Signature :** `registerreconnectfailure(remoteEndPoint)`

**Entrées :**
- `remoteEndPoint` (`string`) — extrémité distante (port ignoré par le seau)
- `username` (`string?`, optionnel) — compte, si déjà connu

**Sorties :** —

**Exemple :**
```csharp
auth.RegisterReconnectFailure(remoteEndPoint: "203.0.113.10:443");
// Enregistre un échec de reconnexion pour le rate-limit
// username optionnel ; le port n’entre pas dans le seau
```
