# Quickstart joueur

À la fin de ce guide, tu es connecté, tu as un personnage, et tu peux te déplacer, chatter, et ouvrir Amis / Groupe / Guilde.

> **Bêta en cours** — certaines étapes (paquet autonome, connexion chiffrée, invitation fermée) ne sont pas encore dans tous les builds. Suis le message de l’opérateur pour *ton* build.

Tip miroir docs : `d6e59759` · Social HUD #27 mergé.

## Prérequis

- Client MMO Maker fourni pour la bêta (Windows 11 x64, protocole **v11**)
- Identifiants de test envoyés par l’opérateur
- Connexion Internet stable

## 1. Lancer le client

Ouvre le client et attends l’écran de connexion.

<!-- CAPTURE: assets/joueur-01-connexion.png -->
*Capture à venir : écran de connexion — saisis l’identifiant et le mot de passe fournis, puis utilise le bouton principal de connexion.*

## 2. Choisir ou créer un personnage

Sur l’écran personnages, sélectionne une case libre ou un personnage existant, puis confirme.

<!-- CAPTURE: assets/joueur-02-perso.png -->
*Capture à venir : liste des personnages — clique une case, confirme avec le bouton principal en bas.*

## 3. Premiers pas en jeu

Une fois en carte, déplace-toi avec les contrôles indiqués à l’écran (ou rappelés par l’opérateur). **F1** = Aide.

<!-- CAPTURE: assets/joueur-03-hud.png -->
*Capture à venir : vue en jeu — personnage sur la carte ; chat en bas ; barres de statut si visibles.*


## 4. Social — Amis / Groupe / Guilde (HUD)

Le social ne passe **plus seulement** par les slash du chat.

1. Dans le **dock chat** (bas), clique **Amis**, **Groupe** ou **Guilde** (boutons contraste à côté des canaux).
2. L’overlay s’ouvre sur l’onglet **Social** avec le sous-onglet choisi.
3. Saisis un **Guid** personnage dans le champ bas (`Guid personnage / nom guilde / MOTD`) pour inviter / ajouter.
4. Utilise les boutons de l’onglet (ex. **Ajouter**, **Inviter**, **Accepter**, **Quitter**, **MOTD**…).

Détail chaque bouton : [UI-CLIENT-SocialHub.md](UI-CLIENT-SocialHub.md).

<!-- CAPTURE: assets/joueur-04-social-hud.png -->
*Capture à venir : dock chat avec Amis/Groupe/Guilde + overlay Social ouvert sur un des trois onglets.*

### Slash (secondaire)

Toujours possibles dans le chat : `/friend`, `/party`, `/guild`, `/block`. `/trade` ouvre l’échange P2P (fenêtre dédiée) — hors focus de ce quickstart social HUD.

Un redémarrage serveur **dissout les groupes**. Guildes / amis / blocages survivent.


## Tu es prêt si…

- [ ] Tu vois ton personnage sur la carte
- [ ] Tu peux te déplacer
- [ ] Tu peux ouvrir le chat (même sans écrire)
- [ ] Tu ouvres **Amis** ou **Groupe** ou **Guilde** depuis le dock chat

## Et après ?

- [Problèmes connus Phase 10](../KNOWN_ISSUES.md)
- [Signaler un bug](BUG_REPORT_TEMPLATE.md)
- Wiki : [Joueur](https://github.com/Netsuno/MMO_Maker/wiki/Joueur)
