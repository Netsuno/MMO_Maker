# clientversionmanifest-compare

← [Core](README.md) · [Référence](../README.md)

Compare manifeste local vs distant (fichiers) — **launcher stub**.

*Source : `ClientVersionManifest.Compare`* · Tip : `a6edd821` · #31

**Signature :** `compare(local, remote)`

**Entrées :**
- `local` / `remote` (`ClientVersionManifest`)

**Sorties :**
- (`VersionCheckResult`) — Current / UpdateAvailable / UpdateRequired / IncompatibleProtocol + message

**Exemple :**
```csharp
var check = ClientVersionManifest.Compare(local, remote);
if (check.Outcome == VersionCheckOutcome.UpdateRequired) return;
// Compare manifeste local vs distant (fichiers) — launcher stub
```

**Note :** script `scripts/check-client-version.sh` ; pas d’installateur.
