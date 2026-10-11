# composedtilesetclientloader

← [Client](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Chargeur HTTP des tilesets composés publiés.

*Source : `ComposedTilesetClientLoader` — deux constructeurs publics*

**Signature :** `composedtilesetclientloader(options, transport?) → ComposedTilesetClientLoader`

**Membres :**
- `PackPath` (`string`) — égal à `/content/composed-tilesets`
- `Tilesets` (`IReadOnlyList<ComposedTilesetDefinition>`) — dernière liste acceptée

**Entrées :**
- `options` (`TilePackClientOptions`) — URL de contenu et clé publique
- `transport` (`ITilePackTransport?`) — défaut : HTTP possédé par le chargeur

**Sorties :**
- `instance` (`ComposedTilesetClientLoader`) — prête pour SyncAsync

**Exemple :**
```csharp
var loader = new ComposedTilesetClientLoader(options);
IReadOnlyList<ComposedTilesetDefinition> tiles = loader.Tilesets;
loader.Dispose();
```
