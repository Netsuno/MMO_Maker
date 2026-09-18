# Plan de test bêta

> **P10-4** — recette **non exécutée** de bout en bout (il manque deux PCs Windows + 30–60 min humaines). Ce fichier nomme ce qui est **automatisable** vs ce qui **exige 2 machines physiques**. Pas une preuve de sortie. Pas READY.

## Prérequis campagne

| # | Prérequis | Statut |
| --- | --- | --- |
| 1 | Paquets client (2 PC) + serveur | Layout + SHA-256 **CI** ; lancement EXE **Windows CI `--smoke-launch`** ; 2 PCs distants **non** |
| 2 | Comptes / invitations | OpsCli + `ProvisionedOnly` livrés ; pas de campagne humaine |
| 3 | Transport chiffré validé | TLS unitaires PASS ; pas de session 2 joueurs TLS |
| 4 | Monde démo publié | Fixture PG **oui** ; pas joué 30–60 min |
| 5 | Canal bugs + [modèle](BUG_REPORT_TEMPLATE.md) | Modèle présent ; canal humain **non** ouvert |

## Matrice 12 étapes — automate vs 2 machines

| # | Rôle | Scénario | Automatisable (CI / 1 hôte) | Exige 2 machines physiques | Statut |
| --- | --- | --- | --- | --- | --- |
| 1 | Joueur A | Install client | Zip + `Frog.Client.exe --smoke-launch` (`packaged-winforms-smoke.ps1`, 1 Windows) | Non pour le smoke ; **oui** pour un testeur A réel | Smoke CI Windows ; install humaine **non jouée** |
| 2 | Joueur B | Install client machine 2 | Non (deuxième OS / deuxième écran) | **Oui** — second PC Windows hors agent | Non joué |
| 3 | A+B | Connexion | TCP login 2 clients loopback (suites PG) | **Oui** si NAT / IP publique / TLS réel | Loopback **oui** ; 2 WAN **non** |
| 4 | A+B | Personnages | TCP create/select | Idem | Loopback **oui** |
| 5 | A+B | Présence même carte | TCP 2 sessions même process serveur | **Oui** pour latence / NAT / firewall | Loopback **oui** |
| 6 | A | Gameplay de base | Gameplay / Phase 8 smokes from-source | **Oui** pour HUD 1366/1920 + DPI | Smokes internes ; recette paquet **non** |
| 7 | A+B | Social | `Phase10SocialTcpTests` / PG | **Oui** pour chat réel 2 PCs | Automatisé loopback |
| 8 | A+B | Échange | `Phase10TradeTcpTests` / PG | **Oui** pour UI `TradeForm` à deux souris | Automatisé loopback |
| 9 | Auteur | Publish mineur | `Frog.DemoWorld` + publisher PG | **Oui** si éditeur **paquet** → joueur distant voit le warp | Fixture **oui** ; paquet+2 PCs **non** |
| 10 | Ops | Backup / restore | `Phase10BackupRestoreRowsTests` (lignes sociales/trade/sanctions + serveur publié) | Non pour la preuve CI | **CI** |
| 11 | Ops | Sanction | mute/ban TCP + restore banni rejeté | **Oui** pour effet visible HUD distant | Automatisé loopback |
| 12 | A+B | Stabilité 30–60 min | Non (durée humaine / 2 clients idle) | **Oui** | Non joué |

**Deux machines physiques sont obligatoires pour les étapes 2, 5 (WAN), 9 (éditeur livré → joueur distant), 12.** Un agent Linux unique ne peut pas les cocher.

## Scénarios (12) — journal campagne

Cocher **Statut** seulement avec preuve (capture / note datée / SHA CI). Ne pas ajouter de lignes hors build.

| # | Rôle | Scénario | Étapes (résumé) | Attendu | Statut |
| --- | --- | --- | --- | --- | --- |
| 1 | Joueur A | Install client | Dézipper / lancer | App démarre | Smoke `--smoke-launch` CI Windows ; campagne 2 PC **non jouée** |
| 2 | Joueur B | Install client | Idem machine 2 | App démarre | Non joué (2e machine) |
| 3 | A+B | Connexion | Login comptes fournis | Session OK | Loopback tests ; WAN **non** |
| 4 | A+B | Personnages | Créer / choisir | En carte | Loopback tests |
| 5 | A+B | Présence | Même carte | Se voient | Loopback tests ; 2 PCs **non** |
| 6 | A | Gameplay de base | Combat / objet / quête courte | Pas de blocage P0 | Smokes from-source |
| 7 | A+B | Social minimal | Groupe / guilde / ami | Action OK | Tests TCP/PG |
| 8 | A+B | Échange | Invite + commit | Pas de dup / perte | Tests TCP/PG |
| 9 | Auteur | Publish mineur | Éditeur paquet → monde | Visible in-game | Fixture démo ; playtest paquet **chemins livrés** (P10-3), recette humaine **non** |
| 10 | Ops | Backup / restore | Runbook + P10-7 | Login après restore ; banni refusé | **CI** `Phase10BackupRestoreRowsTests` |
| 11 | Ops | Sanction | Mute ou kick / ban | Effet visible | Tests + restore |
| 12 | A+B | Stabilité courte | Session 30–60 min | Inventaire cohérent | Non joué (2 machines) |
