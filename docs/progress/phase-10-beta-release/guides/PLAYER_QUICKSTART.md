# Quickstart joueur

À la fin de ce guide, tu es connecté, tu as un personnage, et tu peux te déplacer, chatter, grouper et échanger.

Client = **Windows 11 x64**. Playtest GUI *not proven on Linux agents* (hors gate).

**Bêta fermée :** tu reçois un compte **provisionné** (pas d’inscription ouverte). Jamais un mot de passe base de données.

> Smoke `--smoke-launch` Windows CI = démarrage du shell, **pas** une partie. Recette 2 PCs : **accepted by owner Marc Giroux on 2026-09-19** (non rejouée dans cette PR).

## Prérequis

- Archive `client-win-x64.zip` (même génération protocole **v11** que le serveur)
- Identifiants envoyés par l’opérateur
- Adresse / port du serveur (et consigne TLS si fournie)
- Connexion réseau vers le serveur

## 1. Installer / lancer

1. Dézipper `client-win-x64.zip` **hors** de tout dépôt git.
2. Double-cliquer `Frog.Client.exe` (runtime bundlé, **pas** de SDK).
3. Windows peut afficher SmartScreen : binaire **non signé** — demandé par l’opérateur, pas un store.

Preuve CI de démarrage (pas une partie) :

```powershell
./scripts/packaged-winforms-smoke.ps1
# équivalent manuel après extraction hors repo :
#   Frog.Client.exe --smoke-launch
```

CI smoke : [35403209506](https://github.com/Netsuno/MMO_Maker/actions/runs/35403209506) SUCCESS.

<!-- CAPTURE: assets/joueur-01-connexion.png -->
*Capture à venir : écran de connexion — hôte, identifiant, mot de passe, bouton Connecter / Login.*

## 2. Se connecter

1. Saisir l’**adresse** et le **port** fournis (défaut local `127.0.0.1:6000` seulement si l’opérateur le dit).
2. **Connecter**, puis **Login** avec le compte provisionné.
3. Messages possibles (honnêtes) : serveur indisponible, version incompatible (client v10 vs serveur v11), mauvais identifiants, compte banni, certificat TLS refusé.

Pas d’inscription si le serveur est en `ProvisionedOnly` (profil bêta).

## 3. Personnage

Sur l’écran personnages : **Liste persos**, **Créer perso** (nom court), **Entrer dans le jeu**.

<!-- CAPTURE: assets/joueur-02-perso.png -->
*Capture à venir : liste des personnages — case, nom, bouton Entrer dans le jeu.*

## 4. Premiers pas en carte

- Déplacement : preset **AZERTY ZQSD+E** ou **QWERTY WASD+E**, plus flèches. Rebind dans **Options** (persisté).
- **F1** / bouton **Aide** : aide FR scrollable.
- Chat : onglet Chat — canaux Global / Map / Whisper / Party / Guild selon tes appartenances.
- Inventaire, banque, quêtes, craft : onglets Gameplay / Quêtes. Craft = **noms** de recettes (pas un Guid comme UI normale).
- Version visible `v10.3.0` ; **Copier diagnostics** expurgé (jamais mot de passe / jeton).

<!-- CAPTURE: assets/joueur-03-hud.png -->
*Capture à venir : vue en jeu — personnage, chat, barres de statut.*

## 5. Social et échange (slash)

Saisis dans le chat (libellés exacts = ceux de ton build) :

| Commande | Effet |
| --- | --- |
| `/party` … | Inviter / accepter / quitter un groupe (max 5, expire 60 s) |
| `/guild` … | Guilde persistée (cap 50, rôles chef/officier/membre) |
| `/friend` … | Amitié consentie |
| `/block` … | Blocage : coupe whisper + invites sociales **et** d’échange |
| `/trade` | Ouvre / invite un échange (distance 3 tuiles, même carte) |

Un redémarrage serveur **dissout les groupes** (temporaires). Guildes / amis / blocages survivent.

Échange : les deux voient la même révision ; modifier l’offre annule les confirms ; commit = une transaction serveur.

## 6. Options persistées

**Options** : fenêtre, volume 0–100, disposition clavier. Fichier `%LocalAppData%\Frog\client-settings.json` (atomique).

## Tu es prêt si…

- [ ] Tu vois ton personnage sur la carte
- [ ] Tu peux te déplacer
- [ ] Tu peux ouvrir le chat
- [ ] Aide (F1) et Options s’ouvrent
- [ ] Un problème se signale avec [BUG_REPORT_TEMPLATE](BUG_REPORT_TEMPLATE.md) **sans secret**

## Et après ?

- [Problèmes connus](../KNOWN_ISSUES.md)
- [Périmètre bêta](../BETA_SCOPE.md)
- Wiki : [Joueur](https://github.com/Netsuno/MMO_Maker/wiki/Joueur)
