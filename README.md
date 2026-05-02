# 🐸 FRoG Creator (Modern C# Edition.)

Projet de modernisation complète du **FRoG Creator OSE v0.6.3** (VB6) vers **C# / .NET 8**, en conservant la logique d’origine tout en modernisant l’architecture, les outils et la base de données.

---

## 🎯 Objectifs

- Migrer le moteur **VB6** (Client, Serveur, Éditeur) vers une base **C# .NET 8 (WinForms)**.
- Unifier la logique commune dans un projet central `Frog.Core`.
- Moderniser la communication réseau (TCP/UDP asynchrone).
- Sauvegarder les données dans une **base PostgreSQL**.
- Rendre l’éditeur compatible avec les formats d’origine tout en préparant l’extension du moteur.

---

## 🧱 Structure du projet

| Projet | Description |
|--------|--------------|
| **Frog.Core** | Contient les modèles partagés, les enums, les interfaces, et les sérialiseurs binaires (maps, items, NPCs…). |
| **Frog.Client** | Client du jeu : affichage des cartes, entités, dialogues, HUD, etc. |
| **Frog.Editor** | Éditeur de cartes et de ressources, inspiré du FRoG Creator original. |
| **Frog.Server** | Serveur multijoueur, gestion des sessions, des cartes et de la persistance PostgreSQL. |
| **Frog.Tests** | Tests unitaires et validation de compatibilité entre les modules. |

---

## 🔧 Technologies principales

- **.NET 8.0 / C# 12**
- **WinForms** pour les outils Client et Éditeur
- **PostgreSQL** pour la base de données
- **Async TCP/UDP** pour le réseau
- **Sérialisation binaire** (format compatible VB6)
- **Arborescence claire** pour séparer logique, UI et données

---

| Module              | Statut                    | Détails (seulement ce qui existe vraiment)                                                                                                                                                                                                                                        |
| ------------------- | ------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 🧩 **Frog.Core**    | 🟢 **Structure en place** | - Architecture Core fonctionnelle<br>- Enum `TileType` mis à jour (`Resource = 7`)<br>- Interface `ITileAttribute` ajoutée<br>- Attributs implémentés : `BlockAttribute`, `WarpAttribute`, `ResourceAttribute`<br>- Mise à jour de `Tile.cs` pour supporter une liste d’attributs |
| 🗺️ **Frog.Editor** | 🟠 **En cours**           | - Base de l’éditeur WinForms présente<br>- Début de l’intégration du système d’attributs                                                                                                                                                                                          |
| 🎮 **Frog.Client**  | 🔵 **Base en place**      | - Projet client fonctionnel et compilable<br>- Initialisation de la structure WinForms<br>- Squelette du rendu des cartes préparé                                                                                                                                                 |
| 🖥️ **Frog.Server** | 🔵 **Base en place**      | - Projet serveur fonctionnel<br>- Démarrage serveur déjà opérationnel<br>- Système de logs (`GameServerLogs.cs`) implémenté<br>- Première structure réseau créée                                                                                                                  |
| 🧪 **Tests**        | ⚙️ **Structure prête**    | - Projet Tests présent (vide pour le moment)                                                                                                                                                                                                                                      |


---

# 🧠 Étapes à venir (Roadmap)

## 🧩 Frog.Core
- [ ] Implémenter MapSerializerV2 (Block / Warp / Resource)
- [ ] Ajouter Map.Validate()
- [ ] Support futur pour d’autres attributs (Door, NpcSpawn, zones…)
- [ ] Gestion améliorée des erreurs / validations

## 🗺️ Frog.Editor
- [ ] Compléter la palette d’attributs (Block / Warp / Resource)
- [ ] Ajouter l’overlay visuel des attributs
- [ ] Intégrer la sérialisation MapSerializerV2
- [ ] Outil gomme pour retirer des attributs
- [ ] Outils avancés : rectangle, copier/coller, bucket fill
- [ ] Fenêtre “Propriétés de la carte”
- [ ] Gestion des tilesets (sélection / multi-tilesets)
- [ ] Système Undo/Redo

## 🎮 Frog.Client
- [ ] Lecture des maps via MapSerializerV2
- [ ] Rendu visuel final des tiles
- [ ] Prise en charge du Block (collision)
- [ ] Support du Warp (téléportation)
- [ ] Mise en place du moteur d’entités
- [ ] HUD minimal (vie, mana, nom du joueur)

## 🖥️ Frog.Server
- [ ] Chargement/sauvegarde des maps dans PostgreSQL
- [ ] Envoi d’une map au client
- [ ] Gestion des sessions joueur
- [ ] Mise en place du protocole TCP/UDP
- [ ] Synchronisation joueur → client (position, actions)
- [ ] Logging réseau complet

## 🧪 Tests
- [ ] Tests unitaires pour MapSerializerV2
- [ ] Tests des attributs (Block / Warp / Resource)
- [ ] Tests de validation des tiles
- [ ] Tests de connexion client ↔ serveur minimal


---

## 💬 Crédits & Origine

Basé sur le projet open-source **FRoG Creator OSE v0.6.3** :  
👉 [https://github.com/Alexoune001/FRoG-Creator-OSE-V0.6.3](https://github.com/Alexoune001/FRoG-Creator-OSE-V0.6.3)

Modernisé et réorganisé par **Netsun**,
pour la planification, l’analyse et la migration technique.

---

## 📜 Licence

Projet sous licence **MIT**, libre d’utilisation et de modification.
