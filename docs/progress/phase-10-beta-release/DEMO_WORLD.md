# Monde de démonstration P10-4

Fixture de **validation**, pas le monde final. Publication par les chemins éditeur
(`SaveAsync` / `Intent=Publish`) : `tools/Frog.DemoWorld` et
`scripts/publish-demo-world.sh`.

Catalogue : `Frog.Application/Demo/Phase10DemoWorldCatalog.cs`.
Licences : [`demo-world/LICENSES.md`](demo-world/LICENSES.md).

## Contenu

| Exigence | Livré |
| --- | --- |
| 3 cartes reliées | Village d'accueil → Faubourgs → Arène (warps) |
| 2 régions | Place du village (clair) ; Sous-bois des faubourgs (pluie, `SrcX` distinct) |
| 3 NPC | Guide, Marchand, Artisan |
| 2 monstres | Slime des prés, Loup de l'arène |
| 8 objets | potion, bandage, herbe, tissu, épée, tunique, pain, clé |
| 1 métier + 2 recettes | Herboriste ; bandage ; pain |
| 2 quêtes / 5 types | Premiers pas (Talk+Visit) ; L'épreuve (Collect+Kill+Craft) |
| Événement réutilisable + condition + dialogue à choix + récompense unique | CommonEvent accueil, switch `p10_demo_welcome`, choix de quête, coffre `onceKey` |

Réinstall base vierge : `Migrate()` + `publish` sur une base vide.

## Recette 12 étapes / 2 machines

**Automatisé :** fixture PG + loopback [`guides/BETA_TEST_PLAN.md`](guides/BETA_TEST_PLAN.md) (`Phase10RecipeLoopbackTests`).
**Physique (WAN / éditeur distant / stabilité) :** **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** — non rejouée dans cette PR.
`Frog.Client.exe --smoke-launch` reste un smoke Windows CI, distinct de l’étape 2 acceptée.

## Publier

```bash
export FROG_POSTGRES_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog;Username=frog;Password=…'
./scripts/publish-demo-world.sh
```
