# Espace de travail éditeur — wireframe et responsabilités

Référence Phase 3 (PRD MMO Maker §17 Phase 3 / §18 tâches 4–5). Coque hybride WPF + WinForms (ADR-0004).

## Wireframe (desktop)

```text
┌─ Menu ──────────────────────────────────────────────────────────────┐
│ Fichier  Édition  Ressources  Carte  Affichage   [barre d’outils]   │
├──────────────┬───────────────────────────────┬──────────────────────┤
│ OUTILS       │ CARTE : Nom (W × H)           │ TILESETS             │
│ crayon/…     │ ┌───────────────────────────┐ │ palette / charge     │
│ types tuile  │ │                           │ ├──────────────────────┤
├──────────────┤ │      CANVAS (GDI+)         │ │ COUCHES              │
│ MONDE        │ │      + grille / zoom       │ │ visibilité / verrou  │
│ └ Cartes     │ │      + minimap             │ ├──────────────────────┤
│   ├ 001 …    │ │                           │ │ PROPRIÉTÉS           │
│   └ 002 …    │ └───────────────────────────┘ │ PropertyGrid carte    │
├──────────────┴───────────────────────────────┴──────────────────────┤
│ Statut : tuile x,y · zoom · hints                                   │
└─────────────────────────────────────────────────────────────────────┘
```

Colonnes redimensionnables (splitters WPF). Largeurs mémorisées localement (`editor-workstate.json`).

## Responsabilités des panneaux

| Zone | Contrôle | Responsabilité | Ne fait pas |
| --- | --- | --- | --- |
| Menu / outils | `MainWindow` + commandes | Actions globales (nouveau, ouvrir fichier, undo, zoom) | Accès DB direct |
| Outils gauche | `EditorLeftToolsWpf` | Outil actif, type de tuile attribut | Persistance |
| Arbre monde | `MapsProjectPanel` | Liste / sélection des cartes du catalogue | SQL, sérialisation |
| Canvas | `MapCanvas` | Rendu, caméra, édition tuiles (Phase 4+) | Repository |
| Tilesets | `TilesetPickerPanelWpf` | Sélection tileset / tampon | Publication |
| Couches | `LayersProjectPanel` | Visibilité, verrou, sélection couche | Sauvegarde PG |
| Propriétés | `PropertyGrid` | Métadonnées carte sélectionnée | Connexion DB |
| Statut | barre WPF | Coordonnées, zoom, hints | — |

## Couche application

- Ports : `IMapRepository` (`ListSummariesAsync`, `LoadByLegacyIdAsync`, `SaveAsync`).
- Session : `MapWorkspaceSession` orchestre catalogue + carte courante (pas d’UI).
- Composition éditeur : `EditorMapRepositoryFactory` choisit PostgreSQL (chaîne) ou mémoire (démo hors DB).
- Formulaires / code-behind : **aucun** `DbContext` / `Npgsql` / `MySqlConnection`.

## Carte démo

Au démarrage, la session ouvre une carte démo moderne (mémoire ou seed PG si catalogue vide). Gate Phase 3 : la carte est visible dans le shell ; smoke UI Windows documenté séparément.
