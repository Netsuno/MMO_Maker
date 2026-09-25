# STATUS — Sélecteur de serveur (connexion)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Liste de serveurs, causes d'échec en français, réessai |
| **Propriétaire** | Netsun |
| **Statut** | MVP test privé — UX seulement |
| **Protocole** | `FrogWireProtocol.Version` **11** (Hello inchangé) |
| **Tuiles** | TileAsset **48×48** ; défaut monde 32 non modifié |

L'écran de connexion affiche les serveurs mémorisés. F9 ajoute une adresse (nom optionnel). « Connecter » ouvre le TCP. « Réessayer » relance la même adresse, ou renvoie le compte si le lien est déjà là.

Le bandeau sous la carte nomme la cause courte (identifiants, délai dépassé, version incompatible, serveur injoignable), l'hôte et le protocole. La phrase complète reste dans le statut. « Copier diagnostics » ajoute le dernier échec, toujours sans mot de passe ni jeton.

Hors scope : boutique, banque, combat, bump Hello, art DA ambre.
