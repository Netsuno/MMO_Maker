# tryauthenticateasync

← [Server](README.md) · [Référence](../README.md)

Authentifie un compte (hash + rate-limit).

*Source : `AuthService.TryAuthenticateAsync`*

**Signature :** `tryauthenticateasync(username, password, rateLimitKey)`

**Entrées :**
- `username` (`string`) — identifiant compte
- `password` (`string`) — secret (jamais d’exemple réel)
- `rateLimitKey` (`string`) — clé de fenêtrage (souvent IP:port)

**Sorties :**
- `Success` (`bool`) — auth OK
- `Account` (`AccountRecord?`) — compte si OK
- `RateLimited` (`bool`) — `true` si rate-limit

**Refus :**
- rate-limit
- username/password invalides
- hash incorrect
