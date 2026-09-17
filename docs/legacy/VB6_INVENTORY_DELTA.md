# Inventaire VB6 régénéré (Task 5)

- Source : https://github.com/Alexoune001/FRoG-Creator-OSE-V0.6.3 (clone shallow local, non commité)
- Date : 2026-08-22
- Méthode : glob `.frm`/`.bas`/`.cls` (+ autres unités VB) ; routines via regex Sub/Function/Property
- CSV : `docs/legacy/VB6_INVENTORY_REGENERATED.csv`

## Totaux mesurés vs PRD §3.2

| Composant | Forms (mes./PRD) | Modules (mes./PRD) | Classes (mes./PRD) | Autres | Total unités (mes./PRD) | Routines indexées (mes./PRD) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Client | 20/20 | 14/14 | 0/0 | 0 | 34/34 | 605/58 |
| Editeur | 73/73 | 17/17 | 0/0 | 1 | 91/90 | 1173/108 |
| Serveur | 12/12 | 13/13 | 8/8 | 2 | 35/33 | 913/82 |
| **Total** | 105/105 | 44/44 | 8/8 | 3 | 160/157 | 2691/248 |

## Écarts notables

- **Client** : comptes Forms/Modules/Classes/Total alignés avec le PRD (34).
- **Editeur** : TotalUnits **+1** — le PRD compte 90 (frm+bas) ; l’extrait inclut aussi `Editeur/Forms Sources/ctlProgressBar.ctl` (UserControl).
- **Serveur** : TotalUnits **+2** — `Serveur/Forms Sources/ctlProgressBar.ctl` et `ctlSysTrayBalloon.ctl` (UserControls hors table PRD Forms/Modules/Classes).
- **Routines** : 2691 (regex déterministe) vs 248 (index CSV historique) — l’écart confirme l’avertissement PRD §3.2 : ne pas utiliser 248 comme métrique de parité.

## Notes méthodologiques

- Les « Routines » PRD (248) sont un index CSV historique incomplet ; le total regex ci-dessus est plus élevé et doit servir de base de traçabilité, pas de parité 1:1 avec 248.
- Les fichiers sous sous-dossiers (ex. dépendances) sont inclus s’ils portent une extension unité VB.
- Aucune modification du dépôt VB6 ; le clone reste hors git de MMO_Maker.
- Alignement Forms/Modules/Classes avec §3.2 : **exact** (105 / 44 / 8). Le delta d’unités vient uniquement des `.ctl`.

## 15 plus grandes unités (octets)

| Fichier | Type | Octets | Routines (regex) |
| --- | --- | ---: | ---: |
| `Editeur/Modules Sources/modGameLogic.bas` | Module | 328597 | 127 |
| `Serveur/Modules Sources/modServerTCP.bas` | Module | 246204 | 100 |
| `Client/Forms Sources/frmMirage.frm` | Form | 224544 | 126 |
| `Client/Modules Sources/modGameLogic.bas` | Module | 218044 | 58 |
| `Editeur/Forms Sources/frmMirage.frm` | Form | 211883 | 153 |
| `Serveur/Modules Sources/modGameLogic.bas` | Module | 203991 | 67 |
| `Editeur/Modules Sources/modClientTCP.bas` | Module | 112726 | 83 |
| `Serveur/Forms Sources/frmServer.frm` | Form | 109481 | 106 |
| `Client/Modules Sources/modClientTCP.bas` | Module | 85045 | 65 |
| `Editeur/Forms Sources/frmItemEditor.frm` | Form | 84643 | 40 |
| `Editeur/Forms Sources/frmMapProperties.frm` | Form | 78089 | 33 |
| `Client/Forms Sources/frmTrade.frm` | Form | 76660 | 14 |
| `Editeur/Forms Sources/frmTrade.frm` | Form | 75671 | 14 |
| `Serveur/Modules Sources/modGeneral.bas` | Module | 74142 | 18 |
| `Editeur/Modules Sources/fmod.bas` | Module | 68088 | 4 |
