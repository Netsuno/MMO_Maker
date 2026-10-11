# Référence — Core

← [Référence](../README.md)

Fonctions Core, A–Z. Style `nom(args)`. Tip miroir : `2af2dd22`.

## Index

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [meleedamage](#meleedamage) | `meleedamage(attackerStr, weaponPower, targetVit)` | Dégâts mêlée |
| [monsterexperience](#monsterexperience) | `monsterexperience(level)` | XP monstre |
| [monstermaxhp](#monstermaxhp) | `monstermaxhp(level)` | PV max monstre |
| [socialwirebuildrequest](#socialwirebuildrequest) | `socialwirebuildrequest(kind, action, requestId, extra)` | Corps SocialRequest |
| [socialwiretryparserequest](#socialwiretryparserequest) | `socialwiretryparserequest(payload)` | Parse SocialRequest |
| [spelldamage](#spelldamage) | `spelldamage(attackerInt, spellPower, targetVit)` | Dégâts sort |
| [spellpowerfrommanacost](#spellpowerfrommanacost) | `spellpowerfrommanacost(manaCost)` | Puissance depuis mana |
| [wireprotocolversion](#wireprotocolversion) | `wireprotocolversion()` | Version TCP Hello |



## AudioMixer (#28)

Mute / volume / musique. Tip : `a6edd821`.

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [apply](audiomixer-apply.md) | `apply(volumePercent, muted, musicEnabled)` | Applique settings Son |
| [play](audiomixer-play.md) | `play(cue)` | Lecture cue |
| [shouldplay](audiomixer-shouldplay.md) | `shouldplay(cue)` | Audible ? |
| [setvolume](audiomixer-setvolume.md) | `setvolume(percent)` | Volume |
| [setmuted](audiomixer-setmuted.md) | `setmuted(muted)` | Mute |
| [setmusicenabled](audiomixer-setmusicenabled.md) | `setmusicenabled(enabled)` | Musique opt-in |

## Weather (#30)

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [resolve](weatherresolver-resolve.md) | `resolve(…)` | Plan overlay |
| [cycle](weatherresolver-cycle.md) | `cycle(current)` | F8 |
| [forkind](weathercatalog-forkind.md) | `forkind(kind?)` | Catalogue |
| [shouldplayambience](weatheraudio-shouldplayambience.md) | `shouldplayambience(plan, mixer)` | Mute gate |

## Maintenance / launcher stub (#31)

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [parse](clientversionmanifest-parse.md) | `parse(text)` | VERSION fichier |
| [compare](clientversionmanifest-compare.md) | `compare(local, remote)` | Compare stub |
| [toplayerfacing](maintenancemessages-toplayerfacing.md) | `toplayerfacing(raw?)` | Message joueur |

## ClientSocialRoster (HUD social #27)

État client opcodes 80–83. Une fiche / méthode (pattern Server). Tip miroir : `a6edd821`.

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [accept](clientsocialroster-accept.md) | `accept(kind, subjectOrOther)` | Construit Accept |
| [accepttarget](clientsocialroster-accepttarget.md) | `accepttarget(item)` | Guid Accept/Decline |
| [applyevent](clientsocialroster-applyevent.md) | `applyevent(ev)` | Applique event 83 |
| [applyresult](clientsocialroster-applyresult.md) | `applyresult(result)` | Applique result 81 |
| [applysnapshot](clientsocialroster-applysnapshot.md) | `applysnapshot(snapshot)` | Applique snapshot 82 |
| [buildrows](clientsocialroster-buildrows.md) | `buildrows(kind)` | Lignes UI invites+membres |
| [decline](clientsocialroster-decline.md) | `decline(kind, subjectOrOther)` | Construit Decline |
| [disband](clientsocialroster-disband.md) | `disband(kind)` | Dissout groupe/guilde |
| [emptyhint](clientsocialroster-emptyhint.md) | `emptyhint(kind)` | Texte liste vide |
| [friendremove](clientsocialroster-friendremove.md) | `friendremove(target)` | Retire un ami |
| [invite](clientsocialroster-invite.md) | `invite(kind, target)` | Invite / demande ami |
| [kick](clientsocialroster-kick.md) | `kick(kind, target)` | Expulse un membre |
| [leave](clientsocialroster-leave.md) | `leave(kind)` | Quitte groupe/guilde |
| [motdtext](clientsocialroster-motdtext.md) | `motdtext(kind)` | Lit MOTD |
| [transferleader](clientsocialroster-transferleader.md) | `transferleader(kind, target)` | Transfert chef |
| [tryguildcreate](clientsocialroster-tryguildcreate.md) | `tryguildcreate(name, …)` | Create guilde |
| [tryguildsetmotd](clientsocialroster-tryguildsetmotd.md) | `tryguildsetmotd(motd, …)` | Set MOTD guilde |
| [tryparsetargetguid](clientsocialroster-tryparsetargetguid.md) | `tryparsetargetguid(text, …)` | Parse Guid HUD |

Guide UI : [UI-CLIENT-SocialHub.md](../../progress/phase-10-beta-release/guides/UI-CLIENT-SocialHub.md).

## EconomyHub (#32 scaffolding)

| Groupe | Fiche | Une ligne |
| --- | --- | --- |
| EconomyHubWire | [economyhubwire](economyhubwire.md) | Codec 87–89 Query-only |
| ClientEconomyHub | [clienteconomyhub](clienteconomyhub.md) | État client listes vides |

## InstanceHub (#33 scaffolding)

| Groupe | Fiche | Une ligne |
| --- | --- | --- |
| InstanceHubWire | [instancehubwire](instancehubwire.md) | Codec 90–92 |
| ClientInstanceHub | [clientinstancehub](clientinstancehub.md) | État client |
| DungeonCatalog | [dungeoncatalog](dungeoncatalog.md) | Ruines du Marais / Crypte du Roi |
| ProceduralDungeonGenerator | [proceduraldungeongenerator](proceduraldungeongenerator.md) | Stub 2–4 salles |

## MovementFluidity (#29) — Référence only

| Fonction | Fiche | Une ligne |
| --- | --- | --- |
| MovementFluidity.* | [movementfluidity](movementfluidity.md) | Cap visual dt / stale ack — **before-only metrics** |
| MapViewportCamera.DampFocus | [mapviewportcamera-dampfocus](mapviewportcamera-dampfocus.md) | Camera expo 16/s |

Pas de page guide joueur pour #29.



## Contenu 48×48 et zones (#146–#148)

Tuiles composées, objets de carte, zones de monstres, A–Z. CTS1 n’est pas un opcode. Pipeline : [tuiles 48×48](../pipeline-tuiles-48.md).

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [composedtileref](composedtileref.md) | `composedtileref → ComposedTileRef` | Une tuile choisie : identifiant de contenu et pixels 48×48 |
| [composedtilesetdefinition](composedtilesetdefinition.md) | `composedtilesetdefinition → ComposedTilesetDefinition` | Tileset fait de tuiles 48×48 choisies, dans l’ordre de la palette |
| [composedtilesetdefinition-normalize](composedtilesetdefinition-normalize.md) | `normalize() → void` | Trim le nom, le chemin et les identifiants de tuile |
| [composedtilesetdefinition-validate](composedtilesetdefinition-validate.md) | `validate(out error) → bool` | Vérifie le nom, le chemin et des tuiles 48×48 dont l’id colle aux pixels |
| [composedtilesetpackformat](composedtilesetpackformat.md) | `composedtilesetpackformat → constantes` | En-tête de la charge signée CTS1. Ce n’est pas un opcode Hello |
| [composedtilesetpackreader-read](composedtilesetpackreader-read.md) | `read(data, trustedPublicKey) → IReadOnlyList<ComposedTilesetDefinition>` | Lit une charge CTS1 et vérifie la clé publique épinglée |
| [composedtilesetpackwriter-write](composedtilesetpackwriter-write.md) | `write(tilesets, privateSeed) → byte[]` | Écrit la charge CTS1 signée Ed25519, tuile annoncée 48 |
| [computesha256hex](computesha256hex.md) | `computesha256hex(bytes) → string` | Empreinte SHA-256 hex des octets, même calcul que le tileset feuille |
| [createplacementid](createplacementid.md) | `createplacementid(id) → string` | Fabrique l’identifiant de pose `obj-` plus le Guid compact |
| [formatentry](formatentry.md) | `formatentry(entry) → string` | Formate une ligne de monstre pour la liste |
| [formatstatus](formatstatus.md) | `formatstatus(zoneName) → string` | Phrase de barre d’état pour l’outil Zone |
| [isunprotectedcatalog](isunprotectedcatalog.md) | `isunprotectedcatalog(payload) → bool` | Détecte un catalogue JSON brut, que CTS1 refuse |
| [mapobjectdefinition](mapobjectdefinition.md) | `mapobjectdefinition → MapObjectDefinition` | Prop plaçable, distinct d’un objet d’inventaire |
| [mapobjectdefinition-validate](mapobjectdefinition-validate.md) | `validate(out error) → bool` | Vérifie le nom, le chemin, l’empreinte et le SHA-256 |
| [mobspawnentry](mobspawnentry.md) | `mobspawnentry → MobSpawnEntry` | Ligne de zone : un monstre, une quantité vivante, un délai |
| [mobspawnzone](mobspawnzone.md) | `mobspawnzone → MobSpawnZone` | Rectangle de tuiles. Les monstres n’apparaissent qu’à l’intérieur |
| [mobspawnzonedocument](mobspawnzonedocument.md) | `mobspawnzonedocument → MobSpawnZoneDocument` | Zones d’une carte, hors blob .fmap et hors Hello |
| [mobspawnzonedocument-clone](mobspawnzonedocument-clone.md) | `clone() → MobSpawnZoneDocument` | Copie le document et ses lignes |
| [mobspawnzonedocument-empty](mobspawnzonedocument-empty.md) | `empty() → MobSpawnZoneDocument` | Crée un document sans zone |
| [mobspawnzonedocument-find](mobspawnzonedocument-find.md) | `find(id) → MobSpawnZone?` | Cherche une zone par identifiant |
| [mobspawnzonedocument-fromjson](mobspawnzonedocument-fromjson.md) | `fromjson(json) → MobSpawnZoneDocument` | Relit le JSON des zones. Une version autre que 1 est refusée |
| [mobspawnzonedocument-replacewith](mobspawnzonedocument-replacewith.md) | `replacewith(source) → void` | Remplace les zones par une copie de la source |
| [mobspawnzonedocument-tojson](mobspawnzonedocument-tojson.md) | `tojson() → string` | Sérialise en JSON camelCase (formatVersion, zones, entries) |
| [mobspawnzonedocument-tryfromjson](mobspawnzonedocument-tryfromjson.md) | `tryfromjson(json, out document) → bool` | Relit le JSON. Une chaîne vide donne un document vide |
| [mobspawnzoneedit-cliptomap](mobspawnzoneedit-cliptomap.md) | `cliptomap(document, mapWidth, mapHeight) → int` | Retire les zones qui ne tiennent plus sur la carte |
| [mobspawnzoneedit-contains](mobspawnzoneedit-contains.md) | `contains(zone, tileX, tileY) → bool` | Dit si la case est dans le rectangle |
| [mobspawnzoneedit-hittest](mobspawnzoneedit-hittest.md) | `hittest(zones, tileX, tileY, preferredId?) → MobSpawnZone?` | Trouve la zone sous une case. Une zone préférée gagne |
| [mobspawnzoneedit-picktile](mobspawnzoneedit-picktile.md) | `picktile(zone, random) → (int X, int Y)` | Choisit une case uniforme dans le rectangle |
| [mobspawnzoneedit-tryaddentry](mobspawnzoneedit-tryaddentry.md) | `tryaddentry(zone, monsterId, label, quantity, respawnSeconds, out entry, out error) → bool` | Ajoute un monstre dans la zone |
| [mobspawnzoneedit-trycopy](mobspawnzoneedit-trycopy.md) | `trycopy(document, mapWidth, mapHeight, sourceId, tileX, tileY, out copy, out error) → bool` | Copie une zone vers un autre coin |
| [mobspawnzoneedit-trycreate](mobspawnzoneedit-trycreate.md) | `trycreate(document, mapWidth, mapHeight, tileX, tileY, width, height, out zone, out error) → bool` | Ajoute un rectangle au document |
| [mobspawnzoneedit-trymove](mobspawnzoneedit-trymove.md) | `trymove(zone, mapWidth, mapHeight, tileX, tileY) → bool` | Déplace le rectangle sans changer sa taille |
| [mobspawnzoneedit-trynormalizerect](mobspawnzoneedit-trynormalizerect.md) | `trynormalizerect(x0, y0, x1, y1, mapWidth, mapHeight, out tileX, out tileY, out width, out height) → bool` | Ramène un rectangle de tracé dans les bords de la carte |
| [mobspawnzoneedit-tryremove](mobspawnzoneedit-tryremove.md) | `tryremove(document, id) → bool` | Retire une zone du document |
| [mobspawnzoneedit-tryremoveentry](mobspawnzoneedit-tryremoveentry.md) | `tryremoveentry(zone, entryId) → bool` | Retire une ligne de la zone |
| [mobspawnzoneedit-tryrename](mobspawnzoneedit-tryrename.md) | `tryrename(zone, name, out error) → bool` | Renomme une zone (1–80 caractères) |
| [mobspawnzoneedit-tryreplaceentry](mobspawnzoneedit-tryreplaceentry.md) | `tryreplaceentry(zone, entryId, monsterId, label, quantity, respawnSeconds, out error) → bool` | Remplace une ligne existante |
| [mobspawnzoneedit-tryvalidate](mobspawnzoneedit-tryvalidate.md) | `tryvalidate(document, mapWidth, mapHeight, out error) → bool` | Vérifie toutes les zones avant écriture PostgreSQL |
| [mobspawnzonelabels](mobspawnzonelabels.md) | `mobspawnzonelabels → constantes` | Libellés français de l’outil Zone |
| [workingtileset-servertileetid](workingtileset-servertileetid.md) | `servertileetid → Guid` | Identifiant serveur d’une palette publiée. Vide : palette locale |

### meleedamage

Calcule les dégâts d’une attaque mêlée.

**Signature :** `meleedamage(attackerStr, weaponPower, targetVit)`

**Entrées :**
- `attackerStr` (`int`) — force de l’attaquant
- `weaponPower` (`int`) — puissance de l’arme
- `targetVit` (`int`) — vitalité de la cible

**Sorties :**
- (`int`) — points de dégâts

**Exemple :**
```csharp
int dmg = CombatFormulas.MeleeDamage(attackerStr: 12, weaponPower: 8, targetVit: 4);
// max(1, STR + weaponPower/2 - targetVit/4)
// Calcule les dégâts d’une attaque mêlée
```

*Source : `CombatFormulas.MeleeDamage`.*

---

### monsterexperience

XP accordée pour un monstre tué.

**Signature :** `monsterexperience(level)`

**Entrées :**
- `level` (`int`) — niveau du monstre

**Sorties :**
- (`long`) — expérience

**Exemple :**
```csharp
long xp = CombatFormulas.MonsterExperienceReward(level: 3);
// 10 + level * 5 (level plancher 1)
// XP accordée pour un monstre tué
```

*Source : `CombatFormulas.MonsterExperienceReward`.*

---

### monstermaxhp

PV maximum d’un monstre selon le niveau.

**Signature :** `monstermaxhp(level)`

**Entrées :**
- `level` (`int`) — niveau

**Sorties :**
- (`int`) — PV max

**Exemple :**
```csharp
int hp = CombatFormulas.MonsterMaxHp(level: 3);
// 20 + level * 15 (level plancher 1)
// PV maximum d’un monstre selon le niveau
```

*Source : `CombatFormulas.MonsterMaxHp`.*

---

### socialwirebuildrequest

Construit le corps binaire d’une demande sociale (opcode 80).

**Signature :** `socialwirebuildrequest(kind, action, requestId, extra)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild / Friend / Block
- `action` (`byte`) — action dans la famille
- `requestId` (`Guid`) — corrélation requête
- `extra` (`ReadOnlySpan<byte>`) — payload typé

**Sorties :**
- (`byte[]`) — octets du corps de paquet

**Exemple :**
```csharp
byte[] body = SocialWire.BuildRequest(
    SocialKind.Party, action, requestId, extra);
// Construit le corps binaire d’une demande sociale (opcode 80)
```

*Source : `SocialWire.BuildRequest`.*

---

### socialwiretryparserequest

Parse le corps d’une SocialRequest.

**Signature :** `socialwiretryparserequest(payload)`

**Entrées :**
- `payload` (`ReadOnlySpan<byte>`) — corps reçu

**Sorties :**
- `ok` (`bool`) — parse réussi
- `kind` (`SocialKind`) — famille (octet brut, même si inconnu)
- `action` (`byte`) — action
- `requestId` (`Guid`) — id
- `extra` (`ReadOnlyMemory<byte>`) — reste

**Refus :**
- payload plus court que l’en-tête (1 + 1 + 16)

**Exemple :**
```csharp
if (!SocialWire.TryParseRequest(payload, out var kind, out var action, out var requestId, out var extra))
    return; // payload trop court
// Parse le corps d’une SocialRequest
```

*Source : `SocialWire.TryParseRequest`.*

---

### spelldamage

Calcule les dégâts d’un sort.

**Signature :** `spelldamage(attackerInt, spellPower, targetVit)`

**Entrées :**
- `attackerInt` (`int`) — intelligence
- `spellPower` (`int`) — puissance du sort
- `targetVit` (`int`) — vitalité cible

**Sorties :**
- (`int`) — dégâts

**Exemple :**
```csharp
int dmg = CombatFormulas.SpellDamage(attackerInt: 10, spellPower: 6, targetVit: 4);
// max(1, INT + spellPower - targetVit/5)
// Calcule les dégâts d’un sort
```

*Source : `CombatFormulas.SpellDamage`.*

---

### spellpowerfrommanacost

Dérive une puissance de sort depuis le coût mana.

**Signature :** `spellpowerfrommanacost(manaCost)`

**Entrées :**
- `manaCost` (`int`) — coût mana

**Sorties :**
- (`int`) — puissance

**Exemple :**
```csharp
int power = CombatFormulas.SpellPowerFromManaCost(manaCost: 8);
// max(1, manaCost)
// Dérive une puissance de sort depuis le coût mana
```

*Source : `CombatFormulas.SpellPowerFromManaCost`.*

---

### wireprotocolversion

Version incompatible du contrat TCP (champ Hello).

**Signature :** `wireprotocolversion()`

**Entrées :** —
*(constante)*

**Sorties :**
- (`ushort`) — `11` sur le tip actuel

**Exemple :**
```csharp
ushort version = FrogWireProtocol.Version; // 11 — ne pas bumper
// Version incompatible du contrat TCP (champ Hello)
// (ushort) — 11 sur le tip actuel
```

*Source : `FrogWireProtocol.Version`. Wire v11 ; social 80–83 ; #28 audio ; #30 weather trailer 74 ; #31 maintenance. Tip `2af2dd22` (#145–#148) : pas de bump. CTS1 est un GET HTTP, pas un opcode.*
