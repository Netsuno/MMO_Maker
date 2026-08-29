# Phase 8 — CHANGE_SUMMARY

## P8-2 — Runtime complet

- `MapEventCommandExecutor` : toutes les commandes du COMMAND_CATALOG (show_text, branch, variables, items, gold, teleport, wait, dialogue, quêtes, call_common_event)
- Toutes les conditions (switch, variable, quête, item, niveau, métier, région)
- Variables perso PG (`character_world_variables`) + port `ICharacterWorldStateRepository` étendu
- Step-on (`player_contact`) branché sur l'interpréteur via `TryExecuteStepOnAsync`
- Effets secondaires PacketDispatcher : inventaire, or, téléportation

## P8-3 — Dialogues et quêtes

- Modèles Core : `DialogueDefinition`, `QuestDefinition`, `CharacterQuestProgress`
- Services : `DialogGameplayService`, `QuestGameplayService`
- PG : `character_quest_progress`

## P8-4 — Métiers et recettes

- Modèles : `ProfessionDefinition`, `RecipeDefinition`
- `CraftGameplayService` + `InMemoryEventCraftRepository` (idempotent par requestId)

## P8-5 — Régions et météo

- Modèles : `RegionDefinition`, `WeatherProfileDefinition`
- `WeatherGameplayService` (résolution tuile → région → profil)

## P8-6 — Événements communs et éditeur

- Modèle `CommonEventDefinition`, commande `call_common_event` avec limite de récursion
- Éditeur : `MapEventPageEditorDialog` (JSON pages) + bouton « Éditer pages » + publish dans `MapEventsBrowseDialog`

## Fichiers clés

| Zone | Fichiers |
| --- | --- |
| Runtime | `MapEventCommandExecutor.cs`, `MapEventRuntimeService.cs` |
| Core | `MapEventParameterSchemas.cs`, modèles Phase 8 dans `Frog.Core/Models/` |
| PG | `Phase8PlayerProgress` migration, repos quest/profession |
| Editor | `MapEventPageEditorDialog.cs` |
