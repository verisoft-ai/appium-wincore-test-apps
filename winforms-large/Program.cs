#nullable enable
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WinformsLarge;

/// <summary>
/// Performance fixture: a deliberately large WinForms UI tree. Feeds
/// appium-desktop-driver's <c>dotnet-bridge</c> perf suite — attached with
/// <c>dotnetBridge:true</c> and walked via the bridge's reflected tree. The plain-UIA
/// (<c>uia</c>) suite uses the <c>wpf-large</c> fixture instead: WinForms has no native
/// UIA provider, so walking it over UIA measures the MSAA-&gt;UIA bridge, not the
/// protocol itself. One fixture per suite.
///
/// Not a correctness fixture. Size scales with <c>--nodes &lt;n&gt;</c> (default 1500).
///
/// All four sections are shown at once (2x2 grid, no tab control) — a
/// <see cref="TabControl"/> only creates the selected page's child handles, so
/// getPageSource would walk one section and the node count would not scale.
///
/// Bulk nodes come from nested Panels + Labels + TextBoxes, which WinForms always
/// realises. The DataGridView and TreeView are kept small and fully expanded: those
/// controls virtualise (off-screen grid rows / collapsed tree nodes are absent from
/// the UIA tree), so they are here for shape variety, not node count.
///
/// Anchor controls carry fixed accessible names: <c>perfAnchorFirst</c>,
/// <c>perfAnchorLast</c>, <c>perfGrid</c>, <c>perfTree</c>. The real control count is
/// written to the console and the window title on load.
/// </summary>
internal static class Program
{
    private const int DefaultNodes = 1500;
    private const int SpineDepth = 12;

    [STAThread]
    private static void Main(string[] args)
    {
        int nodes = ParseNodes(args);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new LargeForm(nodes));
    }

    private static int ParseNodes(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--nodes" && int.TryParse(args[i + 1], out var n) && n > 0)
            {
                return n;
            }
        }
        return DefaultNodes;
    }

    private sealed class LargeForm : Form
    {
        public LargeForm(int nodes)
        {
            Text = "Winforms Large Fixture";
            Size = new Size(1280, 860);
            StartPosition = FormStartPosition.CenterScreen;

            // Bulk: split across the spine (wide label grid) and the form rows.
            int wideLabels = Math.Max(1, (int)(nodes * 0.55));
            int formRows = Math.Max(1, (int)(nodes * 0.20)); // x2 controls
            const int gridRows = 25;
            const int treeLeavesPerBranch = 10;
            const int treeBranches = 12;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                AccessibleName = "sectionsGrid",
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            layout.Controls.Add(Section("spine", BuildSpine(wideLabels)), 0, 0);
            layout.Controls.Add(Section("grid", BuildGrid(gridRows)), 1, 0);
            layout.Controls.Add(Section("tree", BuildTree(treeBranches, treeLeavesPerBranch)), 0, 1);
            layout.Controls.Add(Section("forms", BuildForms(formRows)), 1, 1);
            Controls.Add(layout);

            Load += (_, _) =>
            {
                int count = CountControls(this);
                Text = $"Winforms Large Fixture ({count} controls)";
                Console.WriteLine($"[WinformsLarge] controls: {count}");
            };
        }

        private static Control Section(string name, Control content)
        {
            var box = new GroupBox { Text = name, Dock = DockStyle.Fill, AccessibleName = $"section-{name}" };
            content.Dock = DockStyle.Fill;
            box.Controls.Add(content);
            return box;
        }

        private static Control BuildSpine(int wideLabels)
        {
            var outer = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AccessibleName = "spineRoot" };

            Control current = outer;
            for (int i = 0; i < SpineDepth; i++)
            {
                var panel = new Panel { Dock = DockStyle.Top, Height = 24, AccessibleName = $"spineLevel{i}" };
                if (i == 0)
                {
                    panel.Controls.Add(new Label
                    {
                        Text = "first anchor",
                        AccessibleName = "perfAnchorFirst",
                        Dock = DockStyle.Left,
                        AutoSize = true,
                    });
                }
                current.Controls.Add(panel);
                current = panel;
            }

            // Manual positioning inside a plain Panel — the FlowLayout/TableLayout engines
            // are O(n^2)-ish and make the window take tens of seconds to appear at a few
            // thousand controls. A plain Panel with explicit Location skips all of that.
            var wide = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AccessibleName = "wideRow" };
            wide.SuspendLayout();
            const int cols = 12, cellW = 90, cellH = 20;
            for (int i = 0; i < wideLabels; i++)
            {
                wide.Controls.Add(new Label
                {
                    Text = $"w{i}",
                    AccessibleName = $"wide{i}",
                    AutoSize = false,
                    Location = new Point((i % cols) * cellW, (i / cols) * cellH),
                    Size = new Size(cellW - 4, cellH - 2),
                });
            }
            wide.ResumeLayout(false);
            outer.Controls.Add(wide);

            outer.Controls.Add(new Label
            {
                Text = "last anchor",
                AccessibleName = "perfAnchorLast",
                Dock = DockStyle.Bottom,
                AutoSize = true,
            });
            return outer;
        }

        private static Control BuildGrid(int rows)
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AccessibleName = "perfGrid",
                AllowUserToAddRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
            };
            grid.Columns.Add("id", "id");
            grid.Columns.Add("name", "name");
            grid.Columns.Add("status", "status");
            grid.Columns.Add("note", "note");
            for (int r = 0; r < rows; r++)
            {
                grid.Rows.Add($"ID-{r}", $"row {r}", r % 2 == 0 ? "active" : "inactive", $"note for row {r}");
            }
            return grid;
        }

        private static Control BuildTree(int branches, int leavesPerBranch)
        {
            var tree = new TreeView { Dock = DockStyle.Fill, AccessibleName = "perfTree" };
            for (int b = 0; b < branches; b++)
            {
                var branch = new TreeNode($"branch {b}");
                for (int i = 0; i < leavesPerBranch; i++)
                {
                    branch.Nodes.Add(new TreeNode($"leaf {b}.{i}"));
                }
                tree.Nodes.Add(branch);
            }
            tree.ExpandAll();
            return tree;
        }

        private static Control BuildForms(int rows)
        {
            // Manual positioning (see BuildSpine) — TableLayoutPanel is too slow at scale.
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AccessibleName = "formsPanel" };
            panel.SuspendLayout();
            const int rowH = 24;
            for (int i = 0; i < rows; i++)
            {
                panel.Controls.Add(new Label
                {
                    Text = $"Field {i}",
                    AccessibleName = $"formLabel{i}",
                    AutoSize = false,
                    Location = new Point(0, i * rowH),
                    Size = new Size(90, rowH - 4),
                });
                panel.Controls.Add(new TextBox
                {
                    Text = $"value {i}",
                    AccessibleName = $"formField{i}",
                    Location = new Point(96, i * rowH),
                    Width = 160,
                });
            }
            panel.ResumeLayout(false);
            return panel;
        }

        private static int CountControls(Control root)
            => 1 + root.Controls.Cast<Control>().Sum(CountControls);
    }
}
