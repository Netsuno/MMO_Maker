# Frog.Legacy — expérimental / différé

**Statut :** `experimental/deferred` (ADR-0003).

Ce projet contient un lecteur expérimental de cartes FRoG `.fcc`.  
Il **ne fait pas** partie du chemin critique du MMO Maker.

## Règles

- Ne pas référencer depuis `Frog.Editor`, `Frog.Client` ou `Frog.Server`.
- Ne pas ajouter d’importeur CLI ni de nouvelles fonctions d’import sans demande produit explicite.
- Les tests Legacy dans `Frog.Tests` peuvent rester pour non-régression du code existant.
- Suppression éventuelle uniquement si le projet gêne build/architecture.
