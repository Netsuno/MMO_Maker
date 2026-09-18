# Référence — Ops / CLI

## `scripts/publish-frog.sh` / `publish-frog.ps1`

| | |
| --- | --- |
| **Rôle** | Produire les layouts publish (server/client/editor) |
| **Entrées** | `--target` (`server-linux-x64`, `client-win-x64`, …) ; SDK 8.0.424 |
| **Sorties** | Arbres sous `artifacts/publish/…` (gitignoré) |
| **Erreurs** | SDK manquant ; échec `dotnet publish` |
| **Guide** | [PACKAGING_GUIDE](../progress/phase-09-distribution-admin-hardening/PACKAGING_GUIDE.md) |
| **État** | Scripts **oui** ; client/éditeur autonomes **non prouvés** |

## `scripts/run-packaged-server.sh`

| | |
| --- | --- |
| **Rôle** | Lancer l’hôte serveur depuis un dossier publish |
| **Entrées** | Répertoire publish + overlay local |
| **Sorties** | Processus serveur |
| **Erreurs** | Overlay / PG / monde publié manquant |
| **État** | Serveur Linux prouvé (tests packaged) |

## Overlay config

| | |
| --- | --- |
| **Rôle** | Activer PostgreSQL et le bind sans commit de secrets |
| **Entrées** | Fichier local gitignoré **ou** variable d’env documentée dans le runbook |
| **Sorties** | Démarrage accepté / refusé (placeholders sur bind public) |
| **Guide** | [OPERATIONS_RUNBOOK](../progress/phase-09-distribution-admin-hardening/OPERATIONS_RUNBOOK.md) |
| **État** | Livré Phase 9 |

## Backup / restore

| | |
| --- | --- |
| **Rôle** | Dump / restore PG + vérif login |
| **Entrées** | Outils PG + base cible |
| **Sorties** | Base restaurée |
| **Erreurs** | Dump incomplet |
| **État** | Partiel — sanctions / social / trade **pas** encore dans la campagne restore (P10-7) |

## À remplir (P10)

- Outil reset mot de passe / invitations / grant GM
- Job CI charge 25×60
- TLS opérable
