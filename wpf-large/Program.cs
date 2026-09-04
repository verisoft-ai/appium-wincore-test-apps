#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfLarge;

/// <summary>
/// Performance fixture: a deliberately large WPF UI tree. WPF exposes a native UI
/// Automation provider (AutomationPeer), so appium-desktop-driver's <c>uia</c> perf
/// suite uses this to measure the plain-UIA tree walk (getPageSource / XPath
/// materialisation) without the MSAA-&gt;UIA bridge cost that WinForms carries.
///
/// Twin of <c>winforms-large</c> in shape (2x2 grid, four sections, same anchor names)
/// and target framework (net472), so the two fixtures differ only by UI framework.
/// <c>winforms-large</c> now feeds only the <c>dotnet-bridge</c> suite.
///
/// Not a correctness fixture. Size scales with <c>--nodes &lt;n&gt;</c> (default 1500).
///
/// All four sections are shown at once (2x2 <see cref="Grid"/>, no TabControl) — a tab
/// control only realises the selected page, so the node count would not scale.
///
/// Bulk nodes come from a <see cref="Canvas"/> of <see cref="TextBlock"/> /
/// <see cref="TextBox"/> with explicit coordinates — Canvas has no measure/arrange
/// cost per child, so the window appears quickly even at thousands of elements
/// (StackPanel / WrapPanel are O(n)-per-pass and make startup take tens of seconds).
/// The <see cref="DataGrid"/> and <see cref="TreeView"/> are kept small and fully
/// expanded: those controls virtualise, so off-screen rows / collapsed nodes are
/// absent from the UIA tree — they are here for shape variety, not node count.
///
/// Anchor controls carry fixed automation names: <c>perfAnchorFirst</c>,
/// <c>perfAnchorLast</c>, <c>perfGrid</c>, <c>perfTree</c>. The realised element count
/// is written to the console and the window title on load.
/// </summary>
internal static class Program
{
    private const int DefaultNodes = 1500;
    private const int SpineDepth = 12;

    [STAThread]
    private static void Main(string[] args)
    {
        int nodes = ParseNodes(args);
        var app = new Application();
        app.Run(new LargeWindow(nodes));
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

    public sealed class GridRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public string Note { get; set; } = "";
    }

    private sealed class LargeWindow : Window
    {
        public LargeWindow(int nodes)
        {
            Title = "Wpf Large Fixture";
            Width = 1280;
            Height = 860;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            int wideLabels = Math.Max(1, (int)(nodes * 0.55));
            int formRows = Math.Max(1, (int)(nodes * 0.20)); // x2 controls
            const int gridRows = 25;
            const int treeLeavesPerBranch = 10;
            const int treeBranches = 12;

            var layout = new Grid();
            AutomationProperties.SetName(layout, "sectionsGrid");
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            AddSection(layout, 0, 0, "spine", BuildSpine(wideLabels));
            AddSection(layout, 0, 1, "grid", BuildGrid(gridRows));
            AddSection(layout, 1, 0, "tree", BuildTree(treeBranches, treeLeavesPerBranch));
            AddSection(layout, 1, 1, "forms", BuildForms(formRows));

            Content = layout;

            Loaded += (_, _) =>
            {
                int count = CountVisual(this);
                Title = $"Wpf Large Fixture ({count} elements)";
                Console.WriteLine($"[WpfLarge] visual elements: {count}");
            };
        }

        private static void AddSection(Grid parent, int row, int col, string name, UIElement content)
        {
            var box = new GroupBox { Header = name, Content = content };
            AutomationProperties.SetName(box, $"section-{name}");
            Grid.SetRow(box, row);
            Grid.SetColumn(box, col);
            parent.Children.Add(box);
        }

        private static UIElement BuildSpine(int wideLabels)
        {
            var outer = new DockPanel { LastChildFill = true };
            AutomationProperties.SetName(outer, "spineRoot");

            // A chain of nested StackPanels — WPF panels show up in the raw UIA view,
            // which is what page source / native find walk.
            var spineHost = new StackPanel();
            AutomationProperties.SetName(spineHost, "spineLevel0");
            Panel current = spineHost;
            for (int i = 1; i < SpineDepth; i++)
            {
                var panel = new StackPanel();
                AutomationProperties.SetName(panel, $"spineLevel{i}");
                current.Children.Add(panel);
                current = panel;
            }
            var firstAnchor = new TextBlock { Text = "first anchor" };
            AutomationProperties.SetName(firstAnchor, "perfAnchorFirst");
            current.Children.Add(firstAnchor);

            DockPanel.SetDock(spineHost, Dock.Top);
            outer.Children.Add(spineHost);

            var last = new TextBlock { Text = "last anchor" };
            AutomationProperties.SetName(last, "perfAnchorLast");
            DockPanel.SetDock(last, Dock.Bottom);
            outer.Children.Add(last);

            // Bulk: a Canvas with explicit coordinates — no per-child layout cost.
            var wide = new Canvas();
            AutomationProperties.SetName(wide, "wideRow");
            const int cols = 12, cellW = 90, cellH = 20;
            for (int i = 0; i < wideLabels; i++)
            {
                var lbl = new TextBlock
                {
                    Text = $"w{i}",
                    Width = cellW - 4,
                    Height = cellH - 2,
                };
                AutomationProperties.SetName(lbl, $"wide{i}");
                Canvas.SetLeft(lbl, (i % cols) * cellW);
                Canvas.SetTop(lbl, (i / cols) * cellH);
                wide.Children.Add(lbl);
            }
            var scroller = new ScrollViewer
            {
                Content = wide,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            outer.Children.Add(scroller);
            return outer;
        }

        private static UIElement BuildGrid(int rows)
        {
            var data = new List<GridRow>();
            for (int r = 0; r < rows; r++)
            {
                data.Add(new GridRow
                {
                    Id = $"ID-{r}",
                    Name = $"row {r}",
                    Status = r % 2 == 0 ? "active" : "inactive",
                    Note = $"note for row {r}",
                });
            }
            var grid = new DataGrid
            {
                ItemsSource = data,
                AutoGenerateColumns = true,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
            };
            AutomationProperties.SetName(grid, "perfGrid");
            return grid;
        }

        private static UIElement BuildTree(int branches, int leavesPerBranch)
        {
            var tree = new TreeView();
            AutomationProperties.SetName(tree, "perfTree");
            for (int b = 0; b < branches; b++)
            {
                var branch = new TreeViewItem { Header = $"branch {b}", IsExpanded = true };
                for (int i = 0; i < leavesPerBranch; i++)
                {
                    branch.Items.Add(new TreeViewItem { Header = $"leaf {b}.{i}" });
                }
                tree.Items.Add(branch);
            }
            return tree;
        }

        private static UIElement BuildForms(int rows)
        {
            var panel = new Canvas();
            AutomationProperties.SetName(panel, "formsPanel");
            const int rowH = 24;
            for (int i = 0; i < rows; i++)
            {
                var lbl = new TextBlock { Text = $"Field {i}", Width = 90, Height = rowH - 4 };
                AutomationProperties.SetName(lbl, $"formLabel{i}");
                Canvas.SetLeft(lbl, 0);
                Canvas.SetTop(lbl, i * rowH);
                panel.Children.Add(lbl);

                var box = new TextBox { Text = $"value {i}", Width = 160 };
                AutomationProperties.SetName(box, $"formField{i}");
                Canvas.SetLeft(box, 96);
                Canvas.SetTop(box, i * rowH);
                panel.Children.Add(box);
            }
            return new ScrollViewer
            {
                Content = panel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
        }

        private static int CountVisual(DependencyObject root)
        {
            int n = 1;
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                n += CountVisual(VisualTreeHelper.GetChild(root, i));
            }
            return n;
        }
    }
}
