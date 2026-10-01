# tryauthenticateasync

← [Server](README.md) · [Référence](../README.md)

Authentifie un compte (hash + rate-limit).

*Source : `AuthService.TryAuthenticateAsync`*

**Signature :** `tryauthenticateasync(username, password, remoteEndPoint)`

**Entrées :**
- `username` (`string`) — identifiant compte
- `password` (`string`) — secret (jamais d’exemple réel)
- `remoteEndPoint` (`string`) — extrémité distante ; le seau ne garde que l’IP

**Sorties :**
- `Success` (`bool`) — auth OK
- `Account` (`AccountRecord?`) — compte si OK
- `RateLimited` (`bool`) — `true` si rate-limit

**Refus :**
- rate-limit
- username/password invalides
- hash incorrect

**Exemple :**
```csharp
var (ok, account, rateLimited) = await auth.TryAuthenticateAsync(
    "Netsun", "••••••••", remoteEndPoint: "203.0.113.10:443");
if (rateLimited || !ok) return;
```
