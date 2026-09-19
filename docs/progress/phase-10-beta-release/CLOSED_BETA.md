# Bêta fermée + OpsCli (P10-5 lot C)

## Registration:Mode

| Valeur | TCP `RegisterRequest` | Usage |
| --- | --- | --- |
| `Open` | accepté (défaut local / tests) | dev |
| `InviteOnly` | **refusé** | **Jalon** : pas de jetons d'invitation ; même refus que ProvisionedOnly jusqu'à implémentation des invites |
| `ProvisionedOnly` | **refusé** (`Inscriptions fermees.`) | **profil bêta** |

`appsettings.json` reste `Open` pour ne pas casser les smokes. `appsettings.Local.json.example` pose `ProvisionedOnly`.

Create via `tools/Frog.OpsCli` **n'accorde jamais** GM (`auth.operators`).

## OpsCli

```
dotnet run --project tools/Frog.OpsCli -- create user password123
dotnet run --project tools/Frog.OpsCli -- reset-password user
dotnet run --project tools/Frog.OpsCli -- session-revoke user
dotnet run --project tools/Frog.OpsCli -- operator grant user --by bootstrap
dotnet run --project tools/Frog.OpsCli -- operator revoke user
dotnet run --project tools/Frog.OpsCli -- sanction mute user --actor gm --reason spam
```

Chaîne : `FROG_POSTGRES_CONNECTION_STRING` ou `--connection-string`. `UpdatePasswordAsync` ne requiert pas l'ancien mot de passe ; reset révoque les sessions.
