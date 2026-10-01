# publishmapdialogtryvalidate

← [Editor](README.md) · [Référence](../README.md)

*Statut : historique — symbole absent de `Frog.Editor`.*

`PublishMapDialog.TryValidate` documentait le dialogue de publication MariaDB (id, clé, nom affiché). Ce dialogue et ce type ne sont plus dans l’éditeur (retrait #83).

L’enregistrement et la publication passent par **Fichier → Enregistrer (PostgreSQL)** et **Fichier → Publier (PostgreSQL)…** :

- [savemap](savemap.md) — `MainForm.SaveMap`
- [publishmap](publishmap.md) — `MainForm.PublishMap`
- [canexecutesaveorpublish](canexecutesaveorpublish.md) — `AllowsSave`

En démo (`InMemoryDemo`), `AllowsSave` est faux : les commandes WPF sont inactives. Leurs libellés restent « Enregistrer (PostgreSQL) » et « Publier (PostgreSQL)… ». Parcours auteur : [quickstart](../../progress/phase-10-beta-release/guides/CREATOR_QUICKSTART.md).

**Exemple :**
```csharp
// PublishMapDialog.TryValidate est absent (retrait #83).
// Chemin courant : Fichier → Publier (PostgreSQL)…
if (form.CanExecuteSaveOrPublish()) form.PublishMap();
```
