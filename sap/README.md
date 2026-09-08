# Local SAP backend for testing the SAP GUI Scripting subsystem

A throwaway SAP NetWeaver AS ABAP **7.52 Developer Edition** (SID `NPL`) in Docker, used
to exercise `csharp/DesktopDriverServer/Sap/` (attach, page source, `FindById`, actions,
grid/tree) against real SAP screens and to capture tree fixtures.

This is the **light, ASE-based** trial — **~6 GB RAM, ~80 GB disk** — chosen over the
HANA-based `sapse/abap-cloud-developer-trial:2025` which needs 24 GB RAM (this dev machine
has 15.7 GB).

> **Licensing:** the ABAP Developer Edition is free for personal, non-productive use. SAP
> is **withdrawing these downloads on 30 September 2026** — grab the archives before then if
> you use the build route.

---

## One-time setup

### 1. Give WSL2 enough memory

Create `C:\Users\<you>\.wslconfig` (an example is in `sap/wslconfig.example`):

```
[wsl2]
memory=11GB
processors=4
```

Then `wsl --shutdown` and reopen Docker Desktop. (11 GB to WSL leaves ~5 GB for Windows.
`docker-compose.yml` caps the container itself at 10 GB via `mem_limit`.)

### 2. Get an image — pick one route

**Route A — community prebuilt (fastest, no SAP login):**

```bash
cp sap/.env.example sap/.env      # already points at yoraco/sap-dev-edition-7.52-sp04
docker pull yoraco/sap-dev-edition-7.52-sp04     # ~30 GB
```

The SAP install archives are baked into this image; you still run `install.sh` once (step 3).
Trade-off: it's a third-party Docker Hub image republishing SAP's software.

**Route B — build from official archives (clean provenance, self-contained in this repo):**

1. Go to <https://developers.sap.com/trials-downloads.html>, log in with your P-user,
   search **"AS ABAP 7.52"** (SP01 or SP04 — whichever is listed). Download every RAR
   part (~10 GB total). **Before 30 Sep 2026.**
2. Drop all the `.rar` parts into `sap/build/archives/`.
3. Build (extracts the archives, then `docker build`):
   ```bash
   bash sap/build/build.sh
   ```
   The Dockerfile and unattended-install expect script live in `sap/build/` — nothing
   to clone. Produces image `nwabap:7.52`.
4. `cp sap/.env.example sap/.env` and set `SAP_ABAP_IMAGE=nwabap:7.52`.

### 3. First boot + install

```bash
docker compose -f sap/docker-compose.yml up -d
docker compose -f sap/docker-compose.yml exec abap ./install.exp
```

`install.exp` runs the SAP installer unattended (accepts the licence, sets the master
password to `Down1oad`). Takes 20–40 min. Success = **"Installation of NPL successful"**.

For Route A (`yoraco/...`) the script may be named differently — `docker compose … exec
abap ls /tmp/sapdownloads` and check its `INSTALL_README.md`.

The install writes `vm.max_map_count`; the compose file already sets it via `sysctls`,
but if you see `setting key "vm.max_map_count": Read-only file system`, set it on the
WSL host first: `wsl -d docker-desktop sysctl -w vm.max_map_count=2000000`.

### 4. License

> Needs SAP GUI — do **step 6 (connect)** first, then come back here.

The dev edition ships a 90-day licence; if it has lapsed you renew it free:

1. Start SAP (step 6 first if needed), log into SAP GUI as `SAP*` / `Down1oad`, **client 000**.
2. Transaction `SLICENSE` → copy **Active Hardware Key**.
3. <https://go.support.sap.com/minisap> → P-user → choose
   **"NPL - SAP NetWeaver 7.x (Sybase ASE)"** → paste hardware key → Generate → download `NPL.txt`.
4. Back in `SLICENSE`: delete the installed licence row → **Install** → pick `NPL.txt`.

### 5. Enable scripting

**Server side** — from the host:

```bash
docker compose -f sap/docker-compose.yml exec abap /sap/enable-scripting.sh
docker compose -f sap/docker-compose.yml exec abap /sap/stop.sh
docker compose -f sap/docker-compose.yml exec abap /sap/start.sh
```

**Client side** — SAP GUI Options → Accessibility & Scripting → Scripting →
tick **Enable scripting**, untick both **"Notify when a script…"** boxes.

### 6. Connect SAP GUI for Windows

Add to `C:\Windows\System32\drivers\etc\hosts` (as admin):

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
Other users (client 001): `BWDEVELOPER`, `DDIC`, `SAP*` — same password. Client 000: `SAP*` / `Down1oad`.

### 7. Verify with the driver

```bash
node scripts/sap-inspect.mjs
```

Attaches over COM, dumps the SAP tree, renders it as HTML. If it reports
`sapgui/user_scripting is TRUE` needed → redo step 5.

---

## Daily use

```bash
# start container + SAP
docker compose -f sap/docker-compose.yml up -d
docker compose -f sap/docker-compose.yml exec abap /sap/start.sh

# ... work ...

# stop SAP + container (graceful — ASE needs a clean shutdown)
docker compose -f sap/docker-compose.yml exec abap /sap/stop.sh
docker compose -f sap/docker-compose.yml stop
```

Readiness check (all processes must be `GREEN`):

```bash
docker compose -f sap/docker-compose.yml exec abap \
  su - npladm -c "sapcontrol -nr 00 -function GetProcessList"
```

## Test transactions (fixture sources)

| Txn | Controls |
|---|---|
| `SE38` | text fields, buttons, checkboxes, radio buttons |
| `SE16` | selection screen, ALV output |
| `BCALV_GRID_DEMO` | `GuiGridView` |
| `BCALV_TREE_DEMO` | `GuiTree` |
| `SE80` | tabs, toolbars, menus |
| `SU01` | modal popups (`wnd[1]`) |

Capture each: `node scripts/sap-inspect.mjs --xml --out fixture.html`, commit the `.xml`
under `test/fixtures/sap/`.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Container exits immediately | hostname not `vhcalnplci`; or `vm.max_map_count` too low (compose sets it — check Docker Desktop honoured the `sysctls`) |
| ASE won't start / OOM | raise `memory=` in `.wslconfig`; lower other apps; `mem_limit` in compose must be ≤ WSL memory |
| SAP GUI "partner not reached" | `ping vhcalnplci` must resolve (hosts entry); container up; port 3200 published |
| "scripting is disabled" | server: step 5 + restart SAP; client: step 5 checkbox; restart SAP GUI fully |
| `docker` 500 errors after a big pull | Docker Desktop → quit → `wsl --shutdown` → reopen |
