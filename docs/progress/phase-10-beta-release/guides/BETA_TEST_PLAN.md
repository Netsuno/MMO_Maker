# Plan de test bêta

> **Brouillon P10-0** — non exécuté. Pas une preuve de sortie.

## Prérequis campagne

| # | Prérequis | Statut |
| --- | --- | --- |
| 1 | Paquets client (2 PC) + serveur | À faire |
| 2 | Comptes / invitations | À faire |
| 3 | Transport chiffré validé | À faire |
| 4 | Monde démo publié | À faire |
| 5 | Canal bugs + [modèle](BUG_REPORT_TEMPLATE.md) | À faire |

## Scénarios (12)

| # | Rôle | Scénario | Étapes (résumé) | Attendu | Statut |
| --- | --- | --- | --- | --- | --- |
| 1 | Joueur A | Install client | Dézipper / lancer | App démarre | Non joué |
| 2 | Joueur B | Install client | Idem machine 2 | App démarre | Non joué |
| 3 | A+B | Connexion | Login comptes fournis | Session OK | Non joué |
| 4 | A+B | Personnages | Créer / choisir | En carte | Non joué |
| 5 | A+B | Présence | Même carte | Se voient | Non joué |
| 6 | A | Gameplay de base | Combat / objet / quête courte | Pas de blocage P0 | Non joué |
| 7 | A+B | Social minimal | Selon build | Action OK ou « pas dans ce build » | Non joué |
| 8 | A+B | Échange | Selon build | Pas de dup / perte | Non joué |
| 9 | Auteur | Publish mineur | Éditeur → monde | Visible in-game | Non joué |
| 10 | Ops | Backup / restore | Runbook | Login après restore | Non joué |
| 11 | Ops | Sanction | Mute ou kick | Effet visible | Non joué |
| 12 | A+B | Stabilité courte | Session continue | Inventaire cohérent | Non joué |

Cocher **Statut** seulement avec preuve (capture / note datée). Ne pas ajouter de lignes pour des fonctions hors build.
