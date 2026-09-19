# Signaler un bug

Une page. Remplis chaque bloc. **Aucun secret** (mot de passe, chaîne de connexion, jeton, dump `appsettings.Local.json`).

Version / build : badge client `v10.3.0` + SHA indiqué par l’opérateur (protocole **v11**). Canal = celui de l’opérateur, pas un ticket Git public avec secrets.

## Environnement

| Champ | Valeur |
| --- | --- |
| Surface | Client / Éditeur / Serveur / Ops |
| Version / build | |
| OS | |
| Personnage (pseudo) | |
| Heure (America/Toronto si possible) | |

## Reproduire

1.
2.
3.

## Attendu

…

## Obtenu

…

## Captures / logs

- Joindre PNG expurgés
- Logs : retirer secrets avant envoi

## Gravité

| Niveau | Quand l’utiliser |
| --- | --- |
| Bloquant | Impossible de jouer / perte ou duplication / crash au lancement |
| Majeur | Parcours important cassé, contournement difficile |
| Mineur | Gênant, contournement simple |
| Cosmétique | Texte, alignement |

## Bloc copier-coller (Discord / chat)

```text
Titre :
Surface : [Client|Éditeur|Serveur|Ops]
Gravité : [Bloquant|Majeur|Mineur|Cosmétique]
Version / build :
OS :
Personnage :
Heure (America/Toronto) :
Repro :
1.
2.
3.
Attendu :
Obtenu :
Captures / logs (expurgés) :
```

### Exemple fictif (sans secret)

```text
Titre : Crash au clic sur Inventaire
Surface : Client
Gravité : Majeur
Version / build : v10.3.0 / SHA fourni par l’opérateur
OS : Windows 11
Personnage : TesteurBleu
Heure (America/Toronto) : 2026-09-18 16:00
Repro :
1. Se connecter
2. Entrer en carte
3. Ouvrir Inventaire
Attendu : panneau inventaire
Obtenu : fermeture immédiate du client
Captures / logs (expurgés) : (à joindre)
```

## Et après ?

Envoie via le canal indiqué par l’opérateur. Wiki : [Bugs](https://github.com/Netsuno/MMO_Maker/wiki/Bugs)
