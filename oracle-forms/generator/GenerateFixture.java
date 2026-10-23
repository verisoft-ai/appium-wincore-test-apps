import oracle.forms.jdapi.Block;
import oracle.forms.jdapi.Canvas;
import oracle.forms.jdapi.FormModule;
import oracle.forms.jdapi.Item;
import oracle.forms.jdapi.Jdapi;
import oracle.forms.jdapi.JdapiTypes;
import oracle.forms.jdapi.RadioButton;
import oracle.forms.jdapi.Trigger;
import oracle.forms.jdapi.Window;

/**
 * Builds wincore_fixture.fmb through the Forms Java API (JDAPI, ORACLE_HOME/jlib/frmjdapi.jar),
 * so the form's definition lives in source control as code instead of a binary .fmb.
 *
 * Usage: java -cp frmjdapi.jar;. GenerateFixture <output.fmb>
 *
 * The form is a single control block (no database items) with an ON-LOGON trigger that does
 * nothing, so it runs without a database connection. It holds one item of each interactive
 * Forms item type: plain VTextField items, one whose Implementation Class is the VTextFieldEng
 * PJC (pjc/VTextFieldEng.java, mirroring a customer subclass), a password item, a check box,
 * a radio group, and poplist / T-list / combo box list items. The ECHO button's
 * WHEN-BUTTON-PRESSED trigger writes the values the Forms *server* holds into RESULT, as
 * key=value pairs (n = press count, then pt, ct, sl, cb, rg, pl, tl, cmb) - the signal that a
 * client-side interaction actually reached the server.
 */
public class GenerateFixture {

    private static final String WINDOW = "MAIN_WIN";
    private static final String CANVAS = "MAIN_CV";

    public static void main(String[] args) {
        String out = args.length > 0 ? args[0] : "wincore_fixture.fmb";

        FormModule form = new FormModule("WINCORE_FIXTURE");
        form.setConsoleWindow(WINDOW);

        Window win = new Window(form, WINDOW);
        win.setTitle("Wincore Oracle Forms Fixture");
        win.setWidth(700);
        win.setHeight(540);

        Canvas canvas = new Canvas(form, CANVAS);
        canvas.setWindowName(WINDOW);
        canvas.setWidth(700);
        canvas.setHeight(540);

        Block ctrl = new Block(form, "CTRL");
        ctrl.setDatabaseBlock(false);

        textItem(ctrl, "PLAIN_TEXT", "Plain Text", 40, null, false);
        textItem(ctrl, "CUSTOM_TEXT", "Custom Text", 80, "VTextFieldEng", false);
        textItem(ctrl, "SECRET_TEXT", "Secret Text", 120, null, true);

        Item agree = baseItem(ctrl, "AGREE", JdapiTypes.ITTY_CB_CTID, 140, 250, 160, 24);
        agree.setLabel("Agree");
        agree.setCheckedValue("Y");
        agree.setUncheckedValue("N");
        agree.setCheckBoxOtherValues(JdapiTypes.CHECKBOX_UNCHECKED_CTID);
        agree.setInitializeValue("N");

        Item choice = baseItem(ctrl, "CHOICE", JdapiTypes.ITTY_RD_CTID, 140, 285, 300, 24);
        choice.setPrompt("Choice");
        choice.setInitializeValue("A");
        radioButton(choice, "RB_ALPHA", "Alpha", "A", 140);
        radioButton(choice, "RB_BETA", "Beta", "B", 240);
        radioButton(choice, "RB_GAMMA", "Gamma", "C", 340);

        listItem(ctrl, "COLOR", "Color", JdapiTypes.LSST_POPLIST_CTID, 320, 24,
                new String[][] {{"Red", "R"}, {"Green", "G"}, {"Blue", "B"}});
        listItem(ctrl, "SIZE_LIST", "Size", JdapiTypes.LSST_TLIST_CTID, 355, 70,
                new String[][] {{"Small", "S"}, {"Medium", "M"}, {"Large", "L"}});
        listItem(ctrl, "FRUIT", "Fruit", JdapiTypes.LSST_COMBO_CTID, 440, 24,
                new String[][] {{"Apple", "Apple"}, {"Banana", "Banana"}, {"Cherry", "Cherry"}});

        Item result = textItem(ctrl, "RESULT", "Result", 210, null, false);
        result.setWidth(520);
        result.setMaximumLength(400);
        result.setInsertAllowed(false);
        result.setUpdateAllowed(false);

        Item echo = baseItem(ctrl, "ECHO", JdapiTypes.ITTY_PB_CTID, 140, 165, 100, 24);
        echo.setLabel("Echo");
        // n= counts presses, so a caller can tell a fresh RESULT from the previous one.
        new Trigger(echo, "WHEN-BUTTON-PRESSED").setTriggerText(
                "DEFAULT_VALUE('0', 'GLOBAL.ECHO_N');"
                        + " :GLOBAL.ECHO_N := TO_CHAR(TO_NUMBER(:GLOBAL.ECHO_N) + 1);"
                        + " :CTRL.RESULT := 'n=' || :GLOBAL.ECHO_N"
                        + " || '|pt=' || :CTRL.PLAIN_TEXT"
                        + " || '|ct=' || :CTRL.CUSTOM_TEXT"
                        + " || '|sl=' || NVL(LENGTH(:CTRL.SECRET_TEXT), 0)"
                        + " || '|cb=' || :CTRL.AGREE"
                        + " || '|rg=' || :CTRL.CHOICE"
                        + " || '|pl=' || :CTRL.COLOR"
                        + " || '|tl=' || :CTRL.SIZE_LIST"
                        + " || '|cmb=' || :CTRL.FRUIT;");

        // No database: ON-LOGON that does nothing skips the logon dialog entirely.
        new Trigger(form, "ON-LOGON").setTriggerText("null;");

        form.save(out);
        Jdapi.shutdown();
        System.out.println("Wrote " + out);
    }

    private static Item baseItem(Block block, String name, int itemType, int x, int y, int width, int height) {
        Item item = new Item(block, name);
        item.setItemType(itemType);
        item.setCanvasName(CANVAS);
        item.setDatabaseItem(false);
        item.setXPosition(x);
        item.setYPosition(y);
        item.setWidth(width);
        item.setHeight(height);
        return item;
    }

    private static Item textItem(Block block, String name, String prompt, int y, String implClass, boolean conceal) {
        Item item = baseItem(block, name, JdapiTypes.ITTY_TI_CTID, 140, y, 200, 24);
        item.setMaximumLength(60);
        // Prompt is the on-screen label; Hint/Tooltip carry the same text, matching how the
        // customer's items surfaced Name == HelpText in the accessibility tree.
        item.setPrompt(prompt);
        item.setHint(prompt);
        item.setTooltip(prompt);
        if (implClass != null) item.setImplementationClass(implClass);
        if (conceal) item.setConcealData(true);
        return item;
    }

    private static void radioButton(Item group, String name, String label, String value, int x) {
        RadioButton rb = new RadioButton(group, name);
        rb.setLabel(label);
        rb.setRadioButtonValue(value);
        rb.setXPosition(x);
        rb.setYPosition(285);
        rb.setWidth(90);
        rb.setHeight(24);
    }

    /** List elements are {label, value}; the first one is the initial value. */
    private static void listItem(Block block, String name, String prompt, int style, int y, int height, String[][] elements) {
        Item item = baseItem(block, name, JdapiTypes.ITTY_LS_CTID, 140, y, 200, height);
        item.setListStyle(style);
        item.setPrompt(prompt);
        item.setHint(prompt);
        item.setTooltip(prompt);
        // Forms list indexes are 1-based.
        for (int i = 0; i < elements.length; i++) item.insertElement(i + 1, elements[i][0], elements[i][1]);
        item.setInitializeValue(elements[0][1]);
    }
}
