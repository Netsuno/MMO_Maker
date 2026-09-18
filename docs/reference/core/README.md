# Référence — Core

← [Référence](../README.md)

Fonctions Core, A–Z. Style `nom(args)`. Tip : `6fe5bd97`.

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

---

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
- (`ushort`) — `10` sur le tip actuel

*Source : `FrogWireProtocol.Version`. Social 80–83 peut exiger 11 (gel).*
