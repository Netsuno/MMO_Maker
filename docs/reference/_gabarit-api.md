# template-reference-api.md — fiche + index (style Marc)

Remplace le style « narratif protocole / tableaux 2 colonnes » pour la zone **Référence**.
Fiches courtes, scannables, **pas de mur**.

---

## 1. Une fiche fonction

Ancre / titre de section = nom de la fonction (minuscules si c’est le symbole documenté, sinon nom réel du code).

```markdown
### getitem

Recevoir un item.

**Signature :** `getitem(id, nombre)`

**Entrées :**
- `id` (`Guid` | `int`) — identifiant catalogue de l’objet
- `nombre` (`int`) — quantité demandée (&gt; 0)

**Sorties :**
- `ok` (`bool`) — `true` si l’ajout a réussi (ou partiel selon règles)
- `ajoute` (`int`) — quantité réellement placée dans l’inventaire
- `raison` (`string`, si échec) — motif court (plein, id inconnu…)
```

### Règles densité fiche

| Règle | Détail |
| --- | --- |
| Description | **1 phrase** sous le `###` (ce que ça fait) |
| Signature | Une ligne `nom(args)` en backticks |
| Entrées / Sorties | Listes à puces `nom` (`type`) — description ≤ ~12 mots |
| Pas de tableau | Sauf index ; la fiche elle-même = listes |
| Pas d’essai | Pas de paragraphe « contexte Phase… » dans la fiche |
| Erreurs | Optionnel : 2–4 puces max sous **Refus :** si utile |
| Secrets | Aucun exemple de mot de passe / DSN |
| Statut | Si pas « sur main » : une ligne `*Statut : non livré (P10-x)*` sous la description |

### Variante avec refus

```markdown
### getitem

Recevoir un item.

*Statut : aligner sur le build réel*

**Signature :** `getitem(id, nombre)`

**Entrées :**
- `id` (`Guid` | `int`) — identifiant catalogue de l’objet
- `nombre` (`int`) — quantité demandée (&gt; 0)

**Sorties :**
- `ok` (`bool`) — succès
- `ajoute` (`int`) — quantité placée
- `raison` (`string`) — motif si `ok` = false

**Refus :**
- `nombre &lt;= 0`
- `id` inconnu au catalogue
- inventaire plein (comportement exact = code)
```

---

## 2. Page sous-projet (assembly)

Un fichier / page wiki par sous-projet. Fonctions **A–Z** (ordre du nom).

Sous-projets : **Client** · **Server** · **Editor** · **Core** · **PostgreSQL**.

```markdown
# Référence — Core

← [Référence](../README.md)

Fonctions Core, A–Z. Style `nom(args)`.

## Index

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [getitem](#getitem) | `getitem(id, nombre)` | Recevoir un item |
| [hasitem](#hasitem) | `hasitem(id)` | Tester la présence |

---

### getitem
…
```

### Densité page sous-projet

- **Index en tête** (tableau) puis fiches en dessous — ou fiches dans des fichiers séparés liés depuis l’index si &gt; ~15 fonctions.
- Seuil soft : **≤ ~15 fiches** dans un même fichier scrollable ; au-delà → un fichier par fonction + index seul.
- Pas de TOC opcodes legacy sur cette page.
- Fil d’Ariane `← Référence` obligatoire.

---

## 3. Index racine (tous les sous-projets)

```markdown
# Référence

Fonctions documentées : `nom(args)` · entrées/sorties typées.
Classement **par sous-projet**, puis **A–Z**.

> Au fil de l’eau. Absent / prévu ≠ livré.

## Sous-projets (assemblies)

| Sous-projet | Page | Contenu (1 ligne) |
| --- | --- | --- |
| Client | [Client](client/README.md) | UI, appels locaux client |
| Server | [Server](server/README.md) | Handlers, services |
| Editor | [Editor](editor/README.md) | Publish, outils auteur |
| Core | [Core](core/README.md) | Règles / helpers partagés |
| PostgreSQL | [PostgreSQL](postgresql/README.md) | Repos, accès données |

## Comment lire

1. Ouvrir le sous-projet
2. Lire la **Signature**
3. Parcourir **Entrées** / **Sorties** (chaque variable est décrite)

Exemple : [getitem](core/README.md#getitem) · modèle DA : ce fichier
```

Wiki : mêmes libellés (`Référence-Core`, etc.). Home : lien **Référence (dev / ops)**.

---

## 4. Ce qui ne va plus dans Référence API

| Mettre ailleurs | Pourquoi |
| --- | --- |
| Récits opcode 1, 2, 3… en prose | → migrer en fiches `nom(args)` ou doc wire séparée |
| Scripts shell (`publish-frog.sh`) | → Guides Ops (pas une fonction code) |
| « Chaque bouton » UI | → Guides (voir `template-guides-chaque-bouton.md`) ; lien optionnel vers la fiche API |

---

## 5. Checklist revue DA (avant tip)

- [ ] Index racine = 5 lignes sous-projets, pas de liste plate de 50 fonctions
- [ ] Chaque sous-projet : tableau index A–Z puis fiches
- [ ] Chaque fiche : 1 phrase + signature + listes Entrées/Sorties
- [ ] Chaque variable a type + description courte
- [ ] Aucun mur (&gt; ~8 lignes de prose d’affilée)
- [ ] Fil d’Ariane présent
- [ ] Statuts honnêtes
