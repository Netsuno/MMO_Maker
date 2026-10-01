# clientversionmanifest-parse

← [Core](README.md) · [Référence](../README.md)

Parse un fichier VERSION (`key=value`) — **launcher stub** (pas HTTP).

*Source : `ClientVersionManifest.Parse`* · Tip : `a6edd821` · #31

**Signature :** `parse(text)`

**Entrées :**
- `text` (`string`) — lignes `product=`, `protocol=`, `minClient=` optionnel

**Sorties :**
- (`ClientVersionManifest`) — versions produit / protocole / minClient

**Refus :**
- `product` ou `protocol` manquant → `FormatException`

**Exemple :**
```csharp
var local = ClientVersionManifest.Parse(File.ReadAllText("VERSION"));
// product= / protocol= manquants → FormatException
// Parse un fichier VERSION (key=value) — launcher stub (pas HTTP)
```
