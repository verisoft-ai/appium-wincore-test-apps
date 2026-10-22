/**
 * Stand-in for the customer's custom text item: a Pluggable Java Component in the
 * default package that extends Oracle's VTextField, set as an item's Implementation
 * Class. It adds no behaviour on purpose — it reproduces the class lineage the java
 * bridge has to recognise (a non-Oracle class whose superclass is oracle.forms.ui.*),
 * not whatever the customer's subclass does on top.
 *
 * Compiled against ORACLE_HOME/forms/java/frmall.jar into wincore-pjc.jar (build.ps1).
 */
public class VTextFieldEng extends oracle.forms.ui.VTextField {
}
