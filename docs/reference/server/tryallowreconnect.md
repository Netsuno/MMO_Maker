# tryallowreconnect

← [Server](README.md) · [Référence](../README.md)

Autorise une tentative de reconnexion sous rate-limit.

*Source : `AuthService.TryAllowReconnect`*

**Signature :** `tryallowreconnect(rateLimitKey)`

**Entrées :**
- `rateLimitKey` (`string`) — clé (préfixe reconnect appliqué en interne)

**Sorties :**
- (`bool`) — `true` si tentative autorisée
