import oracle.forms.jdapi.Block;
import oracle.forms.jdapi.Canvas;
import oracle.forms.jdapi.FormModule;
import oracle.forms.jdapi.Item;
import oracle.forms.jdapi.Jdapi;
import oracle.forms.jdapi.JdapiTypes;
import oracle.forms.jdapi.Trigger;
import oracle.forms.jdapi.Window;

/**
 * Builds wincore_fixture.fmb through the Forms Java API (JDAPI, ORACLE_HOME/jlib/frmjdapi.jar),
 * so the form's definition lives in source control as code instead of a binary .fmb.
 *
 * Usage: java -cp frmjdapi.jar;. GenerateFixture <output.fmb>
 *
 * The form is a single control block (no database items) with an ON-LOGON trigger that does
 * nothing, so it runs without a database connection. Items mirror what the customer's login
 * screen exposed: plain VTextField items, one item whose Implementation Class is the
 * VTextFieldEng PJC (pjc/VTextFieldEng.java), a password item, and a button whose
 * WHEN-BUTTON-PRESSED trigger copies the values the Forms *server* sees into RESULT — the
 * signal that a value set on the client actually reached the server.
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
        win.setWidth(560);
        win.setHeight(360);

        Canvas canvas = new Canvas(form, CANVAS);
        canvas.setWindowName(WINDOW);
        canvas.setWidth(560);
        canvas.setHeight(360);

        Block ctrl = new Block(form, "CTRL");
        ctrl.setDatabaseBlock(false);

        textItem(ctrl, "PLAIN_TEXT", "Plain Text", 40, null, false);
        textItem(ctrl, "CUSTOM_TEXT", "Custom Text", 80, "VTextFieldEng", false);
        textItem(ctrl, "SECRET_TEXT", "Secret Text", 120, null, true);
        Item result = textItem(ctrl, "RESULT", "Result", 220, null, false);
        result.setWidth(360);
        result.setInsertAllowed(false);
        result.setUpdateAllowed(false);

        Item echo = new Item(ctrl, "ECHO");
        echo.setItemType(JdapiTypes.ITTY_PB_CTID);
        echo.setCanvasName(CANVAS);
        echo.setLabel("Echo");
        echo.setXPosition(140);
        echo.setYPosition(170);
        echo.setWidth(100);
        echo.setHeight(24);
        new Trigger(echo, "WHEN-BUTTON-PRESSED").setTriggerText(
                ":CTRL.RESULT := 'echo:' || :CTRL.PLAIN_TEXT || '|' || :CTRL.CUSTOM_TEXT || '|' || LENGTH(:CTRL.SECRET_TEXT);");

        // No database: ON-LOGON that does nothing skips the logon dialog entirely.
        new Trigger(form, "ON-LOGON").setTriggerText("null;");

        form.save(out);
        Jdapi.shutdown();
        System.out.println("Wrote " + out);
    }

    private static Item textItem(Block block, String name, String prompt, int y, String implClass, boolean conceal) {
        Item item = new Item(block, name);
        item.setItemType(JdapiTypes.ITTY_TI_CTID);
        item.setCanvasName(CANVAS);
        item.setDatabaseItem(false);
        item.setMaximumLength(60);
        item.setXPosition(140);
        item.setYPosition(y);
        item.setWidth(200);
        item.setHeight(24);
        // Prompt is the on-screen label; Hint/Tooltip carry the same text, matching how the
        // customer's items surfaced Name == HelpText in the accessibility tree.
        item.setPrompt(prompt);
        item.setHint(prompt);
        item.setTooltip(prompt);
        if (implClass != null) item.setImplementationClass(implClass);
        if (conceal) item.setConcealData(true);
        return item;
    }
}
