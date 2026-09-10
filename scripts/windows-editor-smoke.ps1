# Smoke manuel éditeur Windows (secours)

Utiliser si le runner GitHub Actions ne peut pas exécuter WPF (échec CI).

## Prérequis

- Windows 10/11, .NET 8 SDK
- Build Release de la solution

## Commandes

```powershell
cd <repo>
dotnet build Frog.Creator.sln -c Release
$env:FROG_EDITOR_FORCE_IN_MEMORY = "1"
dotnet test tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj -c Release --no-build -v n
```

## Checklist manuelle (si test automatisé impossible)

- [ ] `Frog.Editor` démarre sans exception
- [ ] Colonnes gauche / centre / droite visibles
- [ ] Arbre « Monde » contient « Carte démo »
- [ ] Canvas affiche une carte 20×15
- [ ] Barre d’état réactive (coordonnées tuile)
- [ ] Fermeture propre sans blocage

**Ne pas déclarer PASS** tant que le test automatisé ou cette checklist signée n’est pas vert.
