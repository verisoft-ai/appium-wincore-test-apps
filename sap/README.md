# Local SAP backend for the driver's SAP GUI Scripting subsystem

A throwaway **SAP NetWeaver AS ABAP 7.52 SP04 Developer Edition** (SID `NPL`, SAP ASE) in
Docker — the target for `appium-desktop-driver`'s `csharp/DesktopDriverServer/Sap/`
(attach, page source, `FindById`, actions, grid/tree) and the source of the tree fixtures
under the driver's `test/fixtures/sap/`.

Light, **ASE-based — ~6 GB RAM, ~40 GB disk after install**. Not the HANA-based
`sapse/abap-cloud-developer-trial` (24 GB RAM). SAP withdraws this download on
**30 September 2026**; it is free for personal, non-productive use.

> ## ⚠️ Does not work on Docker Desktop / WSL2
>
> SAP ASE 16.0 SP03's `dataserver` (2018, built for SLES 11) **SIGSEGVs in `dsinit` /
> `Snap::Validate` during engine start** — a W^X instruction-fetch fault (`siginfo err
> 0x15`) trying to run trampoline code on a thread stack. Confirmed unfixed across:
> openSUSE Leap 15.6 / 15.3, Debian 12, **Oracle Linux 7 (glibc 2.17)**; WSL2 kernel
> 6.6 **and a hand-built 5.15**; `privileged`, `seccomp:unconfined`, `shm 4g`,
> `randomize_va_space=0`, `transparent_hugepage=never`, `execstack -s`,
> `setarch -X -R`. The WSL2 lightweight-VM layer itself appears to be the problem.
>
> **Run this in a real VM instead** — VirtualBox / Hyper-V / a cloud instance with a
> stock **Ubuntu 20.04** (or SLES/OL 7/8). The 2019–2022 community images ran the same
> trial on plain VMs. The `build/` Dockerfile + `install.exp` + `scripts/` still apply
> inside such a VM (drop the Docker layer, run `install.sh` on the host); the tree
> below documents everything learned. Or borrow a non-prod SAP login and skip the
> backend entirely — the driver subsystem (`csharp/DesktopDriverServer/Sap/`) only
> needs one live SAP session to validate against.

---

## One-time setup

### 1. WSL2 memory

`C:\Users\<you>\.wslconfig` (see `sap/wslconfig.example`):

```
[wsl2]
memory=11GB
processors=4
swap=4GB
```

Then `wsl --shutdown` and reopen Docker Desktop. 11 GB to WSL leaves ~5 GB for Windows;
`docker-compose.yml` caps the container at 10 GB.

### 2. Get the SAP archives

<https://developers.sap.com/trials-downloads.html> → sign in (free SAP account) → search
**"SAP NetWeaver AS ABAP Developer Edition 7.52 SP04"** → download **all 11 parts** plus
the **License** file (the ASE `.lic`). ~15 GB. Public mirror if the SAP links 403:
`archive.org/details/td-752-sp-04`.

Drop every file into `sap/build/archives/` — keep them as `.rar`, do **not** extract.

### 3. Build the base image

Needs 7-Zip or unrar (`winget install --id 7zip.7zip -e`).

```bash
bash sap/build/build.sh
```

Extracts the archives to `sap/build/sapdownloads/` (~14 GB), places the ASE licence where
the installer expects it, and builds `nwabap:7.52` (~330 MB — the payload is **not** baked
in, it's bind-mounted at run time).

### 4. Downgrade the WSL2 kernel to 5.15

**ASE 16.0 SP03 (built 2018 for SLES 11) SIGSEGVs in `dsinit` / `Snap::Validate` on the
stock WSL2 6.x kernel** — no combination of seccomp/caps/ASLR/sysctls fixes it. It runs
on 5.15 (what the community images used).

Build the kernel once (needs Docker + ~2 GB disk, ~15 min):

```bash
docker run -d --name kbuild debian:12 sleep infinity
docker exec kbuild bash -c 'apt-get update -qq && apt-get install -y -qq build-essential flex bison bc libssl-dev libelf-dev dwarves cpio python3 git'
docker exec kbuild bash -lc '
  cd /root && git clone --depth 1 -b linux-msft-wsl-5.15.167.4 https://github.com/microsoft/WSL2-Linux-Kernel.git k
  cd k && cp Microsoft/config-wsl .config && make olddefconfig && make -j$(nproc) bzImage'
mkdir -p ~/wsl-kernel   # a Windows path, e.g. C:\Users\<you>\wsl-kernel
docker cp kbuild:/root/k/arch/x86/boot/bzImage "C:\Users\<you>\wsl-kernel\bzImage-5.15.167.4"
docker rm -f kbuild
```

Add to `C:\Users\<you>\.wslconfig`:

```
kernel=C:\\Users\\<you>\\wsl-kernel\\bzImage-5.15.167.4
```

Then `wsl --shutdown` and restart Docker Desktop. Verify: `docker run --rm alpine uname -r`
→ `5.15.167.4-microsoft-standard-WSL2+`. (`docker info | grep Kernel` too.)

> If Docker Desktop then errors on `dockerInference` / "Inference manager": that's the
> Model Runner feature choking on a stale socket — set `"EnableDockerAI": false` in
> `%APPDATA%\Docker\settings-store.json`. The engine usually comes up regardless.

### 4b. WSL-host sysctls

Also needed, and reset on every `wsl --shutdown` (`build.sh` sets them):

```bash
wsl -d docker-desktop sysctl -w vm.max_map_count=2000000 kernel.randomize_va_space=0
```

### 5. Install

```bash
cp sap/.env.example sap/.env          # SAP_ABAP_IMAGE=nwabap:7.52
docker compose -f sap/docker-compose.yml up -d
docker compose -f sap/docker-compose.yml exec -T abap ./install.exp
```

`install.exp` runs the installer unattended (`PAGER=cat`, accepts the SAP Community
Developer licence, master password `Down1oad`). **20–40 min.** Success line:
**`Installation of NPL successful`**.

Then reclaim the payload space:

```bash
rm -rf sap/build/sapdownloads/*        # ~14 GB; the mount becomes a harmless empty dir
```

### 6. Connect SAP GUI for Windows

`C:\Windows\System32\drivers\etc\hosts` (as admin):

```
127.0.0.1 vhcalnplci
```

SAP Logon → New Connection → Custom Application Server:

| Field | Value |
|---|---|
| Description | `NPL Local` |
| Application Server | `vhcalnplci` |
| Instance Number | `00` |
| System ID | `NPL` |

Log on: client `001`, user `DEVELOPER`, password `Down1oad` (set a new one when prompted).
Client 001 also has `BWDEVELOPER`, `DDIC`, `SAP*`; client 000 has `SAP*` — all `Down1oad`.

### 7. SAP licence (if lapsed)

The dev edition ABAP licence lasts 90 days. If SAP GUI reports it expired:

1. Log in as `SAP*` / `Down1oad`, **client 000** → transaction `SLICENSE` → copy
   **Active Hardware Key**.
2. <https://go.support.sap.com/minisap> → **"NPL - SAP NetWeaver 7.x (Sybase ASE)"** →
   paste hardware key → Generate → download `NPL.txt`.
3. `SLICENSE` → delete the installed row → **Install** → pick `NPL.txt`.

The ASE database licence (`SYBASE_ASE_TestDrive.lic`) is applied automatically during
install; it is valid to 2027-03-31.

### 8. Enable scripting

Server side:

```bash
docker compose -f sap/docker-compose.yml exec -T abap /sap/enable-scripting.sh
docker compose -f sap/docker-compose.yml exec -T abap /sap/stop.sh
docker compose -f sap/docker-compose.yml exec -T abap /sap/start.sh
```

Client side: SAP GUI Options → Accessibility & Scripting → Scripting → tick
**Enable scripting**, untick both **"Notify when a script…"** boxes. Restart SAP GUI.

### 9. Verify from the driver

From the `appium-desktop-driver` checkout:

```bash
node scripts/sap-inspect.mjs
```

Attaches over COM, dumps the SAP component tree, renders it as HTML. A
`sapgui/user_scripting is TRUE` error means step 8 didn't take.

---

## Daily use

```bash
docker compose -f sap/docker-compose.yml up -d
docker compose -f sap/docker-compose.yml exec -T abap /sap/start.sh
# ... work ...
docker compose -f sap/docker-compose.yml exec -T abap /sap/stop.sh    # ASE needs a clean stop
docker compose -f sap/docker-compose.yml stop
```

Readiness — all processes `GREEN`:

```bash
docker compose -f sap/docker-compose.yml exec -T abap \
  su - npladm -c "sapcontrol -nr 00 -function GetProcessList"
```

## Fixture-capture transactions

| Txn | Controls |
|---|---|
| `SE38` | text fields, buttons, checkboxes, radio buttons |
| `SE16` | selection screen, ALV output |
| `BCALV_GRID_DEMO` | `GuiGridView` |
| `BCALV_TREE_DEMO` | `GuiTree` |
| `SE80` | tabs, toolbars, menus |
| `SU01` | modal popups (`wnd[1]`) |

`node scripts/sap-inspect.mjs --xml --out fixture.html`, then commit the `.xml` under the
driver's `test/fixtures/sap/`.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `sysctl "vm.max_map_count" is not in a separate kernel namespace` | run the step-4 `wsl -d docker-desktop sysctl` command; it's host-wide, not per-container |
| Container exits immediately | hostname must be `vhcalnplci` |
| Install/ASE: `Read-only file system` on `vm.max_map_count` | step 4, then retry |
| ASE won't start / OOM | raise `memory=` in `.wslconfig`; close other apps |
| SAP GUI "partner not reached" | `ping vhcalnplci` resolves? container up? port 3200 published? |
| "scripting is disabled" | server: step 8; client: step 8 checkbox; fully restart SAP GUI |
| `docker` 500s / hangs after a big pull | Docker Desktop → quit → `wsl --shutdown` → reopen |
| out of disk mid-install | needs ~40 GB free on `C:` during install; the WSL vhdx grows into it |
| sapinst failed but extraction was fine | don't re-run the whole thing — the DB/kernel tarballs are already in the volumes. `docker compose exec -T abap ./install.sh -t isr -s -k` redoes just install+setup+run (sapinst resumes from `/tmp/sapinst_instdir` if the container wasn't recreated) |
| ASE `dataserver` SIGSEGV in `Snap::Validate` / hang on "internal timer is not progressing" | ASLR — step 4's `kernel.randomize_va_space=0`, then resume with `-t isr -s -k` |
