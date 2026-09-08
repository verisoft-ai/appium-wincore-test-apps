#!/usr/bin/env bash
# Build the local ABAP 7.52 image from SAP's official archives.
#
#   1. Download "AS ABAP 7.52 SP04, ASE" — all 11 parts + the License file — from
#      https://developers.sap.com/trials-downloads.html  (free SAP account)
#   2. Drop every part into  sap/build/archives/  (keep them as .rar, do NOT extract)
#   3. Run this script.
#
# It extracts the archives into sap/build/sapdownloads/ (which must end up containing
# install.sh) and runs `docker build`.
#
# Needs 7-Zip or unrar. On Windows:  winget install --id 7zip.7zip -e
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
archives="$here/archives"
payload="$here/sapdownloads"
image="${SAP_ABAP_IMAGE:-nwabap:7.52}"

shopt -s nullglob nocaseglob
parts=("$archives"/*.rar "$archives"/*.zip "$archives"/*.exe)
shopt -u nocaseglob
if [ ${#parts[@]} -eq 0 ]; then
  echo "No archive parts in $archives/"
  echo "Put the 11 'SAP ABAP AS Part N' files (+ License) there, still as .rar."
  exit 1
fi
echo "Found ${#parts[@]} file(s) in $archives/"

# Locate an extractor: prefer 7z, then unrar, then the WinRAR/7-Zip install dirs.
SEVENZ=""
for c in 7z 7za 7z.exe; do command -v "$c" >/dev/null 2>&1 && SEVENZ="$c" && break; done
if [ -z "$SEVENZ" ]; then
  for p in "/c/Program Files/7-Zip/7z.exe" "/c/Program Files (x86)/7-Zip/7z.exe"; do
    [ -x "$p" ] && SEVENZ="$p" && break
  done
fi
UNRAR=""
command -v unrar >/dev/null 2>&1 && UNRAR="unrar"
for p in "/c/Program Files/WinRAR/UnRAR.exe" "/c/Program Files (x86)/WinRAR/UnRAR.exe"; do
  [ -z "$UNRAR" ] && [ -x "$p" ] && UNRAR="$p"
done

if [ -z "$SEVENZ" ] && [ -z "$UNRAR" ]; then
  echo "No extractor found. Install one, then re-run:"
  echo "  winget install --id 7zip.7zip -e     (then reopen the terminal)"
  exit 1
fi

# The multi-part set is opened from its FIRST volume. SAP names vary:
#   *.part01.rar / *.part1.rar / '... Part 1' (no ext) / plain first .rar
first=""
for pat in "*part01.rar" "*part1.rar" "*Part 1*" "*part 1*" "*_1.rar" "*.rar"; do
  shopt -s nullglob nocaseglob
  m=("$archives"/$pat)
  shopt -u nullglob nocaseglob
  [ ${#m[@]} -gt 0 ] && first="${m[0]}" && break
done
[ -n "$first" ] || { echo "Could not identify the first archive part in $archives/"; exit 1; }
echo "First volume: $(basename "$first")"

echo "Extracting → $payload"
rm -rf "$payload"; mkdir -p "$payload"
if [ -n "$SEVENZ" ]; then
  "$SEVENZ" x -y -o"$payload" "$first"
else
  "$UNRAR" x -o+ "$first" "$payload/"
fi

# install.sh may land one directory deep depending on the SP packaging.
if [ ! -f "$payload/install.sh" ]; then
  nested=$(find "$payload" -maxdepth 3 -name install.sh -print -quit || true)
  [ -n "$nested" ] && { echo "Flattening from $(dirname "$nested")"; mv "$(dirname "$nested")"/* "$payload/"; }
fi
[ -f "$payload/install.sh" ] || { echo "install.sh not found in $payload after extraction — inspect its contents"; exit 1; }
echo "Payload OK: $(ls "$payload" | tr '\n' ' ')"

echo "Building $image  (Docker context: $here — no .rar sent, see .dockerignore) …"
docker build -t "$image" "$here" \
  ${http_proxy:+--build-arg http_proxy=$http_proxy} \
  ${https_proxy:+--build-arg https_proxy=$https_proxy}

echo
echo "Done. Ensure sap/.env has SAP_ABAP_IMAGE=$image, then:"
echo "  docker compose -f sap/docker-compose.yml up -d"
echo "  docker compose -f sap/docker-compose.yml exec abap ./install.exp"
