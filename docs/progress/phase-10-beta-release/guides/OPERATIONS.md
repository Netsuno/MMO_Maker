# Operations — stack bêta

> **Brouillon P10-0** — référence ops pour opérateurs. Pas une diffusion joueur.

## Prérequis

- PostgreSQL 16
- Runtime / SDK .NET 8 selon le mode (from-source vs paquet)
- Overlay local **gitignoré** (jamais committer les secrets)

Référence détaillée Phase 9 (à suivre tant que Phase 10 n’étend pas) :

- [OPERATIONS_RUNBOOK](../../phase-09-distribution-admin-hardening/OPERATIONS_RUNBOOK.md)
- [BACKUP_RESTORE_RUNBOOK](../../phase-09-distribution-admin-hardening/BACKUP_RESTORE_RUNBOOK.md)
- [PACKAGING_GUIDE](../../phase-09-distribution-admin-hardening/PACKAGING_GUIDE.md)

## 1. Démarrer (from-source, dev)

1. Démarrer PostgreSQL (ex. Compose du dépôt).
2. Copier l’exemple d’overlay local → fichier **gitignoré** à côté de l’hôte.
3. Renseigner la connexion via le fichier local **ou** la variable d’environnement documentée dans le runbook (valeurs = hors git / hors wiki).
4. Lancer le serveur : `dotnet run --project Frog.Server/Frog.Server.csproj`

**Résultat attendu :** processus qui écoute ; logs sans secret en clair dans les tickets.

**Rollback :** arrêter le processus ; ne pas publier l’overlay.

## 2. Sauvegarde / restauration

Suivre le runbook Phase 9 (étapes numérotées + preuves).

**Résultat attendu :** login possible après restore.

**Incomplet Phase 10 :** restore avec lignes mute/ban (+ social/trade) pas encore couvert — voir [KNOWN_ISSUES](../KNOWN_ISSUES.md).

## 3. Ce qui est testable vs incomplet

| Sujet | Testable maintenant | Incomplet (Phase 10) |
| --- | --- | --- |
| Serveur Linux publié | Oui (preuve Phase 9) | — |
| Client / éditeur paquet autonome | Non | P10-6 |
| TLS + certificat validé | Non (TCP clair) | P10-5 |
| Inscriptions fermées | Non | P10-5 |
| Charge 25×60 | Non | P10-8 |

## Secrets

- Nommer les variables / fichiers (`appsettings.Local.json`, variable Postgres documentée).
- **Jamais** coller les valeurs dans ce guide, le wiki, ou un ticket public.

## Et après ?

- [KNOWN_ISSUES](../KNOWN_ISSUES.md)
- Wiki : [Ops](https://github.com/Netsuno/MMO_Maker/wiki/Ops)
