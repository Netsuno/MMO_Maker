#!/usr/bin/env bash
# Compare a local VERSION file to a remote/stub VERSION file.
# Exit: 0 current, 1 update available, 2 update required, 3 incompatible protocol, 4 usage/parse.
set -euo pipefail

usage() {
  echo "usage: $0 [--local PATH] --remote PATH" >&2
  echo "  local defaults to docs/progress/maintenance-launcher/VERSION" >&2
  echo "  remote is a VERSION file (stub for a future HTTP endpoint)" >&2
}

LOCAL=""
REMOTE=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --local)
      LOCAL="${2:-}"
      shift 2
      ;;
    --remote)
      REMOTE="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 4
      ;;
    *)
      usage
      exit 4
      ;;
  esac
done

if [[ -z "${REMOTE}" ]]; then
  usage
  exit 4
fi

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
if [[ -z "${LOCAL}" ]]; then
  LOCAL="${ROOT}/docs/progress/maintenance-launcher/VERSION"
fi

if [[ ! -f "${LOCAL}" || ! -f "${REMOTE}" ]]; then
  echo "VERSION file missing (local=${LOCAL} remote=${REMOTE})" >&2
  exit 4
fi

read_key() {
  local file="$1"
  local key="$2"
  awk -F= -v k="$key" '
    $0 ~ /^[[:space:]]*#/ { next }
    $1 == k { gsub(/[[:space:]]/, "", $2); print $2; exit }
  ' "${file}"
}

ver_lt() {
  # numeric dotted compare: returns 0 if $1 < $2
  local IFS=.
  local -a a b
  read -r -a a <<< "$1"
  read -r -a b <<< "$2"
  local i n
  n=${#a[@]}
  if [[ ${#b[@]} -gt $n ]]; then
    n=${#b[@]}
  fi
  for ((i = 0; i < n; i++)); do
    local av="${a[i]:-0}"
    local bv="${b[i]:-0}"
    av="${av%%[^0-9]*}"
    bv="${bv%%[^0-9]*}"
    av="${av:-0}"
    bv="${bv:-0}"
    if ((10#${av} < 10#${bv})); then
      return 0
    fi
    if ((10#${av} > 10#${bv})); then
      return 1
    fi
  done
  return 1
}

LOCAL_PRODUCT="$(read_key "${LOCAL}" product)"
LOCAL_PROTOCOL="$(read_key "${LOCAL}" protocol)"
REMOTE_PRODUCT="$(read_key "${REMOTE}" product)"
REMOTE_PROTOCOL="$(read_key "${REMOTE}" protocol)"
REMOTE_MIN="$(read_key "${REMOTE}" minClient)"
REMOTE_MIN="${REMOTE_MIN:-${REMOTE_PRODUCT}}"

if [[ -z "${LOCAL_PRODUCT}" || -z "${LOCAL_PROTOCOL}" || -z "${REMOTE_PRODUCT}" || -z "${REMOTE_PROTOCOL}" ]]; then
  echo "VERSION requires product= and protocol=" >&2
  exit 4
fi

if [[ "${LOCAL_PROTOCOL}" != "${REMOTE_PROTOCOL}" ]]; then
  echo "incompatible-protocol local=${LOCAL_PROTOCOL} remote=${REMOTE_PROTOCOL}"
  exit 3
fi

if ver_lt "${LOCAL_PRODUCT}" "${REMOTE_MIN}"; then
  echo "update-required local=${LOCAL_PRODUCT} min=${REMOTE_MIN}"
  exit 2
fi

if ver_lt "${LOCAL_PRODUCT}" "${REMOTE_PRODUCT}"; then
  echo "update-available local=${LOCAL_PRODUCT} remote=${REMOTE_PRODUCT}"
  exit 1
fi

echo "current local=${LOCAL_PRODUCT} protocol=${LOCAL_PROTOCOL}"
exit 0
