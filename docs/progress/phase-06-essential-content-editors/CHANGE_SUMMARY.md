# Phase 6 — CHANGE SUMMARY

## Game Data editors

Added structured « Données de jeu » shell (RPG Maker–inspired workflow, original UI) with:

1. Tilesets  
2. NPCs / monsters  
3. Items  
4. Spells / skills  
5. Classes  
6. Shops  
7. Resources / spawns  

Each slice: domain validation, Guid identity, PostgreSQL draft/publish, Application ports, editor list+form, published server consumer, tests.

## Migrations (content schema)

- `TilesetDraftPublish`
- `NpcDraftPublish` (name may vary — see Migrations folder)
- `ItemDraftPublish`
- `SpellDraftPublish`
- `ClassDraftPublish`
- `ShopDraftPublish`
- `ResourceDraftPublish`

## Architecture

- UI never opens DbContext; composition via Editor*RepositoryFactory
- PostgreSQL sole SoT for content (ADR-0002)
- No new MariaDB features
