# Plan de test bêta (externe)

> **Statut : BROUILLON P10-0 — Phase 10 pas prête. Ce plan n’a pas été exécuté. Pas une preuve de gate.**

Objectif mandat : **12 étapes**, **2 joueurs**, **2 machines**, paquets livrés — pas une boucle locale seule.

## Prérequis (quand la candidate existera)

- [ ] Paquets client (2 machines Windows) + serveur Linux + PG 16
- [ ] Comptes provisionnés / invitations
- [ ] TLS validé sur le parcours externe
- [ ] Monde démo publié
- [ ] Canal de bugs + [`BUG_REPORT_TEMPLATE.md`](BUG_REPORT_TEMPLATE.md)

## Esquisse des 12 étapes

| # | Étape | Attendu | Preuve |
| --- | --- | --- | --- |
| 1 | Installer client machine A | Lance sans SDK | Capture + version |
| 2 | Installer client machine B | Idem | Capture |
| 3 | Connexion TLS au serveur | Certificat accepté ; pas de repli clair silencieux | Log expurgé |
| 4 | Création / login perso A et B | Deux persos distincts | — |
| 5 | Même carte, se voir | Présence réseau | Capture |
| 6 | Parcours combat / objets / quête | Pas de P0 | Notes |
| 7 | Social minimal (groupe ou ami) | Selon gel P10-1 | — |
| 8 | Échange P2P | Transaction unique, pas de dup | — |
| 9 | Éditeur : publish mineur | Contenu visible in-game | Capture |
| 10 | Backup + restore | Login OK après restore | Rapport |
| 11 | Sanction (mute/kick) | Effet + restore sanctions (P10-7) | — |
| 12 | Stabilité courte | Pas de perte inventaire | Notes |

**Exécution :** non commencée (P10-4 / P10-9). Cocher seulement après preuves réelles.

## Hors plan

HdV, mail objets, mobile, import VB6, boutique réelle, updater auto.
