import javax.swing.*;
import javax.swing.tree.DefaultMutableTreeNode;
import java.awt.*;

/**
 * Fixture for the tree-provider pattern commands the plain TestForm has no controls for:
 * expand/collapse on tree nodes, multi- and single-select lists, scroll-into-view on a long
 * list, and range values (slider / progress bar). Each control's accessible name is the
 * locator the e2e tests use.
 */
public class ControlsForm extends JFrame {
    public ControlsForm() {
        setTitle("Controls Form");
        setDefaultCloseOperation(EXIT_ON_CLOSE);
        setSize(640, 520);
        setLayout(new GridLayout(0, 2, 10, 10));

        DefaultMutableTreeNode root = new DefaultMutableTreeNode("Food");
        DefaultMutableTreeNode fruits = new DefaultMutableTreeNode("Fruits");
        fruits.add(new DefaultMutableTreeNode("Apple"));
        fruits.add(new DefaultMutableTreeNode("Banana"));
        DefaultMutableTreeNode vegetables = new DefaultMutableTreeNode("Vegetables");
        vegetables.add(new DefaultMutableTreeNode("Carrot"));
        root.add(fruits);
        root.add(vegetables);
        JTree tree = new JTree(root);
        tree.setRootVisible(false);
        tree.expandRow(0); // Fruits starts expanded, Vegetables collapsed
        tree.getAccessibleContext().setAccessibleName("foodTree");
        add(new JScrollPane(tree));

        JList<String> multiList = new JList<>(new String[]{"Alpha", "Beta", "Gamma", "Delta", "Epsilon"});
        multiList.setSelectionMode(ListSelectionModel.MULTIPLE_INTERVAL_SELECTION);
        multiList.getAccessibleContext().setAccessibleName("multiList");
        add(new JScrollPane(multiList));

        JList<String> singleList = new JList<>(new String[]{"One", "Two", "Three"});
        singleList.setSelectionMode(ListSelectionModel.SINGLE_SELECTION);
        singleList.getAccessibleContext().setAccessibleName("singleList");
        add(new JScrollPane(singleList));

        String[] many = new String[200];
        for (int i = 0; i < many.length; i++) many[i] = "Row " + i;
        JList<String> longList = new JList<>(many);
        longList.setVisibleRowCount(4);
        longList.getAccessibleContext().setAccessibleName("longList");
        add(new JScrollPane(longList));

        JSlider slider = new JSlider(0, 100, 20);
        slider.getAccessibleContext().setAccessibleName("volumeSlider");
        add(slider);

        JProgressBar progress = new JProgressBar(0, 100);
        progress.setValue(10);
        progress.getAccessibleContext().setAccessibleName("progressBar");
        add(progress);

        JComboBox<String> combo = new JComboBox<>(new String[]{"Red", "Green", "Blue"});
        combo.getAccessibleContext().setAccessibleName("colorCombo");
        add(combo);

        JButton plain = new JButton("Plain");
        plain.getAccessibleContext().setAccessibleName("plainButton");
        add(plain);
    }

    public static void main(String[] args) {
        SwingUtilities.invokeLater(() -> new ControlsForm().setVisible(true));
    }
}
