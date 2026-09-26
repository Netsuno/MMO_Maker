# STATUS — Régions de carte et rencontres

| Champ | Valeur |
| --- | --- |
| **Chantier** | Mode Région (numéros 1–63) et table de rencontres par carte |
| **Propriétaire** | Netsun |
| **Statut** | MVP éditeur + lecture sidecar |
| **Tuiles** | 48×48 `TileAsset` — pas de retour à 32 |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — `.fmap` **reste v5 / v6** |

Les régions et la table de rencontres vivent dans le sidecar `{carte}.regions.json`, à côté du `.fmap`. Elles ne sont pas dans le blob carte ni dans Hello. Aucun asset ni rvdata VX.

## Livré

1. **Modèle** — cases de région 0–63 (0 = aucune), pas moyens, rencontres (monstre, alias, poids, régions). Liste vide = toute la carte.
2. **Éditeur** — outil Région (G), numéro affiché sur la carte, panneau français Régions / Rencontres. Le catalogue des monstres publiés remplit la liste ; s’il est vide, un nom ou un identifiant suffit.
3. **Fichier** — enregistrement et relecture du sidecar sans changer le `.fmap`. Un redimensionnement ou un décalage recadre les cases. Annuler une peinture de tuile conserve la couche région. Le serveur relit le sidecar au chargement fichier (pas les blobs PostgreSQL).

## Hors scope

Ticks de rencontre en jeu (pas moyen, choix de troupe, apparition). Le document est lisible ; le combat ne s’en sert pas encore.
