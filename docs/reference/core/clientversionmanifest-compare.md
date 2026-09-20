# clientversionmanifest-compare

← [Core](README.md) · [Référence](../README.md)

Compare manifeste local vs distant (fichiers) — **launcher stub**.

*Source : `ClientVersionManifest.Compare`* · Tip : `d6e59759` · #31

**Signature :** `compare(local, remote)`

**Entrées :**
- `local` / `remote` (`ClientVersionManifest`)

**Sorties :**
- (`VersionCheckResult`) — Current / UpdateAvailable / UpdateRequired / IncompatibleProtocol + message

**Note :** script `scripts/check-client-version.sh` ; pas d’installateur.
