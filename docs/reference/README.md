# Référence / Fonctions

Catalogue court des actions du client, de l’éditeur et des ops.
Chaque fiche répond : **qu’est-ce que j’entre → qu’est-ce que j’obtiens**.

Wiki miroir : [Référence](https://github.com/Netsuno/MMO_Maker/wiki/R%C3%A9f%C3%A9rence).

## Comment lire une fiche

| Champ | Signification |
| --- | --- |
| Entrées | Données, clic UI, ou prérequis avant l’action |
| Sorties | Résultat visible / état / message |
| Public | Joueur · Auteur · Ops · Dev |
| Statut | Disponible · En cours Phase 10 · Non livré |

> **Bêta** — si le libellé UI change, la fiche suit l’UI. Pas de « READY » sans preuve.

## Par public

| Public | Page |
| --- | --- |
| Joueur | [actions-joueur.md](actions-joueur.md) |
| Auteur | [editeur.md](editeur.md) |
| Ops | [ops-cli.md](ops-cli.md) |
| Dev (protocole v10) | [protocole.md](protocole.md) |
| Dev (social/trade v11) | [protocole-social-trade.md](protocole-social-trade.md) |

## Toutes les fiches (aperçu)

| Nom | Public | Entrées (1 ligne) | Sorties (1 ligne) | Statut |
| --- | --- | --- | --- | --- |
| Connexion compte | Joueur/Dev | identifiant + secret (jamais d’exemple réel) | session ou refus | Disponible |
| Personnages | Joueur | liste / choix / création | perso actif | Disponible |
| Déplacement | Joueur | sync position | PositionUpdate | Disponible |
| Interaction tuile | Joueur | activationId | effets événement | Disponible |
| Chat | Joueur | canal 0–2 + texte | message | Disponible (Party/Guild = Non livré) |
| Banque / boutique | Joueur | dépôt/retrait/achat | inventaire/or | Disponible |
| Quête / craft / dialogue | Joueur | requestId / choix | journal / loot | Disponible |
| Modération | Ops | cible + action | mute/kick/ban | Disponible |
| Social 80–83 | Joueur | kind/action | snapshot/event | Non livré (P10-1) |
| Trade 84–86 | Joueur | trade_id | snapshot offre | Non livré (P10-2) |
| Publish éditeur | Auteur | contenu + overlay local | monde publié | Partiel (paquet non prouvé) |
| publish-frog | Ops | --target | arbres publish | Scripts oui |

Modèle : [_fiche-modele.md](_fiche-modele.md). Enrichir **au fil de l’eau**.
