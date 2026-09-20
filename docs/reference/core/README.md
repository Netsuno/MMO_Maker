# Référence — Core

← [Référence](../README.md)

Fonctions Core, A–Z. Style `nom(args)`. Tip miroir : `d6e59759`.

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

Mute / volume / musique. Tip : `d6e59759`.

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

État client opcodes 80–83. Une fiche / méthode (pattern Server). Tip miroir : `d6e59759`.

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



### meleedamage

Calcule les dégâts d’une attaque mêlée.

**Signature :** `meleedamage(attackerStr, weaponPower, targetVit)`

**Entrées :**
- `attackerStr` (`int`) — force de l’attaquant
- `weaponPower` (`int`) — puissance de l’arme
- `targetVit` (`int`) — vitalité de la cible

**Sorties :**
- (`int`) — points de dégâts

*Source : `CombatFormulas.MeleeDamage`.*

---

### monsterexperience

XP accordée pour un monstre tué.

**Signature :** `monsterexperience(level)`

**Entrées :**
- `level` (`int`) — niveau du monstre

**Sorties :**
- (`long`) — expérience

*Source : `CombatFormulas.MonsterExperienceReward`.*

---

### monstermaxhp

PV maximum d’un monstre selon le niveau.

**Signature :** `monstermaxhp(level)`

**Entrées :**
- `level` (`int`) — niveau

**Sorties :**
- (`int`) — PV max

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

*Source : `SocialWire.BuildRequest`.*

---

### socialwiretryparserequest

Parse le corps d’une SocialRequest.

**Signature :** `socialwiretryparserequest(payload)`

**Entrées :**
- `payload` (`ReadOnlySpan<byte>`) — corps reçu

**Sorties :**
- `ok` (`bool`) — parse réussi
- `kind` (`SocialKind`) — famille
- `action` (`byte`) — action
- `requestId` (`Guid`) — id
- `extra` (`ReadOnlySpan<byte>`) — reste

**Refus :**
- payload trop court / kind inconnu

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

*Source : `CombatFormulas.SpellDamage`.*

---

### spellpowerfrommanacost

Dérive une puissance de sort depuis le coût mana.

**Signature :** `spellpowerfrommanacost(manaCost)`

**Entrées :**
- `manaCost` (`int`) — coût mana

**Sorties :**
- (`int`) — puissance

*Source : `CombatFormulas.SpellPowerFromManaCost`.*

---

### wireprotocolversion

Version incompatible du contrat TCP (champ Hello).

**Signature :** `wireprotocolversion()`

**Entrées :** —
*(constante)*

**Sorties :**
- (`ushort`) — `11` sur le tip actuel

*Source : `FrogWireProtocol.Version`. Wire v11 ; social 80–83 ; #28 audio ; #30 weather trailer 74 ; #31 maintenance (pas de bump).*
