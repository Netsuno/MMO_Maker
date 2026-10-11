# Commandes d’événement de carte

← [Guides](README.md) · [Quickstart auteur](CREATOR_QUICKSTART.md) · [Données de jeu](GAME_DATA.md) · [Inventaire UI](../../../EDITOR_UI_INVENTORY.md)

> Le catalogue Phase 8 [`COMMAND_CATALOG.md`](../../phase-08-quests-events-advanced-creation/COMMAND_CATALOG.md) s’arrête à `call_common_event`. Cette page est le catalogue tel que le code le lance : Core, serveur, éditeur. Hello / protocole **v11**. Les tuiles de contenu restent **48×48**.

À la fin de ce guide, chaque commande que l’éditeur sait enregistrer a un nom, des paramètres, un effet serveur et ses bornes.

## Où les poser

**Carte → Événements carte…** (`MapEventsBrowseDialog`). Une page s’édite dans `MapEventPageEditorDialog`.

- La barre **Palette** (`MapEventCommandPaletteBar`) a **34** boutons. Deux d’entre eux (**Si interrupteur**, **Si variable**) écrivent le même discriminant `branch`.
- Le combo **Commande** (`MapEventCommandParameterPanel`) liste les **45** discriminants de `MapEventCommandDiscriminators.All`, triés par l’identifiant anglais, affichés avec `MapEventEditorLabels.CommandKind`.
- La case **JSON avancé** montre le JSON et masque les champs. Le schéma reste **1**. Une propriété inconnue est refusée.
- Une page accepte au plus **64** commandes, **50** pages par événement, **32** conditions de page, **8** niveaux de branche imbriquée. Le JSON d’une commande tient dans **8192** octets UTF-8.

<!-- CAPTURE: Éditeur de page d’événement carte, barre Palette avec les boutons français (Texte, Afficher choix, Jouer BGM…), combo Commande, case JSON avancé décochée -->
*Capture à venir.*

Le libellé du **bouton** et celui du **combo** ne sont pas toujours les mêmes. Le bouton dit **Interrupteur** ; le combo, une fois la commande posée, dit **Régler interrupteur**. Les deux stockent `set_switch`.

<!-- CAPTURE: Même commande Interrupteur : bouton de palette « Interrupteur » et combo Commande affichant « Régler interrupteur » -->
*Capture à venir.*

## Ce que le serveur fait de toute façon

`MapEventCommandExecutor` enchaîne les commandes de la page. La première erreur arrête la liste (`Limite d'exécution événement atteinte.` au-delà de **256** pas). Un personnage a au plus **4** exécutions actives (`MapEventExecutionTracker`).

**Attendre** est la seule commande qui pose `Waiting` et reprend plus tard. Les autres, y compris **Texte** et **Téléportation**, ne coupent pas la liste. Plusieurs **Texte** dans le même passage : le dernier écrase `state.ShowText`.

Les interrupteurs et variables des commandes sont ceux du **personnage** (clé `[A-Za-z0-9_]`, 64 octets UTF-8). Le catalogue **Système** de [Données de jeu](GAME_DATA.md) ne les alimente pas tout seul : la clé tapée ici doit être la même.

Les fiches citées par un guid (boutique, dialogue, quête, événement commun, objet donné) doivent être **publiées**, quand le serveur consulte un catalogue. **Changer classe**, **Changer compétences** et **Changer équipement** écrivent l’identifiant même si la fiche n’est pas relue.

## Écarts connus

- **Afficher choix** est accepté et **aucune branche n’est prise**. `DialogueChoiceRequest` (opcodes 67/68) vise un dialogue du catalogue, pas les pages inline (`MapEventDeferredPresentation`).
- **Jouer BGM** et **Jouer SE** sont validés comme une piste de carte. Le client ne joue pas cet asset d’événement : il ne joue que les cues d’interface et la boucle stub.
- Le catalogue historique ne documentait pas les choix, l’audio, l’or et les objets « changer », la météo, les images, les effets d’écran, le défilement, l’animation, ni la progression (niveau, EXP, paramètres, compétences, équipement, nom, classe, récupération, PV/PM).
- **Changer EXP** ne fait pas monter de niveau. La courbe de combat (`100 × niveau²`, +1 aux stats, +10 PV/PM max) n’est pas appliquée. L’EXP reste dans la barre du niveau courant. **Changer niveau** déplace le niveau seul, puis borne l’EXP à la barre du nouveau niveau.

<!-- CAPTURE: Commande Afficher choix avec les libellés Oui / Non et l’annulation, pour montrer que les branches sont dans le formulaire alors que le serveur ne les prend pas -->
*Capture à venir.*

## Palette

Les défauts ci-dessous sont ceux de `MapEventCommandPalette.TryCreate`, avant que tu changes les champs.

### Texte

- **Bouton :** Texte. **Combo :** Texte. Discriminant `show_text`.
- **Paramètre :** Texte. Défaut `Bonjour.`
- **Serveur :** copie le texte dans `state.ShowText` et continue. Le client le reçoit avec le résultat de l’exécution.
- **Limite :** 1–512 caractères. Vide ou trop long : `show_text: texte vide ou trop long (max 512).`

### Afficher choix

- **Bouton :** Afficher choix. **Combo :** Afficher choix. `show_choices`.
- **Paramètres :** 2 à 4 libellés (défaut Oui / Non, 64 caractères chacun), annulation **Interdit**, **Choix 1**…**Choix 4** ou **Branche**, une liste de commandes par choix, et des commandes d’annulation seulement si l’annulation est **Branche**.
- **Serveur :** valide le JSON et **ne prend aucune branche**. L’exécution continue.
- **Limite :** profondeur de branche 8, comme une branche imbriquée. `show_choices: entre 2 et 4 choix.`

### Jouer BGM

- **Bouton et combo :** Jouer BGM. `play_bgm`.
- **Paramètres :** Fichier (défaut `Assets/Audio/music-loop.wav`), Volume 0–100 (défaut 100), Fondu (ms) 0–60000 (défaut 0). **Parcourir…** ouvre le navigateur audio.
- **Serveur :** valide la piste et ne la joue pas côté client (voir l’écart).
- **Limite :** chemin 240 caractères au plus (`MapAudioTrack`).

### Jouer SE

- **Bouton et combo :** Jouer SE. `play_se`.
- **Paramètres :** comme BGM. Défaut `Assets/Audio/ui-click.wav`, volume 100, fondu 0.
- **Serveur :** même validation, pas de lecture de l’asset d’événement.
- **Limite :** identique à BGM.

### Ouvrir boutique

- **Bouton et combo :** Ouvrir boutique. `open_shop`.
- **Paramètres :** combo **Boutique** (boutiques publiées) et **Identifiant**. Défaut : guid vide, refusé tant qu’une boutique n’est pas choisie. Nom optionnel, 1–120 s’il est présent.
- **Serveur :** cherche la boutique **publiée**. Si elle existe, `OpenShopId` est posé. Sinon journal, et `Boutique indisponible.` si aucun texte n’est déjà prévu.
- **Limite :** guid obligatoire. L’achat lui-même n’est pas dans cette commande.

### Changer or

- **Bouton et combo :** Changer or. `change_gold`.
- **Paramètres :** Opération **Augmenter** / **Diminuer**, Montant (entier > 0, défaut 1).
- **Serveur :** réécrit en `give_gold` ou `take_gold` sans clé « une fois », puis applique l’or du personnage. **Diminuer** sous zéro : `take_gold: or insuffisant.`
- **Limite :** le montant doit être un entier strictement positif. Pas de plafond autre que l’entier 32 bits du parseur.

### Changer objets

- **Bouton et combo :** Changer objets. `change_items`.
- **Paramètres :** Objet (guid, vide refusé), Opération **Augmenter** / **Diminuer**, Quantité (1–9999 dans le champ, entier > 0 à la validation).
- **Serveur :** réécrit en `give_item` ou `take_item`. Donner passe par l’inventaire. Retirer sans assez d’objets : `take_item: quantité insuffisante.`
- **Limite :** guid d’objet obligatoire. La fiche n’est pas relue ici : l’inventaire reçoit l’identifiant.

### Interrupteur

- **Bouton :** Interrupteur. **Combo :** Régler interrupteur. `set_switch`.
- **Paramètres :** Interrupteur (défaut `interrupteur_1`), case **oui** (défaut cochée).
- **Serveur :** `SetSwitchAsync` sur le personnage, puis copie dans la charge utile de session.
- **Limite :** clé non vide, `[A-Za-z0-9_]`, 64 octets UTF-8. Ce n’est pas le libellé français du catalogue Système, c’est cette clé.

### Variable =

- **Bouton :** Variable =. **Combo :** Régler variable. `set_variable`.
- **Paramètres :** Variable (défaut `variable_1`), Valeur (entier, le champ va de `int.MinValue` à `int.MaxValue`, défaut 0).
- **Serveur :** `SetVariableAsync` sur le personnage.
- **Limite :** même règle de clé que l’interrupteur.

### Variable +

- **Bouton :** Variable +. **Combo :** Ajouter à la variable. `add_variable`.
- **Paramètres :** Variable (défaut `variable_1`), Delta (0 … entier max dans le champ, défaut 1).
- **Serveur :** `AddVariableAsync` avec ce delta.
- **Limite :** 0 est accepté (la variable ne bouge pas). Clé comme ci-dessus.

### Variable −

- **Bouton :** Variable −. **Combo :** Retirer de la variable. `sub_variable`.
- **Paramètres :** Variable, Delta ≥ 0 (défaut 1).
- **Serveur :** ajoute **l’opposé** du delta (`AddVariableAsync` avec `-delta`).
- **Limite :** le champ ne prend pas un delta négatif : le signe est celui de la commande.

### Branche

- **Boutons :** **Si interrupteur** et **Si variable**. **Combo :** Branche. Discriminant unique `branch`.
- **Si interrupteur** crée une condition interrupteur `interrupteur_1` = oui, avec Alors / Sinon vides.
- **Si variable** crée une condition variable `variable_1` ≥ 1.
- **Paramètres du formulaire :** **Si** (sept types), **Alors**, **Sinon**. Types : Interrupteur, Variable (`= ≠ < ≤ > ≥`), Quête (`pas commencée`, `en cours`, `prête à rendre`, `terminée`), Objet (quantité), Niveau (minimum), Métier (niveau minimum), Carte ou région.
- **Serveur :** évalue la condition du personnage (quête, inventaire, niveau, métier, carte ou région publiée) et exécute Alors ou Sinon. Une région est lue dans le catalogue publié.
- **Limite :** profondeur 8 (`Profondeur de branche maximale dépassée.`). Les commandes imbriquées comptent dans les 256 pas.

<!-- CAPTURE: Commande Branche, panneau Si avec le type Interrupteur, listes Alors et Sinon vides -->
*Capture à venir.*

### Changer météo

- **Bouton et combo :** Changer météo. `set_weather`.
- **Paramètre :** Météo **Clair**, **Pluie** ou **Brouillard** (stocké `clear`, `rain`, `fog`). Défaut Clair.
- **Serveur :** pose l’override de météo de la session. Un kind inconnu dans un snapshot déjà commis est journalisé et ignoré ; un JSON illisible reste une erreur. Un téléport qui change de carte peut retirer cet override.
- **Limite :** seulement ces trois kinds. Hello reste **11**.

### Afficher image

- **Bouton et combo :** Afficher image. `show_picture`.
- **Paramètres :** Numéro 1–100 (défaut 1), Fichier (défaut `Assets/Pictures/placeholder.png`, 180 caractères), X et Y de −9999 à 9999, Opacité 0–255 (défaut 255), Synthèse **Normal**, **Addition** ou **Soustraction**.
- **Serveur :** enregistre l’opération image sur la session. Le client l’applique (`MainShellForm.ApplyOnePicture`). Pas de nouvel opcode.
- **Limite :** numéro hors 1–100 refusé. Hello **11**.

### Déplacer image

- **Bouton et combo :** Déplacer image. `move_picture`.
- **Paramètres :** Numéro, X, Y, Opacité, Synthèse. Pas de durée dans le formulaire.
- **Serveur :** même chemin image, kind déplacement.
- **Limite :** numéro 1–100, coordonnées −9999…9999, opacité 0–255.

### Teinter image

- **Bouton et combo :** Teinter image. `tint_picture`.
- **Paramètres :** Numéro, Rouge, Vert, Bleu, Opacité (0–255). Défauts de teinte : 0, 0, 64, opacité 128. Pas de durée dans le formulaire.
- **Serveur :** enregistre la teinte de cette image.
- **Limite :** canaux 0–255.

### Effacer image

- **Bouton et combo :** Effacer image. `erase_picture`.
- **Paramètre :** Numéro 1–100.
- **Serveur :** retire cette image de la session.
- **Limite :** numéro seulement.

<!-- CAPTURE: Commande Afficher image, numéro, chemin placeholder.png, X Y, opacité et synthèse Normal -->
*Capture à venir.*

### Fondu en fermeture

- **Bouton et combo :** Fondu en fermeture. `fadeout_screen`.
- **Paramètre :** Durée (ms), 0–60000, défaut 1000.
- **Serveur :** pose l’état d’écran fondu fermé. Le noir déjà posé reste la référence des effets passagers.
- **Limite :** durée dans cette plage. Hello **11**.

### Fondu en ouverture

- **Bouton et combo :** Fondu en ouverture. `fadein_screen`.
- **Paramètre :** Durée (ms), 0–60000, défaut 1000.
- **Serveur :** pose l’état d’écran fondu ouvert.
- **Limite :** même plage de durée.

### Teinte écran

- **Bouton et combo :** Teinte écran. `tint_screen`.
- **Paramètres :** Rouge, Vert, Bleu, Opacité (0–255), Durée (ms) 0–60000. Défauts 0, 0, 64, opacité 128, durée 1000.
- **Serveur :** pose la teinte d’écran (état durable).
- **Limite :** canaux 0–255.

### Tremblement écran

- **Bouton et combo :** Tremblement écran. `shake_screen`.
- **Paramètres :** Puissance 0–48 px (une tuile de contenu, `MapEventScreen.MaxPower`), Vitesse 1–9, Durée (ms) 0–60000. Défauts 8, 5 et 1000.
- **Serveur :** effet passager. À la fin, le noir, la teinte et le décalage de carte déjà posés ne changent pas.
- **Limite :** la puissance 48 est l’amplitude en pixels, pas une seconde taille de tuile de carte. La tuile de contenu reste **48×48**.

### Flash écran

- **Bouton et combo :** Flash écran. `flash_screen`.
- **Paramètres :** Rouge, Vert, Bleu, Opacité, Durée. Défauts 255, 255, 255, opacité 170, durée 1000.
- **Serveur :** effet passager, même règle que le tremblement.
- **Limite :** canaux 0–255, durée 0–60000.

<!-- CAPTURE: Commandes d’écran regroupées dans le combo (Fondu en fermeture, Teinte écran, Tremblement écran, Flash écran), champs durée et canaux -->
*Capture à venir.*

### Faire défiler la carte

- **Bouton et combo :** Faire défiler la carte. `scroll_map`.
- **Paramètres :** Direction **Haut / Bas / Gauche / Droite** (défaut Bas), Distance 0–100 tuiles (défaut 1), Vitesse 1–6 (défaut 4).
- **Serveur :** pose le décalage de carte. La durée affichée se déduit comme en VX : 256 unités par tuile, 2^vitesse unités par image, 60 images/s. Ce n’est pas un déplacement du personnage.
- **Limite :** distance 0 ne déplace pas. Hello **11**.

### Afficher animation

- **Bouton et combo :** Afficher animation. `show_animation`.
- **Paramètres :** Animation **Étincelle** (1), **Soin** (2) ou **Impact** (3), Cible **Joueur** ou **Événement**, Durée (ms) 0–60000 (défaut 1000).
- **Serveur :** enregistre l’animation. L’ancre (joueur ou événement) est résolue plus tard, tuile bornée par la largeur de carte maximale. Trois animations intégrées, sans planche et sans combat.
- **Limite :** identifiant 1–3 seulement. Hello **11**.

### Changer niveau

- **Bouton et combo :** Changer niveau. `change_level`.
- **Paramètre :** Delta entier non nul, de −98 à 98 (niveaux 1 à 99). Le champ part à 1.
- **Serveur :** ajoute le delta au niveau, borne 1–99, puis borne l’EXP à la barre de ce niveau (`100 × niveau²`, 0 au niveau 99). Les stats et les PV/PM max ne bougent pas.
- **Limite :** delta 0 refusé (`delta (int ≠ 0) requis.`). Pas de passage de niveau par l’EXP.

### Changer EXP

- **Bouton et combo :** Changer EXP. `change_exp`.
- **Paramètre :** Delta non nul, ±1 000 000. Le champ part à 10.
- **Serveur :** ajoute à l’EXP **du niveau courant** et borne à la barre. Le niveau ne change pas. Un surplus est coupé à `100 × niveau²`. Au niveau 99 l’EXP reste 0.
- **Limite :** ce n’est pas `ProgressionCurve.ApplyExperience`. Pour faire monter le niveau, utilise **Changer niveau**.

### Changer paramètre

- **Bouton et combo :** Changer paramètre. `change_param`.
- **Paramètres :** Paramètre et Delta ±9999 (défaut +1). Libellés : STR, AGI, DEX, INT, VIT, LUCK, HP, **HP max**, MP, **MP max**.
- **Serveur :** STR…LUCK bornés 1–99. HP courant borné 0…PV max. PM courant borné 0…PM max. HP max borné 1–9999 (le PV courant est ramené s’il dépasse). MP max borné 0–9999.
- **Limite :** delta 0 refusé. Les six caractéristiques ne descendent pas sous 1.

### Changer compétences

- **Bouton et combo :** Changer compétences. `change_skills`.
- **Paramètres :** Opération **Augmenter** / **Diminuer**, Compétence (guid, vide refusé).
- **Serveur :** ajoute ou retire le guid de la liste apprise (JSON sur le personnage), 64 entrées au plus. Déjà présente : aucun changement. Absente au retrait : `change_skills: compétence absente.` Liste pleine : `change_skills: liste pleine.`
- **Limite :** le serveur ne vérifie pas que le guid est une compétence publiée. Une compétence publiée peut rester lançable hors de cette liste.

### Changer équipement

- **Bouton et combo :** Changer équipement. `change_equipment`.
- **Paramètres :** Opération **Équiper** / **Déséquiper**, Emplacement **Arme** / **Armure**, Objet (guid). **Déséquiper** sans guid n’envoie pas `itemId`.
- **Serveur :** écrit l’arme ou l’armure portée et aligne l’inventaire (`AlignEquippedAsync`). Pas de nouvel emplacement paperdoll. Équiper le même objet : aucun changement.
- **Limite :** `equip` exige un guid. Le type d’objet (arme contre armure) n’est pas revérifié ici.

### Changer nom

- **Bouton et combo :** Changer nom. `change_name`.
- **Paramètre :** Nom. Défaut proposé `Héros`.
- **Serveur :** remplace le nom affiché du personnage qui lance l’événement. Même nom : aucun changement. Nom déjà pris sur le compte, sans tenir compte de la casse : `change_name: nom déjà utilisé.` Le niveau et les stats restent.
- **Limite :** 32 caractères, lettres, chiffres, espaces, tiret, souligné (`CharacterDisplayNameRules`). Hello **11**.

### Changer classe

- **Bouton et combo :** Changer classe. `change_class`.
- **Paramètre :** Classe (guid, vide refusé).
- **Serveur :** écrit `ClassId` sur le personnage. Même classe : aucun changement. Les PV, PM, stats et le niveau **ne sont pas** recopiés depuis la fiche de [Données de jeu](GAME_DATA.md).
- **Limite :** la création de personnage, elle, exige une classe **publiée**. Cette commande non.

### Récupération totale

- **Bouton et combo :** Récupération totale. `recover_all`.
- **Paramètre :** aucun. Le formulaire affiche `PV et PM au maximum.` Le JSON est un objet vide.
- **Serveur :** PV = max(0, PV max), PM = max(0, PM max). Le KO et les statuts ne sont pas une table à part : seuls ces deux courants bougent.
- **Limite :** un JSON qui n’est pas un objet est refusé.

### Changer PV/PM

- **Bouton et combo :** Changer PV/PM. `change_hp_mp`.
- **Paramètres :** **PV** ou **PM**, Opération **Augmenter** / **Diminuer**, Montant 1–9999 (défaut 1).
- **Serveur :** ajoute ou retire sur le courant, borné 0…maximum du même vital. Ne touche pas au maximum.
- **Limite :** montant hors 1–9999 refusé. Hello **11**.

## Dans le combo, pas sur la palette

Ces douze commandes sont dans le combo **Commande** et dans l’exécuteur. Aucun bouton de la barre Palette ne les insère. **Changer or** et **Changer objets** restent le chemin palette ; Donner / Retirer sont le discriminant après réécriture, et aussi un choix direct du combo.

### Dialogue

- **Combo :** Dialogue. `start_dialogue`. Pas de bouton.
- **Paramètre :** Dialogue (guid).
- **Serveur :** ouvre une session de dialogue **publié**. Introuvable : `Dialogue introuvable.` Sinon pousse l’état de dialogue (révision, jeton, texte, choix) et, si aucun texte n’est déjà posé, le résumé locuteur + texte.
- **Limite :** ce n’est pas **Afficher choix**. Les choix de ce dialogue passent par le catalogue de dialogues.

### Donner objet

- **Combo :** Donner objet. `give_item`.
- **Paramètres :** Objet (guid), Quantité (entier > 0 ; le champ va de 1 à 9999), Clé une fois (optionnelle, 1–64 caractères).
- **Serveur :** ajoute à l’inventaire. Si la clé est remplie, un interrupteur dérivé empêche un second don : le second passage ne fait rien. Échec d’ajout : l’interrupteur est relâché et le message d’inventaire (ou `give_item échoué.`) remonte.
- **Limite :** **Changer objets / Augmenter** réécrit cette commande **sans** clé une fois.

### Retirer objet

- **Combo :** Retirer objet. `take_item`.
- **Paramètres :** Objet, Quantité, Clé une fois (lue, le retrait ne s’en sert pas comme le don).
- **Serveur :** retire sur les piles de cet objet. Pas assez : `take_item: quantité insuffisante.`
- **Limite :** quantité entière > 0.

### Donner or

- **Combo :** Donner or. `give_gold`.
- **Paramètres :** Montant (entier > 0 ; le champ part à 10), Clé une fois optionnelle (1–64).
- **Serveur :** ajoute l’or. Avec une clé, un second passage ne redonne rien. Personnage absent après la prise de clé : la clé est rendue et `Personnage introuvable.`
- **Limite :** **Changer or / Augmenter** réécrit ce don sans clé.

### Retirer or

- **Combo :** Retirer or. `take_gold`.
- **Paramètres :** Montant > 0, clé une fois (non utilisée par le retrait).
- **Serveur :** soustrait. Sous zéro : `take_gold: or insuffisant.` et l’or ne change pas.
- **Limite :** pas de solde négatif.

### Démarrer quête

- **Combo :** Démarrer quête. `start_quest`.
- **Paramètre :** Quête (guid publié).
- **Serveur :** `TryStartQuestAsync`. Échec ou quête inconnue : `Quête introuvable ou transition invalide.` Succès : le résumé devient le texte affiché s’il n’y en a pas déjà un.
- **Limite :** la quête vient du contenu Phase 8 publié, pas d’un champ de Données de jeu.

### Avancer quête

- **Combo :** Avancer quête. `advance_quest`.
- **Paramètres :** Quête (guid), Étape (entier ≥ 0, le champ va jusqu’à 99).
- **Serveur :** `TryAdvanceQuestAsync` vers cette étape. Transition impossible : même message d’échec que le démarrage.
- **Limite :** l’étape doit exister sur la quête publiée.

### Rendre quête

- **Combo :** Rendre quête. `turn_in_quest`.
- **Paramètre :** Quête (guid).
- **Serveur :** `TryTurnInQuestAsync` avec un nouvel identifiant d’opération. Pas prête ou échec : le message du service, ou `Turn-in quête invalide.` Succès : inventaire et or marqués changés (récompenses de la quête).
- **Limite :** une quête non prête ne se rend pas.

### Téléportation

- **Combo :** Téléportation. `teleport`. Pas de bouton de palette (le warp de carte est un autre outil).
- **Paramètres :** Carte (entier ≥ 1 dans le champ), Tuile X et Tuile Y (0–9999 dans le champ).
- **Serveur :** `MovementService.TryTeleportToTile`. Échec : `teleport échoué.` ou le message du déplacement. La liste de commandes **continue**. Un changement de carte peut retirer l’override de météo.
- **Limite :** la carte doit exister pour le mouvement. Hello **11**.

### Attendre

- **Combo :** Attendre. `wait`.
- **Paramètre :** Durée (ms), 0–60000, défaut 500.
- **Serveur :** pose `Waiting` et `WaitUntilUtc`, puis reprend les commandes suivantes après le délai. C’est la seule pause de l’interpréteur.
- **Limite :** `wait: milliseconds (0–60000) requis.` 0 est valide.

### Événement commun

- **Combo :** Événement commun. `call_common_event`.
- **Paramètres :** Événement commun (guid) **ou** Alias éditeur (entier). L’un des deux est obligatoire.
- **Serveur :** charge l’événement commun **publié**, choisit la page dont les conditions passent, exécute ses commandes. Inconnu ou sans page : `Événement commun introuvable.` Aucune page ne passe : `Aucune page active pour cet événement commun.` Rappel du même id : `Cycle common-event détecté.`
- **Limite :** profondeur **4** (`Profondeur call_common_event dépassée.`), distincte de la profondeur 8 des branches. Les pas comptent dans les 256.

### Apprendre métier

- **Combo :** Apprendre métier. `learn_profession`.
- **Paramètre :** Métier (guid).
- **Serveur :** apprend le métier publié au personnage. Le message du service devient le texte affiché s’il n’y en a pas. Échec : le message du service.
- **Limite :** le métier vient du contenu Phase 8 publié. Hello **11**.

## Conditions de page

Les conditions **de la page** (liste au-dessus des commandes) ne sont pas des commandes. Toutes doivent être vraies pour que la page parte (`MapEventEditorLabels` les relie par « et »). Les types sont les mêmes que le **Si** d’une branche. Déclencheurs de page : **Action**, **Contact joueur**, **Automatique**, **Parallèle**. Déplacement : **Fixe** ou **Trajet**.

## Tu es prêt si…

- [ ] La palette a 34 boutons, et **Si interrupteur** comme **Si variable** donnent une Branche
- [ ] Le combo **Commande** contient aussi Dialogue, Donner / Retirer objet, Donner / Retirer or, les trois quêtes, Téléportation, Attendre, Événement commun, Apprendre métier
- [ ] **Afficher choix** ne lance pas la branche du choix en jeu
- [ ] **Jouer BGM** ne lance pas le fichier sur le client
- [ ] **Changer EXP** ne fait pas gagner de niveau
- [ ] Un interrupteur d’événement utilise la clé du personnage, pas seulement le libellé de **Système**
- [ ] Hello reste **11**

## Et après ?

- [Données de jeu](GAME_DATA.md) — boutiques, objets, classes, compétences, interrupteurs nommés
- [Quickstart auteur](CREATOR_QUICKSTART.md)
- [Catalogue Phase 8](../../phase-08-quests-events-advanced-creation/COMMAND_CATALOG.md) — s’arrête à `call_common_event`
