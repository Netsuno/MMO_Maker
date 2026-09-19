#!/usr/bin/env bash
# P10-4: run the automatable 12-step recipe on one loopback host.
# Does not claim two physical machines, WAN, or a 30-60 min human session.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet test "${ROOT}/Frog.Tests/Frog.Tests.csproj" -c Release \
  --filter "FullyQualifiedName~.Phase10RecipeLoopbackTests" \
  --verbosity normal
echo "P10-4 loopback recipe tests OK. Steps 2 / 5-WAN / 9-paquet-distant / 12 still need 2 PCs."
