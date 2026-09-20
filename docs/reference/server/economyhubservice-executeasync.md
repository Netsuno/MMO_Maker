# economyhubservice-executeasync

← [Server](README.md) · Tip : `d6e59759` · #32 scaffolding Query-only

Exécute une demande EconomyHub in-memory. **Pas de PostgreSQL.**

*Source : `EconomyHubService.ExecuteAsync`*

**Signature :** `executeasync(session, kind, action, requestId, extra, cancellationToken)`

**Entrées :**
- `session` — personnage actif requis
- `kind` / `action` — Query seulement reconnu
- `requestId`, `extra` (ignoré MVP)

**Sorties :**
- `(Result, Snapshot?)` — succès + message MVP ; snapshot liste vide (coffre : 8 slots vides si membre guilde via `ISocialStore`)

**Refus :**
- pas de personnage / kind ou action inconnus

**Honnêteté :** messages du type « Hotel des ventes : aucune enchere (MVP). » — pas de buy/send/deposit.
