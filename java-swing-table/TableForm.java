import javax.accessibility.*;
import javax.swing.*;
import javax.swing.table.*;
import java.awt.*;
import java.util.Locale;

/**
 * Regression fixture reproducing a customer's Java Swing screen where XPath predicates on
 * {@code @TableRow} / {@code @TableColumn} and {@code contains(@Name, ...)} on RTL (Hebrew)
 * text returned zero elements even though getPageSource showed the values.
 *
 * Structural traits copied from the customer's captured page source:
 *  - a custom JTable subclass (their "HrTable") with an empty accessible name
 *  - EXACTLY ONE data row, EIGHT columns (RowCount=1, ColumnCount=8)
 *  - column 0 rendered as a radio button (selection column), columns 1..7 as labels
 *  - Hebrew / right-to-left cell text; several cells carry a tooltip (AccessibleDescription)
 *  - table cells are transient javax.swing.JTable$AccessibleJTable$AccessibleJTableCell
 *  - a sibling status label "נמצאו (1) שורות" NOT inside the table
 *  - deep panel/scroll-pane nesting between the frame and the table
 */
public class TableForm extends JFrame {

    /** Mirrors the customer's "HrTable" custom subclass with a blank accessible name. */
    static class HrTable extends JTable {
        HrTable(TableModel m) {
            super(m);
            getAccessibleContext().setAccessibleName("");
        }
    }

    /** Mirrors the customer's "HrMessageLabel". */
    static class HrMessageLabel extends JLabel {
        HrMessageLabel(String text) {
            super(text);
            getAccessibleContext().setAccessibleName(text);
        }
    }

    static class RadioCellRenderer extends JRadioButton implements TableCellRenderer {
        RadioCellRenderer() {
            setHorizontalAlignment(CENTER);
        }
        @Override
        public Component getTableCellRendererComponent(JTable table, Object value,
                boolean isSelected, boolean hasFocus, int row, int column) {
            setSelected(Boolean.TRUE.equals(value));
            getAccessibleContext().setAccessibleName(Boolean.TRUE.equals(value) ? "selected" : "not selected");
            return this;
        }
    }

    // Column headers and the single data row — Hebrew, right-to-left.
    static final String[] COLUMNS = {
            "בחר", "ת.ז.", "שם", "מספר", "תאריך", "סוג", "סטטוס", "הערה"
    };
    // Column 5 and 6 intentionally blank — the customer's page source had empty-Name cells
    // mid-row, and predicates on @TableColumn must still locate them.
    static final Object[] ROW = {
            Boolean.TRUE, "300000001", "ישראל ישראלי", "000962035",
            "24/08/2026", "", "", "אין הערה"
    };

    public TableForm() {
        setTitle("Table Form");
        setDefaultCloseOperation(EXIT_ON_CLOSE);
        setSize(1000, 400);
        applyComponentOrientation(ComponentOrientation.RIGHT_TO_LEFT);

        DefaultTableModel model = new DefaultTableModel(new Object[][]{ ROW }, COLUMNS) {
            @Override
            public Class<?> getColumnClass(int c) {
                return c == 0 ? Boolean.class : String.class;
            }
            @Override
            public boolean isCellEditable(int r, int c) {
                return false;
            }
        };

        HrTable table = new HrTable(model);
        table.setRowHeight(22);
        table.getColumnModel().getColumn(0).setCellRenderer(new RadioCellRenderer());

        // Enterprise tables almost always have a sorter/filter installed; this changes the
        // AccessibleJTable view<->model index mapping the cells report.
        TableRowSorter<TableModel> sorter = new TableRowSorter<>(model);
        table.setRowSorter(sorter);

        // Give a couple of cells a tooltip so AccessibleDescription (HelpText) is populated,
        // matching the customer's page source where some cells carried an HTML tooltip.
        DefaultTableCellRenderer tip = new DefaultTableCellRenderer() {
            @Override
            public Component getTableCellRendererComponent(JTable t, Object value,
                    boolean isSelected, boolean hasFocus, int row, int column) {
                Component comp = super.getTableCellRendererComponent(t, value, isSelected, hasFocus, row, column);
                ((JComponent) comp).setToolTipText(String.valueOf(value));
                return comp;
            }
        };
        table.getColumnModel().getColumn(3).setCellRenderer(tip);

        // Deep nesting: frame > outerPanel > midPanel > scrollPane > viewport > table
        JScrollPane scroll = new JScrollPane(table);
        JPanel midPanel = new JPanel(new BorderLayout());
        midPanel.getAccessibleContext().setAccessibleName("tableHostPanel");
        midPanel.add(scroll, BorderLayout.CENTER);

        HrMessageLabel status = new HrMessageLabel("נמצאו (1) שורות");
        JLabel summary = new JLabel("סה\"כ 5 רשומות פעילות");
        summary.getAccessibleContext().setAccessibleName("סה\"כ 5 רשומות פעילות");

        JPanel headerPanel = new JPanel(new FlowLayout(FlowLayout.RIGHT));
        headerPanel.add(summary);
        headerPanel.add(status);

        // A 3rd-party-ish node that throws while being walked, placed BEFORE the table in
        // traversal order — mirrors real enterprise screens with flaky custom components.
        JPanel brokenSibling = new JPanel() {
            @Override
            public AccessibleContext getAccessibleContext() {
                if (accessibleContext == null) {
                    accessibleContext = new AccessibleJPanel() {
                        @Override
                        public int getAccessibleChildrenCount() {
                            throw new IllegalStateException("simulated broken node (regression fixture)");
                        }
                    };
                    accessibleContext.setAccessibleName("brokenSibling");
                }
                return accessibleContext;
            }
        };

        JPanel outerPanel = new JPanel(new BorderLayout());
        outerPanel.getAccessibleContext().setAccessibleName("outerPanel");
        outerPanel.add(headerPanel, BorderLayout.NORTH);
        outerPanel.add(brokenSibling, BorderLayout.WEST);
        outerPanel.add(midPanel, BorderLayout.CENTER);

        setContentPane(outerPanel);
        applyComponentOrientation(ComponentOrientation.RIGHT_TO_LEFT);
    }

    public static void main(String[] args) {
        Locale.setDefault(new Locale("he", "IL"));
        SwingUtilities.invokeLater(() -> new TableForm().setVisible(true));
    }
}
