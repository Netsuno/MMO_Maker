# Modèle de signalement de bug

> **Statut : BROUILLON P10-0.** À utiliser pendant la bêta ; adapter si le canal change.

## Avant d’ouvrir

1. Version / build affichée (si disponible).
2. Heure (America/Toronto si possible) + personnage.
3. Reproduire une fois si sûr.
4. **Expurger** secrets : pas de DSN, mot de passe, jeton, `appsettings.Local.json`.

## Modèle

```text
Titre court :
Surface : [Client | Éditeur | Serveur | Ops]
Gravité : [Bloquant parcours | Majeur | Mineur | Cosmétique]
Version / build :
OS :
Étapes pour reproduire :
1.
2.
3.
Attendu :
Obtenu :
Fréquence : [Toujours | Parfois | Une fois]
Captures / logs (expurgés) :
```

## Gravité

| Niveau | Exemple |
| --- | --- |
| Bloquant | Connexion impossible, perte/duplication d’objets, crash au lancement |
| Majeur | Quête / trade / social inutilisable sur un parcours prévu |
| Mineur | UI trompeuse, contournement existant |
| Cosmétique | Typo, alignement |

Wiki : [Bugs](https://github.com/Netsuno/MMO_Maker/wiki/Bugs)
