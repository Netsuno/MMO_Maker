# Rate-limit auth (P10-5 lot B)

## Clés

| Seau | Forme | Seuil | Fenêtre |
| --- | --- | --- | --- |
| IP + username | `ipuser:<ip-normalisée>:<user-minuscule>` | **8** échecs | **60 s** |
| IP seule | `ip:<ip-normalisée>` | **30** échecs | **60 s** |

Normalisation IP (`AuthRateLimitKey`) :

- strip du port (`127.0.0.1:54321` → `127.0.0.1`, `[::1]:6000` → `::1`) ;
- dépliage IPv4-mapped (`::ffff:192.0.2.10` → `192.0.2.10`).

Login, **register** et reconnect **partagent** ces seaux. Un succès login/register/reconnect **vide le seau IP+user**, pas le seau IP.

Ce n’est **plus** `ClientSession.RemoteEndPoint` (IP:port). Changer de port source ne réinitialise pas la protection.

## NAT

Plusieurs joueurs derrière la même IPv4 publique **partagent** le seau IP (30 / 60 s). Un voisin qui se trompe de mot de passe ne doit pas, à lui seul, saturer 8/60 s d’un autre compte (seau IP+user distinct). Un brute-force distribué sur beaucoup de usernames depuis la même IP publique est borné à 30 échecs / 60 s — c’est voulu ; documenter aux ops qu’un hôtel / CGNAT dense peut heurter ce plafond. Relâcher le seau IP n’est pas un succès d’un seul compte (évite qu’un compte valide « lave » la fenêtre).

In-memory, redémarrage processus = fenêtres à zéro.

## Tests

`Phase10AuthRateLimitTests` : ports distincts, unmap v4, 8/60 et 30/60, register limité.
