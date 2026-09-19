# Quickstart joueur

À la fin de ce guide, tu es connecté, tu as un personnage, et tu peux te déplacer sur une carte.

> **Bêta en cours** — smoke `--smoke-launch` Windows CI ; playtest zip = Hello serveur hors dépôt, pas une partie. TLS campaign P10-8 ≠ 2 PCs.

## Prérequis

- Client MMO Maker fourni pour la bêta
- Identifiants de test envoyés par l’opérateur
- Connexion Internet stable

## 1. Lancer le client

Sur Windows, dézipper `client-win-x64.zip` **hors** du dépôt git, puis double-cliquer `Frog.Client.exe` (runtime bundlé, pas de SDK). Windows peut afficher SmartScreen : binaire **non signé**.

Preuve CI de démarrage (pas une partie) :

```powershell
./scripts/packaged-winforms-smoke.ps1
# équivalent manuel après extraction hors repo :
#   Frog.Client.exe --smoke-launch
```

<!-- CAPTURE: assets/joueur-01-connexion.png -->
*Capture à venir : écran de connexion — saisis l’identifiant et le mot de passe fournis, puis utilise le bouton principal de connexion.*

## 2. Choisir ou créer un personnage

Sur l’écran personnages, sélectionne une case libre ou un personnage existant, puis confirme.

<!-- CAPTURE: assets/joueur-02-perso.png -->
*Capture à venir : liste des personnages — clique une case, confirme avec le bouton principal en bas.*

## 3. Premiers pas en jeu

Une fois en carte, déplace-toi avec les contrôles indiqués à l’écran (ou rappelés par l’opérateur).

<!-- CAPTURE: assets/joueur-03-hud.png -->
*Capture à venir : vue en jeu — personnage sur la carte ; chat en bas ; barres de statut si visibles.*

## Tu es prêt si…

- [ ] Tu vois ton personnage sur la carte
- [ ] Tu peux te déplacer
- [ ] Tu peux ouvrir le chat (même sans écrire)

## Et après ?

- [Problèmes connus Phase 10](../KNOWN_ISSUES.md)
- [Signaler un bug](BUG_REPORT_TEMPLATE.md)
- Wiki : [Joueur](https://github.com/Netsuno/MMO_Maker/wiki/Joueur)
