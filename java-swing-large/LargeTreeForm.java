import javax.accessibility.*;
import javax.swing.*;
import javax.swing.tree.*;
import java.awt.*;

/**
 * Performance fixture: a deliberately large Java Swing accessibility tree used to
 * benchmark the java-agent tree-walk paths (getPageSource, full-tree XPath scans,
 * bulk getAttribute) in appium-desktop-driver.
 *
 * <p>Not a correctness fixture — it proves no single UIA capability. It exists so
 * {@code test/perf/java-pagesource.perf.ts} has a stable, sizeable tree to measure
 * against, and so the {@code perfMetrics} capability's RPC counters have something
 * worth counting.
 *
 * <p>Total node count scales with the {@code nodeCount} system property
 * (default 1500): {@code javaw -DnodeCount=5000 -cp . LargeTreeForm}. The tree mixes
 * the shapes that stress different parts of the walk:
 * <ul>
 *   <li><b>deep spine</b> — {@value #SPINE_DEPTH} nested JPanels, exercises recursion + depth handling</li>
 *   <li><b>wide row</b> — one panel with hundreds of flat JLabel children, exercises
 *       the O(n) getComponentZOrder call buildInfo makes per node</li>
 *   <li><b>JTable</b> — transient AccessibleJTableCell wrappers, one per cell</li>
 *   <li><b>JTree</b> — fully expanded, virtual tree-node Accessibles</li>
 *   <li><b>form rows</b> — labelled JTextFields, realistic bulk</li>
 * </ul>
 *
 * <p>All four sections are shown at once (2x2 grid, no tabs) so getPageSource walks
 * the whole tree — a tabbed pane realises only the selected tab.
 *
 * <p>Anchor elements carry fixed accessible names for the benchmark to target:
 * {@code perfAnchorFirst}, {@code perfAnchorLast}, {@code perfTable}, {@code perfTree}.
 * On launch the real accessible-node count is printed to stdout as
 * {@code [LargeTreeForm] accessible nodes: N}.
 */
public class LargeTreeForm extends JFrame {

    static final int SPINE_DEPTH = 15;
    static final int DEFAULT_NODE_COUNT = 1500;

    public LargeTreeForm(int nodeCount) {
        setTitle("Large Tree Form");
        setDefaultCloseOperation(EXIT_ON_CLOSE);
        setSize(1200, 800);

        // Budget the requested node count across the four sections. The JTable
        // dominates: its cells are transient AccessibleJTableCell wrappers, allocated
        // fresh on every getAccessibleChild() call and registry-saved one by one — the
        // shape that most stresses the agent's per-node RPC walk. Push nodeCount high
        // (e.g. -DnodeCount=12000) to drive the table into tens of thousands of cells.
        int tableCols = 5;
        int tableRows = Math.max(1, (int) (nodeCount * 0.8) / tableCols);
        int treeNodes = Math.max(1, nodeCount / 20);
        int wideLabels = Math.max(1, nodeCount / 20);
        int formRows = Math.max(1, nodeCount / 40);

        // All four sections visible at once (no tabs) — a tabbed pane only realises
        // the selected tab's accessibility subtree, so getPageSource would walk one
        // section instead of the whole tree and the node count would not scale.
        JPanel grid = new JPanel(new GridLayout(2, 2, 4, 4));
        grid.getAccessibleContext().setAccessibleName("sectionsGrid");
        grid.add(wrapSection("spine", buildSpine(wideLabels)));
        grid.add(wrapSection("table", buildTable(tableRows, tableCols)));
        grid.add(wrapSection("tree", buildTree(treeNodes)));
        grid.add(wrapSection("forms", buildForms(formRows)));

        setContentPane(grid);
    }

    private static JComponent wrapSection(String name, JComponent content) {
        JPanel panel = new JPanel(new BorderLayout());
        panel.getAccessibleContext().setAccessibleName("section-" + name);
        panel.setBorder(BorderFactory.createTitledBorder(name));
        panel.add(content, BorderLayout.CENTER);
        return panel;
    }

    /** Deep nested-panel spine ending in a wide flat row of labels. */
    private static JComponent buildSpine(int wideLabels) {
        JPanel root = new JPanel(new BorderLayout());
        root.getAccessibleContext().setAccessibleName("spineRoot");

        JPanel current = root;
        for (int i = 0; i < SPINE_DEPTH; i++) {
            JPanel next = new JPanel(new BorderLayout());
            next.getAccessibleContext().setAccessibleName("spineLevel" + i);
            if (i == 0) {
                JLabel first = new JLabel("first anchor");
                first.getAccessibleContext().setAccessibleName("perfAnchorFirst");
                next.add(first, BorderLayout.NORTH);
            }
            current.add(next, BorderLayout.CENTER);
            current = next;
        }

        // Wide flat row: many siblings under one parent.
        JPanel wide = new JPanel(new GridLayout(0, 20, 2, 2));
        wide.getAccessibleContext().setAccessibleName("wideRow");
        for (int i = 0; i < wideLabels; i++) {
            JLabel l = new JLabel("w" + i);
            l.getAccessibleContext().setAccessibleName("wide" + i);
            wide.add(l);
        }
        current.add(new JScrollPane(wide), BorderLayout.CENTER);

        JLabel last = new JLabel("last anchor");
        last.getAccessibleContext().setAccessibleName("perfAnchorLast");
        root.add(last, BorderLayout.SOUTH);
        return root;
    }

    private static JComponent buildTable(int rows, int cols) {
        String[] colNames = new String[cols];
        for (int c = 0; c < cols; c++) {
            colNames[c] = "col" + c;
        }
        Object[][] data = new Object[rows][cols];
        for (int r = 0; r < rows; r++) {
            for (int c = 0; c < cols; c++) {
                data[r][c] = "r" + r + "c" + c;
            }
        }
        JTable table = new JTable(data, colNames);
        table.getAccessibleContext().setAccessibleName("perfTable");
        return new JScrollPane(table);
    }

    private static JComponent buildTree(int nodeCount) {
        DefaultMutableTreeNode root = new DefaultMutableTreeNode("perfTreeRoot");
        // Fan out ~20 children per branch so the tree is bushy, not a linked list.
        int perBranch = 20;
        int branches = Math.max(1, nodeCount / perBranch);
        int made = 0;
        for (int b = 0; b < branches && made < nodeCount; b++) {
            DefaultMutableTreeNode branch = new DefaultMutableTreeNode("branch " + b);
            root.add(branch);
            made++;
            for (int i = 0; i < perBranch && made < nodeCount; i++) {
                branch.add(new DefaultMutableTreeNode("leaf " + b + "." + i));
                made++;
            }
        }
        JTree tree = new JTree(root);
        tree.getAccessibleContext().setAccessibleName("perfTree");
        // Expand every row so all nodes are live in the accessibility tree (collapsed
        // subtrees are not walked). getRowCount() grows as rows expand, so this covers
        // the whole tree.
        for (int row = 0; row < tree.getRowCount(); row++) {
            tree.expandRow(row);
        }
        return new JScrollPane(tree);
    }

    private static JComponent buildForms(int rows) {
        JPanel panel = new JPanel(new GridLayout(0, 2, 4, 4));
        panel.getAccessibleContext().setAccessibleName("formsPanel");
        for (int i = 0; i < rows; i++) {
            JLabel label = new JLabel("Field " + i);
            label.getAccessibleContext().setAccessibleName("formLabel" + i);
            JTextField field = new JTextField("value " + i);
            field.getAccessibleContext().setAccessibleName("formField" + i);
            panel.add(label);
            panel.add(field);
        }
        return new JScrollPane(panel);
    }

    /** Walks the full accessible tree and returns the node count — diagnostic only. */
    private static int countAccessibleNodes(Accessible a) {
        if (a == null) return 0;
        AccessibleContext ac = a.getAccessibleContext();
        if (ac == null) return 1;
        int total = 1;
        int n;
        try {
            n = ac.getAccessibleChildrenCount();
        } catch (Throwable t) {
            return total;
        }
        for (int i = 0; i < n; i++) {
            try {
                total += countAccessibleNodes(ac.getAccessibleChild(i));
            } catch (Throwable ignored) {
            }
        }
        return total;
    }

    public static void main(String[] args) {
        int nodeCount = Integer.getInteger("nodeCount", DEFAULT_NODE_COUNT);
        SwingUtilities.invokeLater(() -> {
            LargeTreeForm frame = new LargeTreeForm(nodeCount);
            frame.setVisible(true);
            // Let the tab contents realize, then report the real tree size.
            SwingUtilities.invokeLater(() ->
                System.out.println("[LargeTreeForm] accessible nodes: " + countAccessibleNodes(frame)));
        });
    }
}
