# Plan d’étapes — Client UI modernization

**Propriétaire :** Netsun  
**Ordre recommandé :** UI-1 → UI-15. Chaque étape est **indépendamment mergeable** si elle respecte son contrat « done » et ne casse pas les smokes.  
**Ce run :** documentation seulement. Aucune étape produit n’est commencée.

Ne pas enchaîner une réécriture HUD monolithique. Si une étape glisse vers le serveur ou Phase 10 : **stop**.

---

## Vue d’ordre

```text
UI-1  Thème (chrome actuel, layout inchangé)
  └─► UI-2  OverlayHost (ancres, show/hide, focus input)
        ├─► UI-3  PlayerHud (CombatState)
        ├─► UI-4  Chat dock (3 canaux + historique)
        ├─► UI-5  Inventaire / équipement overlay
        ├─► UI-6  Dialogue modal
        ├─► UI-7  Quêtes + tracker (même snapshot)
        ├─► UI-8  Boutique overlay
        ├─► UI-9  Fiche personnage (lecture seule)
        ├─► UI-10 Login / sélection perso (même DA)
        ├─► UI-11 Options + UserSettings fichier
        ├─► UI-12 Minimap (cache local)
        ├─► UI-13 Hotbar + menu BD
        └─► UI-14 Banque / sol (chrome)
UI-15 Isolation rendu carte / budget 60 FPS   ← peut démarrer en parallèle dès UI-1
```

UI-15 est volontairement **découplée** : le `Bitmap` full-map est déjà le risque #1 ; attendre la fin du chrome pour le traiter serait une erreur.

---

## Règles communes à chaque étape

- Branche de travail **séparée** de `cursor/phase10-beta-release`.
- Pas de nouveau `PacketId`. Pas de changement `Frog.Server` « pour l’UI ».
- Smokes `GameplayClientSmokeTests` + Phase 8 : **verts** (ou manifeste SHA mis à jour **dans la même PR** si le chrome visible change).
- Accesseurs `*ForTest` conservés.
- Critère perf : l’étape n’ajoute pas d’appel `RedrawMap()` / `MapViewRenderer.Render` / `Control.Refresh()` sur le timer 16 ms, sauf UI-15 qui **réduit** ce coût.
- Done = code + smokes concernés + note courte dans ce dossier (mise à jour [STATUS.md](STATUS.md)), pas une spec de plus.

---

## UI-1 — Jetons de thème + skin du chrome existant

**But :** sombre + or sur Login / Character / toolbar / onglets / boutons, **sans** bouger les docks.

**Fait :**

- `UiTheme` + application à la construction
- Contrastes lisibles (texte clair sur panneau sombre)
- Aucun changement de hiérarchie `BuildLayout` (toujours toolbar + carte + 360 px + log)
- Smokes : PASS ; si captures Phase 8 changent, manifeste mis à jour

**Hors :** overlay, minimap, nouveaux contrôles.

**Risque 60 FPS :** faible si `Apply` n’est pas dans `SmoothTimer_OnTick`.

---

## UI-2 — OverlayHost

**But :** contrat d’ancres + `Show/Hide/Toggle` ; le rail droit **peut** encore exister (feature flag / onglets gardés) pour ne pas casser les smokes d’un coup.

**Fait :**

- Host au-dessus de la carte **sans** `TransparencyKey`
- `IsTextInputFocused`
- Au moins un overlay de démo (ex. toggle du log) **ou** simple panneau vide masqué par défaut
- Carte continue de se redessiner **uniquement** via le chemin mouvement existant

**Hors :** migrer tous les onglets d’un coup.

**Risque 60 FPS :** invalidation parent. Done refuse `Invalidate` sur `MainShellForm` à 16 ms. Mesure qualitative : bouger avec un overlay ouvert ne stutter pas plus que baseline (UI-15 mesurera).

---

## UI-3 — PlayerHud

**But :** HG compact : nom, niveau, HP/MP (barres), XP optionnelle, or. Source = `CombatStateReceived` uniquement.

**Fait :**

- Remplace visuellement `_lblCombat` (le label peut rester hidden pour tests **ou** le test lit le HUD)
- Respawn reste branché sur `IsDead`
- Pas de portrait obligatoire (placeholder couleur ok)

**Hors :** classe/guilde, interpolation des barres « fake regen ».

**Risque 60 FPS :** update HUD **event-driven**, pas par frame.

---

## UI-4 — Chat dock

**But :** historique dédié + saisie + Global / Map / Whisper + ligne Système (copie filtrée du journal erreurs si besoin). Slash modération inchangé.

**Fait :**

- Réception n’est plus **uniquement** `[G]…` dans `_txtLog` (le log peut encore dupliquer pour `LogContainsForTest`)
- Focus saisie bloque le mouvement
- Pas d’onglet Guilde / Groupe

**Hors :** emotes, bulles monde.

**Risque 60 FPS :** `ListBox` / `RichTextBox` — append ligne, pas rebuild complet. Cap d’historique (ex. 200 lignes).

---

## UI-5 — Inventaire + équipement overlay

**But :** fenêtre overlay réutilisant `InventoryPanel` / `EquipmentPanel` (restyle interne ok : owner-draw grille **si** les events et `*ForTest` restent).

**Fait :**

- Toggle (bouton menu ou touche I)
- Or = `CombatState.Gold`
- Textes `Arme: {Name}` / `Armure:` préservés **ou** smokes adaptés
- Toujours 2 slots équipement

**Hors :** drag-and-drop, tooltips riches, icônes items (sauf si bitmap déjà au catalogue — ne pas bloquer).

**Risque 60 FPS :** `ApplySnapshot` déjà rebuild la liste (ok, event). Interdit : snapshot → `RedrawMap`.

---

## UI-6 — Dialogue modal

**But :** `DialoguePanel` en modal centre (au-dessus du monde), auto-show sur `DialogueStatePush`, clear sur refus / fin.

**Fait :**

- Smokes `DialoguePanelForTest` / `ClickFirstChoiceForTest` / speaker
- Token session inchangé

**Hors :** portraits PNJ custom, voicelines.

**Risque 60 FPS :** ouverture event-only.

---

## UI-7 — Journal de quêtes + tracker

**But :** fenêtre quêtes (filtre En cours / Terminées **côté client**) + tracker compact (1–3 lignes) branché sur **le même** `ApplySnapshot`.

**Fait :**

- Turn-in inchangé
- Tracker ne fetch rien
- Onglet Phase 8 peut rester comme hôte de secours le temps que `SelectPhase8TabForTest` soit remappé

**Hors :** carte des objectifs, nouveaux types de quêtes.

---

## UI-8 — Boutique overlay

**But :** fenêtre Achat / Vente à la place des combos toolbar, **mêmes** `SendShopBuyAsync` / `SendShopSellAsync` et helpers `TrySelect*ForTest`.

**Fait :**

- Liste nommée depuis le catalogue publié
- Qty + résultats (log ou label statut)
- GUID secours peuvent rester hidden

**Hors :** icônes 3D, preview stats item (sauf champs catalogue déjà lus).

---

## UI-9 — Fiche personnage

**But :** overlay lecture seule : nom, niveau, HP/MP/XP/or, 6 stats JSON.

**Fait :**

- Pas de `CharacterStatsUpdate` depuis cette fiche
- Alias FR optionnels documentés (STR→Force, etc.) — clés wire inchangées
- Pas de guilde / réputation

**Hors :** respec, équipement paper-doll dupliqué (renvoyer vers UI-5).

---

## UI-10 — Login + sélection perso (DA)

**But :** mêmes champs / boutons / sécurité jeton ; fond sombre + or ; titres FRoG.

**Fait :**

- Playtest auto-login intact
- `LoginButtonForTest` etc. intactes
- Pas d’écran « art only » qui cache les champs accessibles

**Hors :** launcher, patcher, crédits.

---

## UI-11 — Options + UserSettings

**But :** remplacer le stub par un **vrai** fichier (JSON user-local) : taille fenêtre, plein écran (`FormBorderStyle` / `Bounds`), volume placeholder (0–100, no-op audio tant que pas de `SoundService` live).

**Fait :**

- Lecture au boot, écriture sur OK
- Fallback fichier manquant / JSON cassé
- Aucun setting réseau qui change hôte/port **sans** les champs login (éviter deux sources)

**Hors :** vsync GPU réel, rebind complet, qualité « 1280×720 » serveur.

**Risque 60 FPS :** resize une fois, pas par frame. Plein écran ne doit pas multiplier les `RedrawMap` hors mouvement.

---

## UI-12 — Minimap

**But :** miniature **cachée** de la carte déjà en mémoire + point joueur (et éventuellement autres `_others`).

**Fait :**

- Rebuild cache **uniquement** au `MapDataReceived` / changement de carte — pas à 16 ms
- Point joueur : petit invalidate local (bitmap minimap, pas full map)

**Hors :** ping, fog, quêtes sur minimap (le tracker UI-7 suffit).

**Risque 60 FPS :** **critique** si quelqu’un appelle `MapViewRenderer.Render` pour la mini. Interdit. Downscale d’une copie cache ou draw grille low-res dédiée.

---

## UI-13 — Hotbar + menu BD

**But :** slots 1–0 **présentation** des actions déjà là (mêlée, sort sélectionné, interact E) + boutons Perso / Inventaire / Quêtes / Options (`Toggle` overlays). « Carte » = focus monde / fermer fenêtres, pas un nouveau paquet.

**Fait :**

- Clavier 1–0 et clics appellent les **mêmes** handlers que les boutons actuels
- Menu n’empile pas 5 fenêtres sans z-order (une principale + HUD)

**Hors :** barres de sorts à 20 skills, cooldown serveur nouveau.

---

## UI-14 — Banque / sol

**But :** chrome cohérent (onglets de l’inventaire **ou** petites fenêtres), mêmes listes et boutons.

**Fait :**

- `BankItemsListForTest` / `GroundItemsListForTest` / pickup
- Pas de géolocalisation sol obligatoire

**Hors :** drag banque↔inventaire (peut être une sous-étape plus tard, mêmes `SendBank*`).

---

## UI-15 — Isolation rendu carte / budget 60 FPS

**But :** le monde reste fluide **avec** overlays ouverts. Mesurer, puis réduire le coût `RedrawMap`.

**Fait (minimum) :**

- Compteur FPS local (debug, pas stub `FpsLabel` obligatoire)
- `MapViewRenderer` : plus d’allocation full-map **par tick** si possible — dirty tiles / viewport clip / bitmap persistante + blit entités
- Overlays : `Invalidate(rect)` local
- Documenter : avant / après (même machine, même carte), pas de chiffre inventé

**Hors :** MonoGame, GPU renderer, UDP.

**Risques déjà identifiés (baseline) :**

| Risque | Preuve | Mitigation |
| --- | --- | --- |
| `new Bitmap(mapW*32, mapH*32)` chaque dirty tick | `RedrawMap` + `MapViewRenderer.Render` | UI-15 : surface persistante |
| Dispose/reassign `PictureBox.Image` 60 Hz | même chemin | blit in-place |
| Overlay qui invalide tout le Form | WinForms | host sibling, pas TransparencyKey |
| Rebuild ListBox chat/inv à 60 Hz | à éviter | event-only |
| `Application.DoEvents` dans smokes | tests only | ne pas copier dans le game loop |

Cible : sensation ~60 FPS sur carte Phase 7 typique **avec** HUD + 1 fenêtre ouverte. Pas de latence imputable au chrome (saisie chat / toggle I).

---

## Hors plan (rappels)

| Sujet | Pourquoi |
| --- | --- |
| Phase 10 / PR #8 | Chantier séparé |
| Guilde / groupe / trade P2P | Social DEFERRED ; pas de wire |
| Drag-drop inventaire | Sous-étape possible **après** UI-5, mêmes events |
| Audio réel | Dépend d’un `SoundService` live (n’existe pas) |
| Clone pixel mockup | Mandat : direction seulement |
| Merge de la PR docs | Interdit pour ce run |

---

## Critère de succès du **chantier** (plusieurs runs)

Joueur : carte dominante, HUD compact, fenêtres sombre/or cohérentes, **toutes** les actions Phase 7/8 encore possibles (shop, banque, quête, dialogue, craft, chat 3 canaux, mêlée). Serveur et protocole v10 inchangés.

Critère de succès de **ce run** : ce dossier + Draft PR docs. Voir [STATUS.md](STATUS.md).
