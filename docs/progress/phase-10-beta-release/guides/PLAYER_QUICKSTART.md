# Guide rapide — Joueur testeur

> **Statut : BROUILLON P10-0 — Phase 10 pas prête. Ne pas diffuser comme procédure livrée.**

Ce guide s’adresse au **joueur testeur** : vous recevez seulement le **client** Windows. Vous n’avez jamais besoin (ni le droit) d’un secret de base de données.

## Ce que vous pourrez faire (cible bêta)

1. Installer le client sans SDK / Visual Studio / Git.
2. Vous connecter au serveur indiqué par l’opérateur (invitation / compte provisionné).
3. Créer ou choisir un personnage, entrer dans le monde.
4. Jouer le parcours de démonstration (environ 30–60 minutes une fois le monde démo livré).

<!-- CAPTURE: assets/player-01-install.png — écran d’install / dossier client ; à prendre quand paquet autonome existe (P10-6) -->
*Capture à venir : installation du client.*

## Ce qui est déjà vrai sur `main` (Phases 7–9)

Gameplay de base (cartes, combat, objets, quêtes, craft, boutique, banque, chat, mute/kick/ban) existe côté plateforme. Voir le journal : [`../../STATUS.md`](../../STATUS.md) et [`../BETA_SCOPE.md`](../BETA_SCOPE.md).

## Ce qui n’est **pas** prêt (ne pas attendre ça aujourd’hui)

D’après [`../KNOWN_ISSUES.md`](../KNOWN_ISSUES.md) :

| Attente joueur | État réel |
| --- | --- |
| Paquet client autonome (sans runtime .NET à installer soi-même) | **Non prouvé** (P10-6) |
| Connexion chiffrée TLS | **Absente** (TCP clair) — P10-5 |
| Inscription fermée par invitation | **Absente** (register ouvert) — P10-5 |
| Groupes / guildes / amis / blocage | **Absents** — P10-1 |
| Échanges entre joueurs | **Absents** — P10-2 |
| Aide intégrée / rebind / version visible | **Incomplets** — P10-3 |

<!-- CAPTURE: assets/player-02-login.png — écran connexion ; sans adresse IP réelle ni identifiants -->
*Capture à venir : connexion (expurger adresse / identifiants si besoin).*

## Procédure provisoire (quand l’opérateur vous envoie un build)

1. Dézipper le dossier client fourni.
2. Lancer l’exécutable indiqué dans le message de l’opérateur (nom exact à confirmer au packaging P10-6).
3. Entrer **uniquement** l’adresse / le port et le compte fournis.
4. Créer un personnage si demandé, puis suivre le parcours démo.

Si quelque chose bloque le parcours : utilisez [`BUG_REPORT_TEMPLATE.md`](BUG_REPORT_TEMPLATE.md). **N’incluez jamais** un mot de passe, un DSN ou un fichier `appsettings.Local.json`.

## Liens

- Périmètre : [`../BETA_SCOPE.md`](../BETA_SCOPE.md)
- Wiki : [Joueur](https://github.com/Netsuno/MMO_Maker/wiki/Joueur)
