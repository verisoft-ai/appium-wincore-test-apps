# tn5250j fixture (IBM i / 5250)

[tn5250j](https://github.com/tn5250j/tn5250j) 0.8.0-beta2, a Java Swing
5250 terminal emulator, pointed at [PUB400](https://pub400.com), a free
public IBM i server. Target for the planned 5250 green-screen support.

## What this is

| Piece | What it is |
| --- | --- |
| tn5250j | Client only: draws the 24×80 screen, sends keystrokes |
| TN5250 | Telnet-based block-mode protocol of IBM i (formerly AS/400) |
| PUB400 | Shared IBM i 7.5 system on IBM Power, telnet port 23 |

IBM i is not a mainframe. A mainframe (IBM Z, z/OS) speaks 3270, not 5250.
Both are block-mode: the host sends a whole screen of fields, the client
fills input fields locally and sends them back on Enter or an F-key.

## Run

```powershell
npm run build:tn5250j-test-app   # fetch.ps1: download the jar, check SHA-256
.\tn5250j\start.ps1              # opens the PUB400 sign-on screen
```

Requires a JDK or JRE 8+ (`JAVA_HOME`, or `javaw.exe` on `PATH`).
`start.ps1 -Session <name>` picks another entry from `config\sessions`;
`-Wait` blocks until tn5250j exits.

## Layout

| Path | Committed | Purpose |
| --- | --- | --- |
| `fetch.ps1` | yes | Downloads the pinned release jar into `bin\` |
| `start.ps1` | yes | Launches tn5250j with repo-local settings |
| `config\sessions` | yes | Session definitions (`PUB400` → port 23) |
| `config\keymap` | yes | Key bindings (F1–F24, Field+, SysReq, Reset) |
| `config\TN5250JDefaults.props` | yes | Colours, font |
| `bin\` | no | Jar plus the runtime settings copy |

The jar is GPL-2.0, so it is downloaded rather than committed.

`start.ps1` copies `config\` into `bin\settings` on every launch and passes
`-Demulator.settingsDirectory`, so tn5250j never touches
`%USERPROFILE%\.tn5250j` and its own writes (window position, last
session) never dirty the repo. It also passes `-nc`, which skips the
single-instance bootstrapper; without it a second launch hands its session
to the running tn5250j and exits.

## Window

- Top-level frame title: `tn5250j`. Each session is a tab named after the
  session (`PUB400`), because of the `-t` flag.
- The screen is one custom-painted Swing component. Fields, text and the
  cursor are not separate accessibility elements.

## PUB400 account

Sign up for free at <https://pub400.com>. Accounts expire when unused, and
the host has scheduled IPLs and backups, so expect occasional downtime.

Keep credentials out of the repo: CI reads them from secrets, local runs
from environment variables. Tests that need PUB400 should skip, not fail,
when it is unreachable or no credentials are set.

## CI

Needs a self-hosted Windows runner with an interactive desktop (tn5250j is
a GUI) and outbound access to `pub400.com:23`.
