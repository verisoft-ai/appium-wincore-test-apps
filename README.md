# appium-windows2-test-apps

Fixture apps (WinForms, WPF, DevExpress, Java Swing) used by [appium-desktop-driver](https://github.com/verisoft-ai/appium-desktop-driver)'s E2E test suite.

Each fixture app proves a specific UI Automation capability — owner-drawn controls invisible to plain UIA, DevExpress custom-drawn cells, CoreCLR bridge targets, x86 injection, etc. Source is checked in; build output (`bin/`, `obj/`) is not.

Two fixtures are not correctness fixtures — they feed the driver's performance benchmark (`test/perf/` there), sized via a flag (default 1500):

- `java-swing-large` (`LargeTreeForm.java`) — large Java Swing accessibility tree. `-DnodeCount=<n>`.
- `winforms-large` (`WinformsLarge.csproj`) — large WinForms tree. `--nodes <n>`. Measured both via plain UIA and, when attached with `dotnetBridge:true`, via the .NET bridge — the same tree two ways.

One fixture is not an app but a whole backend:

- `sap/` — a Dockerised SAP NetWeaver AS ABAP 7.52 Developer Edition, the target for the driver's SAP GUI Scripting subsystem (`csharp/DesktopDriverServer/Sap/`). SAP GUI's custom-drawn controls are invisible to UIA; the driver reaches them through SAP's COM scripting API instead, and that API is empty until a real SAP session exists. `sap/README.md` is the full runbook (image build from SAP's archives, licence, scripting switches, connect). ~6 GB RAM, ~80 GB disk.

## Prerequisites

- .NET SDK (8.0+ for the `net8-*` fixtures, .NET Framework for the rest)
- JDK 8+ (for `java-swing-form`)
- A DevExpress license for the `devexpress-*` fixtures — set `DevExpress_License` (or `DevExpress_LicensePath`) to your key. Without it the build still succeeds (DX1000 evaluation watermark), but the running app shows a trial nag dialog on launch.

## Building

```bash
npm run build:test-apps   # the original small set: winform-combo, wpf-minimal, wpf-datagrid-template, java-swing-form, devexpress-grid-ownerdraw
npm run build:all         # every fixture app
```

Or build one at a time — see `package.json` for the full list of `build:*` scripts, one per fixture.

## Usage from appium-desktop-driver

`appium-desktop-driver`'s E2E tests (`test/e2e/helpers/session.ts`) resolve fixture paths via a `TEST_APPS_DIR` environment variable, defaulting to a sibling checkout:

```
../appium-windows2-test-apps
```

Clone this repo next to `appium-desktop-driver`, build the fixtures you need, then run `npm run test:e2e` from the driver repo.

## License

Apache-2.0
