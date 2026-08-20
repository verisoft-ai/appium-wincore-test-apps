// Builds the wpf-devexpress-lightweight/ fixture used by
// appium-desktop-driver's test/e2e/dotnet-bridge-devexpress-lightweight.e2e.ts. CoreCLR (.NET 8)
// WPF, DevExpress.Xpf.Grid GridControl with 5000 rows and explicit (non-auto-generated) columns —
// large enough that TableView renders unfocused/off-screen cells via
// DevExpress.Xpf.Grid.LightweightCellEditor (DevExpress's own perf optimization for virtualized
// rows) instead of a full editor. Reproduces a real customer report: LightweightCellEditor exposed
// none of the properties the bridge's reflector already probed for (Text/EditValue/DisplayText),
// so every cell in the page source came back Name="" Value="" — structurally present, no info.
//
// Requires the DevExpress_LicensePath env var (see build-devexpress-test-app.js in this same
// directory) — pinned to whatever DevExpress WPF version is actually installed locally (see
// nuget source list), not any specific customer version. This fixture exists to reproduce the
// rendering mode live and prove the bridge's fix, not to pin an exact DevExpress release.

const { execFileSync } = require('node:child_process');
const path = require('node:path');

const PROJECT_DIR = path.resolve(__dirname, '..', 'wpf-devexpress-lightweight');
const PROJECT_FILE = path.join(PROJECT_DIR, 'WpfDevExpressLightweight.csproj');
const OUT_DIR = path.join(PROJECT_DIR, 'bin');

function run(cmd, args, options = {}) {
    console.log(`> ${cmd} ${args.join(' ')}`);
    execFileSync(cmd, args, { stdio: 'inherit', ...options });
}

function main() {
    run('dotnet', ['restore', PROJECT_FILE]);
    run('dotnet', [
        'build', PROJECT_FILE,
        '-c', 'Release',
        '-o', OUT_DIR,
    ]);

    console.log(`Built WPF DevExpress lightweight-cell fixture -> ${OUT_DIR}`);
}

main();
