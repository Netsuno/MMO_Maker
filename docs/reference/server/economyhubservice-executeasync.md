# economyhubservice-executeasync

← [Server](README.md) · Tip : `a6edd821` · #32 scaffolding Query-only

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

**Exemple :**
```csharp
var (result, snapshot) = await economyHub.ExecuteAsync(
    session, EconomyHubKind.Mail, action: 1 /* Query */, requestId, extra, ct);
if (!result.Success) return; // kind/action inconnu ou pas de perso
```

**Honnêteté :** messages du type « Hotel des ventes : aucune enchere (MVP). » — pas de buy/send/deposit.
