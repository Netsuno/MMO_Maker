# editortool-mobzone

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Outil Zone : un rectangle de monstres, sans peindre de tuile.

*Source : `EditorTool.MobZone` — `EditorToolHotkeys` touche Z, `PaletteHint`*

**Signature :** `EditorTool.MobZone → 11`

**Entrées :**
- —

**Sorties :**
- `value` (`EditorTool`) — valeur 11

**Refus :**
- le menu Carte WPF livré n’a pas cette commande (lacune connue)

**Exemple :**
```csharp
EditorTool tool = EditorTool.MobZone;
EditorToolHotkeys.TryResolveKeyCode(Keys.Z, out tool);
// PaletteHint inclut « Z zone »
```
