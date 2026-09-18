# Gabarit — fiche fonction (style code)

Décision Marc (2026-09-18) : la zone **Référence** documente des **fonctions** comme du code lisible, pas seulement des gestes UI ou des opcodes.

## Signature

```text
nomFonction(param1, param2, …)
```

- `nomFonction` : **camelCase** (ou le nom réel du symbole dans le sous-projet si déjà fixé).
- Paramètres dans l’ordre d’appel ; types explicites dans le tableau Entrées.
- Une fiche = **une** fonction (pas un paquet d’opcodes entier).

## Emplacement

Classement **A–Z par sous-projet**, puis A–Z du nom de fonction :

| Sous-projet | Dossier / page wiki suggérée |
| --- | --- |
| Client | `docs/reference/client/` · wiki `Référence-Client` |
| Server | `docs/reference/server/` · wiki `Référence-Server` |
| Editor | `docs/reference/editor/` · wiki `Référence-Editor` |
| Core | `docs/reference/core/` · wiki `Référence-Core` |
| PostgreSQL | `docs/reference/postgresql/` · wiki `Référence-PostgreSQL` |

Index racine : `docs/reference/README.md` + wiki `Référence` — listes A–Z par sous-projet (voir `template-reference-index-az.md`).

## Structure Markdown (copier-coller)

```markdown
# nomFonction(param1, param2)

← [Référence](../README.md) · [Sous-projet](./README.md)

`Sous-projet` · Statut : Disponible | En cours P10-x | Non livré

Une phrase : ce que la fonction fait (effet observable).

## Signature

```text
nomFonction(param1, param2) → ResultType
```

## Entrées

| Nom | Type | Description |
| --- | --- | --- |
| param1 | `Type` | À quoi ça sert, contraintes (min/max, non null…) |
| param2 | `Type` | … |

## Sorties

| Nom | Type | Description |
| --- | --- | --- |
| (retour) | `ResultType` | Ce que l’appelant reçoit |
| effet latéral | — | Ex. inventaire mis à jour, paquet envoyé |

## Erreurs / refus

| Condition | Comportement |
| --- | --- |
| … | … |

## Voir aussi

- Fonctions voisines
- Guide UI / bouton qui déclenche cet appel (si applicable)
```

## Règles DA

1. **Chaque variable** (entrée et sortie) a une description courte (≤ ~15 mots).
2. Types en `` `backticks` `` (`int`, `string`, `Guid`, `ItemId`, noms de DTO du projet).
3. Pas de secret / DSN / mot de passe en exemple.
4. Statut honnête — pas de READY inventé.
5. Si la fonction est déclenchée par un **bouton UI**, lien vers la ligne du guide « chaque bouton » (voir `template-guides-chaque-bouton.md`).
6. Anciennes fiches « opcode / action joueur » : migrer vers ce format **ou** laisser une ligne d’index qui pointe vers la nouvelle fiche ; ne pas dupliquer deux styles sur la même fonction.

## Anti-patterns

- Titre uniquement humain sans signature (`## Banque`) → non ; utiliser `bankDeposit(slot, quantity)`.
- Entrées en prose sans tableau de variables → non.
- Une page « Inventaire 38–59 » fourre-tout → découper en fonctions.
