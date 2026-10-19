#!/usr/bin/env sh
set -eu

if [ "$#" -lt 2 ] || [ "$#" -gt 3 ]; then
  echo 'Usage: install-brain-client.sh <published-brain-file> <sha256> [destination-directory]' >&2
  exit 2
fi

source_file=$1
expected=$2
destination=${3:-"$HOME/.local/bin"}
case "$expected" in
  *[!0-9a-fA-F]*|'') echo 'Invalid SHA-256 value.' >&2; exit 2 ;;
esac
if [ "${#expected}" -ne 64 ]; then echo 'Invalid SHA-256 length.' >&2; exit 2; fi
if [ ! -f "$source_file" ]; then echo 'Published brain file not found.' >&2; exit 2; fi
if command -v sha256sum >/dev/null 2>&1; then
  actual=$(sha256sum "$source_file" | cut -d ' ' -f 1)
else
  actual=$(shasum -a 256 "$source_file" | cut -d ' ' -f 1)
fi
if [ "$(printf '%s' "$actual" | tr '[:upper:]' '[:lower:]')" != "$(printf '%s' "$expected" | tr '[:upper:]' '[:lower:]')" ]; then
  echo 'Brain Client package checksum mismatch.' >&2
  exit 1
fi
mkdir -p "$destination"
temporary="$destination/.brain.$$"
trap 'rm -f "$temporary"' EXIT HUP INT TERM
install -m 0700 "$source_file" "$temporary"
mv -f "$temporary" "$destination/brain"
echo "Installed Brain Client at $destination/brain"
