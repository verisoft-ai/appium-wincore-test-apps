#!/usr/bin/env bash
# Build a WSL2 5.15 kernel bzImage — SAP ASE 16.0 SP03 SIGSEGVs in dsinit on the
# stock WSL2 6.x kernel (see sap/README.md step 4).
#
#   bash sap/build/build-wsl-kernel.sh  "C:\Users\<you>\wsl-kernel"
#
# Then point .wslconfig at the produced bzImage:
#   [wsl2]
#   kernel=C:\\Users\\<you>\\wsl-kernel\\bzImage-5.15.167.4
# ...and `wsl --shutdown`, restart Docker Desktop.
set -euo pipefail

TAG="${SAP_WSL_KERNEL_TAG:-linux-msft-wsl-5.15.167.4}"
OUT_DIR="${1:-}"
[ -n "$OUT_DIR" ] || { echo "usage: $0 <windows output dir, e.g. C:\\Users\\you\\wsl-kernel>"; exit 1; }
NAME="bzImage-${TAG#linux-msft-wsl-}"

command -v docker >/dev/null || { echo "docker required"; exit 1; }

echo "Building $TAG in a throwaway debian:12 container (~15 min) …"
docker rm -f sap-kbuild >/dev/null 2>&1 || true
docker run -d --name sap-kbuild debian:12 sleep infinity >/dev/null
trap 'docker rm -f sap-kbuild >/dev/null 2>&1 || true' EXIT

docker exec sap-kbuild bash -c '
  set -e
  apt-get update -qq
  apt-get install -y -qq build-essential flex bison bc libssl-dev libelf-dev dwarves cpio python3 git >/dev/null'

docker exec sap-kbuild bash -lc "
  set -e
  cd /root
  git clone --depth 1 -b $TAG https://github.com/microsoft/WSL2-Linux-Kernel.git k
  cd k
  cp Microsoft/config-wsl .config
  make olddefconfig >/dev/null
  make -j\$(nproc) bzImage"

mkdir -p "$OUT_DIR" 2>/dev/null || true
docker cp sap-kbuild:/root/k/arch/x86/boot/bzImage "$OUT_DIR/$NAME"
echo
echo "→ $OUT_DIR/$NAME"
echo "Add to .wslconfig:  kernel=$(printf '%s' "$OUT_DIR/$NAME" | sed 's/\\\\/\\\\\\\\/g')"
