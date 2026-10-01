#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MsaaLegacyControls;

// MSAA-only fixture for the driver's standard-accessibility-values work. Every control here
// publishes its content through IAccessible (MSAA) only — UIA reaches it through
// UIAutomationCore's "MSAA Proxy", the same path VB6 / Delphi / older MFC / many third-party
// grids take. No third-party dependency and no licence, so it runs in CI.
//
// Real content lives in accValue; accName is a placeholder ("Host row 2", "Group Row") — the
// shape that makes UIA Name useless on these apps. StatusLabel echoes every effect in plain
// text so tests can verify actions without trusting accessibility.
//
// Accessible names used as anchors: serversGrid, legacyOutline, pinBox, passwordBox,
// nativeTree. statusLabel keeps its text as UIA Name (AutomationId "statusLabel").

// MSAA constants (oleacc.h).
internal static class Msaa
{
    public const AccessibleStates Expanded = AccessibleStates.Expanded;     // 0x200
    public const AccessibleStates Collapsed = AccessibleStates.Collapsed;   // 0x400
    public const AccessibleStates Protected = AccessibleStates.Protected;   // 0x20000000
}

// ── Custom IAccessible-only outline (VB6/Delphi-style tree grid) ──────────────────────────────

internal sealed class OutlineGroup
{
    public string Value { get; set; } = "";
    public string[] Items { get; set; } = Array.Empty<string>();   // item values; names are placeholders
    public bool Expanded { get; set; }

    // ReportsState=false: toggles on the default action but never sets EXPANDED/COLLAPSED,
    // like controls that never maintain the bit. Driver must not regress on these.
    public bool ReportsState { get; set; } = true;

    // Stuck=true: reports COLLAPSED, default action does nothing. Driver must fail loudly.
    public bool Stuck { get; set; }
}

internal sealed class LegacyOutline : Control
{
    private const int RowHeight = 22;

    public readonly List<OutlineGroup> Groups = new()
    {
        new OutlineGroup { Value = "Region: US East", Items = new[] { "web-01", "db-01" }, Expanded = true },
        new OutlineGroup { Value = "Region: EU West", Items = new[] { "api-03", "cache-01" }, Expanded = false },
        new OutlineGroup { Value = "Region: Archive", Items = new[] { "old-01" }, Expanded = false, ReportsState = false },
        new OutlineGroup { Value = "Region: Locked", Items = new[] { "vault-01" }, Expanded = false, Stuck = true },
    };

    public string? SelectedItem { get; private set; }
    public event Action? StateChanged;

    public LegacyOutline()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AccessibleName = "legacyOutline";
        BackColor = Color.White;
    }

    public void Toggle(OutlineGroup g)
    {
        if (g.Stuck) return;
        g.Expanded = !g.Expanded;
        Invalidate();
        StateChanged?.Invoke();
    }

    public void SelectItem(string item)
    {
        SelectedItem = item;
        Invalidate();
        StateChanged?.Invoke();
    }

    // Visible rows in paint order: (group, itemIndex or -1 for the group row).
    public IEnumerable<(OutlineGroup Group, int Item)> Rows()
    {
        foreach (var g in Groups)
        {
            yield return (g, -1);
            if (g.Expanded) for (int i = 0; i < g.Items.Length; i++) yield return (g, i);
        }
    }

    public Rectangle RowBounds(OutlineGroup g, int item)
    {
        int index = 0;
        foreach (var r in Rows())
        {
            if (r.Group == g && r.Item == item) return new Rectangle(0, index * RowHeight, Width, RowHeight);
            index++;
        }
        return Rectangle.Empty;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        int y = 0;
        foreach (var (g, i) in Rows())
        {
            var rect = new Rectangle(0, y, Width, RowHeight);
            if (i < 0)
            {
                var glyph = g.ReportsState || g.Stuck ? (g.Expanded ? "▼ " : "▶ ") : "• ";
                e.Graphics.FillRectangle(Brushes.Gainsboro, rect);
                TextRenderer.DrawText(e.Graphics, glyph + g.Value, Font, rect, Color.Black, TextFormatFlags.VerticalCenter);
            }
            else
            {
                var item = g.Items[i];
                if (item == SelectedItem) e.Graphics.FillRectangle(Brushes.LightSkyBlue, rect);
                TextRenderer.DrawText(e.Graphics, "      " + item, Font, rect, Color.Black, TextFormatFlags.VerticalCenter);
            }
            y += RowHeight;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int index = e.Y / RowHeight;
        var row = Rows().Skip(index).FirstOrDefault();
        if (row.Group == null) return;
        if (row.Item < 0) Toggle(row.Group);
        else SelectItem(row.Group.Items[row.Item]);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new OutlineAccessible(this);

    private sealed class OutlineAccessible : ControlAccessibleObject
    {
        private readonly LegacyOutline _owner;
        public OutlineAccessible(LegacyOutline owner) : base(owner) { _owner = owner; }

        public override AccessibleRole Role => AccessibleRole.Outline;
        public override int GetChildCount() => _owner.Groups.Count;
        public override AccessibleObject GetChild(int index) => new GroupAccessible(_owner, this, _owner.Groups[index]);
    }

    private sealed class GroupAccessible : AccessibleObject
    {
        private readonly LegacyOutline _owner;
        private readonly AccessibleObject _parent;
        private readonly OutlineGroup _g;

        public GroupAccessible(LegacyOutline owner, AccessibleObject parent, OutlineGroup g)
        {
            _owner = owner; _parent = parent; _g = g;
        }

        public override string Name { get => "Group Row"; set { } }
        public override string Value { get => _g.Value; set { } }
        public override AccessibleRole Role => AccessibleRole.OutlineItem;
        public override AccessibleObject Parent => _parent;
        public override Rectangle Bounds => _owner.RectangleToScreen(_owner.RowBounds(_g, -1));

        // Not selectable: Select() is a no-op, so the driver's select must report failure.
        public override AccessibleStates State
        {
            get
            {
                var s = AccessibleStates.Focusable;
                if (_g.ReportsState || _g.Stuck) s |= _g.Expanded ? Msaa.Expanded : Msaa.Collapsed;
                return s;
            }
        }

        public override string DefaultAction => !_g.ReportsState && !_g.Stuck ? "Toggle" : (_g.Expanded ? "Collapse" : "Expand");
        public override void DoDefaultAction() => _owner.Toggle(_g);
        public override void Select(AccessibleSelection flags) { }

        public override int GetChildCount() => _g.Expanded ? _g.Items.Length : 0;
        public override AccessibleObject GetChild(int index) => new ItemAccessible(_owner, this, _g, index);
    }

    private sealed class ItemAccessible : AccessibleObject
    {
        private readonly LegacyOutline _owner;
        private readonly AccessibleObject _parent;
        private readonly OutlineGroup _g;
        private readonly int _i;

        public ItemAccessible(LegacyOutline owner, AccessibleObject parent, OutlineGroup g, int i)
        {
            _owner = owner; _parent = parent; _g = g; _i = i;
        }

        private string Item => _g.Items[_i];

        // Placeholder name, numbered across the whole outline like DevExpress' "Host row N".
        public override string Name
        {
            get => "Host row " + (_owner.Groups.TakeWhile(x => x != _g).Sum(x => x.Items.Length) + _i + 1);
            set { }
        }

        public override string Value { get => Item; set { } }
        public override AccessibleRole Role => AccessibleRole.OutlineItem;
        public override AccessibleObject Parent => _parent;
        public override Rectangle Bounds => _owner.RectangleToScreen(_owner.RowBounds(_g, _i));
        public override string DefaultAction => "Select";
        public override void DoDefaultAction() => _owner.SelectItem(Item);

        public override AccessibleStates State
        {
            get
            {
                var s = AccessibleStates.Selectable | AccessibleStates.Focusable;
                if (_owner.SelectedItem == Item) s |= AccessibleStates.Selected | AccessibleStates.Focused;
                return s;
            }
        }

        public override void Select(AccessibleSelection flags)
        {
            if ((flags & AccessibleSelection.TakeSelection) != 0) _owner.SelectItem(Item);
        }
    }
}

// ── Legacy PIN box: PROTECTED state, but accValue leaks the secret ────────────────────────────

internal sealed class LegacyPinBox : Control
{
    public const string Pin = "4721";

    public LegacyPinBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        AccessibleName = "pinBox";
        BackColor = Color.White;
        Size = new Size(120, 24);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        TextRenderer.DrawText(e.Graphics, new string('●', Pin.Length), Font, ClientRectangle, Color.Black, TextFormatFlags.VerticalCenter);
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.Gray, ButtonBorderStyle.Solid);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new PinAccessible(this);

    private sealed class PinAccessible : ControlAccessibleObject
    {
        public PinAccessible(LegacyPinBox owner) : base(owner) { }
        public override AccessibleRole Role => AccessibleRole.Text;
        public override string Value { get => Pin; set { } }
        public override AccessibleStates State => AccessibleStates.Focusable | Msaa.Protected;
    }
}

// ── Legacy combo: MSAA-only, no ExpandCollapsePattern, opens only on ALT+Down ─────────────────

// The customer case behind the driver's ALT+Down fallback: a combo box UIA3 exposes with no
// ExpandCollapsePattern, no MSAA default action, that only opens from the keyboard.
// ReportsState=true sets STATE_SYSTEM_COLLAPSED / EXPANDED (like DevExpress ComboBoxEdit);
// false reports neither bit — the driver then has nothing to verify against.
internal sealed class LegacyCombo : Control
{
    private readonly string[] _items = { "Books", "Electronics", "Groceries", "Toys" };
    private readonly bool _reportsState;
    public bool IsOpen { get; private set; }
    public event Action? OpenChanged;

    public LegacyCombo(string accessibleName, bool reportsState)
    {
        _reportsState = reportsState;
        AccessibleName = accessibleName;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Color.White;
        Size = new Size(160, 22);
    }

    private void SetOpen(bool open)
    {
        if (IsOpen == open) return;
        IsOpen = open;
        Height = open ? 22 * (_items.Length + 1) : 22;
        Invalidate();
        OpenChanged?.Invoke();
    }

    // ALT+Down (and F4) toggles, like a Win32 combo. A click only focuses — the driver's
    // fallback may click to take focus before sending ALT+Down.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Alt | Keys.Down) || keyData == Keys.F4)
        {
            SetOpen(!IsOpen);
            return true;
        }
        if (keyData == Keys.Escape && IsOpen)
        {
            SetOpen(false);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        TextRenderer.DrawText(e.Graphics, _items[0] + "  ▾", Font, new Rectangle(0, 0, Width, 22), Color.Black, TextFormatFlags.VerticalCenter);
        if (IsOpen)
        {
            for (int i = 0; i < _items.Length; i++)
                TextRenderer.DrawText(e.Graphics, "  " + _items[i], Font, new Rectangle(0, 22 * (i + 1), Width, 22), Color.Black, TextFormatFlags.VerticalCenter);
        }
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Focused ? Color.DodgerBlue : Color.Gray, ButtonBorderStyle.Solid);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ComboAccessible(this);

    private sealed class ComboAccessible : ControlAccessibleObject
    {
        private readonly LegacyCombo _owner;
        public ComboAccessible(LegacyCombo owner) : base(owner) { _owner = owner; }

        public override AccessibleRole Role => AccessibleRole.ComboBox;
        public override string Value { get => _owner._items[0]; set { } }
        public override string DefaultAction => "";
        public override void DoDefaultAction() { }

        public override AccessibleStates State
        {
            get
            {
                var s = AccessibleStates.Focusable;
                if (_owner.Focused) s |= AccessibleStates.Focused;
                if (_owner._reportsState) s |= _owner.IsOpen ? Msaa.Expanded : Msaa.Collapsed;
                return s;
            }
        }
    }
}

// ── Raw accessible box: MSAA name / value exactly as given ───────────────────────────────────

// Publishes Name / Value through IAccessible untouched — no WinForms text handling in between —
// so the fixture can hand UIA a 5000-character value (stock edits cap ValuePattern at 4096) or
// control characters XML 1.0 cannot carry.
internal sealed class RawAccessibleBox : Control
{
    private readonly string _name;
    private readonly string _value;

    public RawAccessibleBox(string automationId, string name, string value)
    {
        Name = automationId;
        _name = name;
        _value = value;
        Size = new Size(160, 18);
        BackColor = Color.WhiteSmoke;
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new RawAccessible(this);

    private sealed class RawAccessible : ControlAccessibleObject
    {
        private readonly RawAccessibleBox _owner;
        public RawAccessible(RawAccessibleBox owner) : base(owner) { _owner = owner; }
        public override AccessibleRole Role => AccessibleRole.Text;
        public override string Name { get => _owner._name; set { } }
        public override string Value { get => _owner._value; set { } }
    }
}

// ── Main form ─────────────────────────────────────────────────────────────────────────────────

internal sealed class MainForm : Form
{
    private readonly Label _status = new() { Name = "statusLabel", Dock = DockStyle.Bottom, Height = 24 };
    private readonly DataGridView _grid;
    private readonly LegacyOutline _outline = new() { Dock = DockStyle.Fill };
    private readonly TreeView _tree;

    // Customer reproduction (driver commit 99f531c): a plain WinForms DropDownList ComboBox
    // exposes no ExpandCollapsePattern over raw UIA3, so `windows: expand` relies on the
    // client's ALT+Down keyboard fallback. DropDown / DropDownClosed drive the status label.
    private readonly ComboBox _combo = new()
    {
        Name = "cmbCategories",
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 160,
    };
    private bool _comboOpen;
    private readonly LegacyCombo _legacyComboState = new("legacyComboState", reportsState: true);
    private readonly LegacyCombo _legacyComboNoState = new("legacyComboNoState", reportsState: false);

    // Stock TrackBar: UIA's trackbar proxy exposes RangeValuePattern (setRangeValue).
    private double _wpfSliderValue = 3;
    private readonly TrackBar _slider = new() { Name = "volumeSlider", Minimum = 0, Maximum = 10, Value = 2, Width = 160 };

    public MainForm()
    {
        Text = "MSAA Legacy Controls";
        ClientSize = new Size(1000, 640);

        _grid = BuildGrid();
        _tree = BuildTree();

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        layout.Controls.Add(_grid, 0, 0);
        layout.Controls.Add(_outline, 1, 0);
        var bottomLeft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        bottomLeft.Controls.Add(BuildSecrets());
        bottomLeft.Controls.Add(BuildCombo());
        bottomLeft.Controls.Add(BuildMisc());
        layout.Controls.Add(bottomLeft, 0, 1);
        layout.Controls.Add(_tree, 1, 1);

        Controls.Add(layout);
        Controls.Add(_status);

        _grid.SelectionChanged += (_, _) => UpdateStatus();
        _outline.StateChanged += UpdateStatus;
        _tree.AfterExpand += (_, _) => UpdateStatus();
        _tree.AfterCollapse += (_, _) => UpdateStatus();
        Shown += (_, _) => { _grid.ClearSelection(); UpdateStatus(); };
    }

    private static DataGridView BuildGrid()
    {
        // Stock .NET Framework DataGridView: UIA sees it only through the MSAA Proxy.
        // Cell accName is "<column> Row <n>", accValue is the content.
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AccessibleName = "serversGrid",
            AllowUserToAddRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            // CellSelect: row-level accSelect has no effect, cell-level works — gives the
            // driver's select both a positive and a negative case.
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            MultiSelect = false,
        };
        grid.Columns.Add("host", "Host");
        grid.Columns.Add("role", "Role");
        grid.Columns.Add("region", "Region");
        grid.Columns.Add("status", "Status");
        grid.Rows.Add("web-01", "Web", "US East", "Healthy");
        grid.Rows.Add("db-01", "Database", "US East", "Degraded");
        grid.Rows.Add("api-03", "API", "EU West", "Healthy");
        grid.Rows.Add("queue-01", "Queue", "EU West", "Offline");
        return grid;
    }

    private static TreeView BuildTree()
    {
        // Stock TreeView (SysTreeView32): UIA's native TreeView proxy, real ExpandCollapsePattern.
        // Regression contrast for the MSAA-only outline.
        var tree = new TreeView { Dock = DockStyle.Fill, AccessibleName = "nativeTree" };
        var a = new TreeNode("Datacenters");
        a.Nodes.Add("Frankfurt");
        a.Nodes.Add("Virginia");
        tree.Nodes.Add(a);
        return tree;
    }

    private static Control BuildSecrets()
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
        panel.Controls.Add(new Label { Text = "PIN (legacy control)", AutoSize = true });
        panel.Controls.Add(new LegacyPinBox());
        panel.Controls.Add(new Label { Text = "Password (stock TextBox)", AutoSize = true });
        panel.Controls.Add(new TextBox { AccessibleName = "passwordBox", UseSystemPasswordChar = true, Text = "hunter2", Width = 120 });
        return panel;
    }

    private Control BuildCombo()
    {
        _combo.Items.AddRange(new object[] { "Books", "Electronics", "Groceries", "Toys" });
        _combo.SelectedIndex = 0;
        _combo.DropDown += (_, _) => { _comboOpen = true; UpdateStatus(); };
        _combo.DropDownClosed += (_, _) => { _comboOpen = false; UpdateStatus(); };
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
        panel.Controls.Add(new Label { Text = "Category (stock ComboBox)", AutoSize = true });
        panel.Controls.Add(_combo);
        panel.Controls.Add(new Label { Text = "Legacy combo (reports state)", AutoSize = true });
        panel.Controls.Add(_legacyComboState);
        panel.Controls.Add(new Label { Text = "Legacy combo (no state)", AutoSize = true });
        panel.Controls.Add(_legacyComboNoState);
        _legacyComboState.OpenChanged += UpdateStatus;
        _legacyComboNoState.OpenChanged += UpdateStatus;
        return panel;
    }

    private Control BuildMisc()
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
        panel.Controls.Add(new Label { Text = "Volume (stock TrackBar)", AutoSize = true });
        panel.Controls.Add(_slider);
        _slider.ValueChanged += (_, _) => UpdateStatus();
        // 5000-character value: page source / XPath cap it at 4096, getAttribute returns all.
        panel.Controls.Add(new RawAccessibleBox("longValueBox", "longValueBox", new string('y', 5000)));
        // Control characters in name and value: page source must stay valid XML.
        panel.Controls.Add(new RawAccessibleBox("ctrlCharBox", "Bad\u0001Name", "Bad\u0002Value"));
        panel.Controls.Add(BuildWpfHost());
        return panel;
    }

    // WPF content hosted in this WinForms window. WPF has its own UIA provider: its Slider
    // exposes a real RangeValuePattern (setRangeValue), and WPF elements carry no
    // LegacyIAccessible values in page source (native UIA — see the driver spec).
    private Control BuildWpfHost()
    {
        var slider = new System.Windows.Controls.Slider { Minimum = 0, Maximum = 10, Value = 3, Width = 150 };
        System.Windows.Automation.AutomationProperties.SetAutomationId(slider, "wpfSlider");
        slider.ValueChanged += (_, _) => { _wpfSliderValue = slider.Value; UpdateStatus(); };
        var button = new System.Windows.Controls.Button { Content = "WPF button" };
        System.Windows.Automation.AutomationProperties.SetAutomationId(button, "wpfButton");
        var stack = new System.Windows.Controls.StackPanel();
        stack.Children.Add(slider);
        stack.Children.Add(button);
        return new System.Windows.Forms.Integration.ElementHost { Width = 170, Height = 60, Child = stack };
    }

    private void UpdateStatus()
    {
        var cell = _grid.SelectedCells.Count > 0 ? _grid.SelectedCells[0] : null;
        var gridText = cell == null
            ? "none"
            : $"{_grid.Rows[cell.RowIndex].Cells[0].Value}/{_grid.Columns[cell.ColumnIndex].HeaderText}";
        var expanded = string.Join(",", _outline.Groups.Where(g => g.Expanded).Select(g => g.Value.Replace("Region: ", "")));
        var treeText = _tree.Nodes[0].IsExpanded ? "expanded" : "collapsed";
        _status.Text = $"Grid: {gridText} | Outline selected: {_outline.SelectedItem ?? "none"} | Outline expanded: {expanded} | Tree: {treeText} | Combo: {(_comboOpen ? "open" : "closed")} | LegacyComboState: {(_legacyComboState.IsOpen ? "open" : "closed")} | LegacyComboNoState: {(_legacyComboNoState.IsOpen ? "open" : "closed")} | Slider: {_slider.Value} | WpfSlider: {_wpfSliderValue:0.##}";
    }
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
