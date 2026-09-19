# moderationexecuteasync

← [Server](README.md) · [Référence](../README.md)

Exécute mute / unmute / kick / ban / unban si l’acteur est opérateur.

*Source : `ModerationService.ExecuteAsync`*

**Signature :** `moderationexecuteasync(actorAccountId, action, targetUsername, reason)`

**Entrées :**
- `actorAccountId` (`Guid`) — compte opérateur
- `action` (`ModerationAction`) — type de sanction
- `targetUsername` (`string`) — cible
- `reason` (`string`) — motif (défaut wire si vide)

**Sorties :**
- `Success` (`bool`) — via `ModerationCommandResult`
- `Message` (`string`) — message joueur/ops

**Refus :**
- acteur non opérateur / non auth
- cible introuvable / username invalide
