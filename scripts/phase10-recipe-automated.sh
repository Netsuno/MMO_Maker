#!/usr/bin/env bash
# P10-4: print + run what can be automated of the 12-step recipe.
# Does NOT fake two physical machines. Does NOT run a 30–60 min human session.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN_TESTS=1

USAGE='Usage: phase10-recipe-automated.sh [--skip-tests]

Prints the automate vs 2-physical-machine matrix, then runs the loopback
recipe test (steps 3–8 on one host). Steps 2, 5-WAN, 9-distant, 12 stay named.
'

while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-tests) RUN_TESTS=0; shift ;;
    -h|--help) printf '%s' "$USAGE"; exit 0 ;;
    *) echo "error: unknown argument: $1" >&2; exit 2 ;;
  esac
done

cat <<'EOF'
P10-4 recipe — automate vs 2 physical machines
==============================================
 1 Install client A          AUTOMATABLE  packaged-winforms-smoke.ps1 (1 Windows)
 2 Install client B          2 MACHINES   second PC / second OS
 3 Connexion A+B             AUTOMATABLE  loopback (Phase10RecipeLoopbackTests)
 4 Personnages               AUTOMATABLE  loopback create/select
 5 Présence même carte       AUTOMATABLE  loopback ; WAN / NAT = 2 MACHINES
 6 Gameplay de base          AUTOMATABLE  Phase 8 smokes from-source (pas paquet HUD)
 7 Social                    AUTOMATABLE  Phase10SocialTcpTests
 8 Échange                   AUTOMATABLE  Phase10TradeTcpTests
 9 Publish éditeur → distant 2 MACHINES   fixture PG oui ; zip+joueur distant non
10 Backup / restore          AUTOMATABLE  Phase10BackupRestoreRowsTests
11 Sanction                  AUTOMATABLE  mute/ban TCP + restore
12 Stabilité 30–60 min       2 MACHINES   durée humaine, deux clients idle

Deux machines physiques sont obligatoires pour les étapes 2, 5 (WAN), 9 (éditeur livré → joueur distant), 12.
EOF

if [[ "$RUN_TESTS" -eq 1 ]]; then
  echo "==> Phase10RecipeLoopbackTests"
  dotnet test "${ROOT}/Frog.Tests/Frog.Tests.csproj" -c Release --verbosity minimal \
    --filter "FullyQualifiedName~.Phase10RecipeLoopbackTests"
fi

echo "OK recipe matrix printed. Not a 2-PC pass. Not READY."
