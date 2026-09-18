# publishmap

← [Editor](README.md) · [Référence](../README.md)

Publie la carte courante vers PostgreSQL (menu **Publier (PostgreSQL)…**).

*Source : `MainForm.PublishMap` → `PublishMapCoreAsync` / `SaveMapIntent.Publish`*

**Signature :** `publishmap()`

**Entrées :** —
*(carte courante + dialogue métier si requis par le workspace)*

**Sorties :**
- `SaveMapResult` après publish
- statut carte « publiée » si succès

**Refus :**
- persistance non durable / validation / conflit
