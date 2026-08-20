#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Windows;
using DevExpress.Xpf.Grid;

namespace WpfDevExpressLightweight;

// Investigation fixture, not a permanent regression test yet — reproduces the
// DevExpress.Xpf.Grid.LightweightCellEditor rendering mode a real customer hit (large,
// virtualized GridControl, default/auto-generated TextColumn editors, cells outside the current
// focus/edit state). The earlier WPF probe (test-apps/wpf-datagrid-template/) only exercised a
// DataGridTemplateColumn's CellTemplate, which DevExpress's own docs+behavior treat differently
// from a column's default (non-templated) editor — a small in-memory ObservableCollection with a
// custom DataTemplate never actually engages lightweight-editor virtualization. This fixture uses
// a large row count and the GridControl's default (non-templated, auto-generated) columns instead,
// which is what makes DevExpress switch unfocused/off-screen cells to the lightweight editor path
// for render performance.

internal sealed class RowItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public string Notes { get; set; } = "";
}

internal sealed class MainWindow : Window
{
    private static readonly string[] StatusValues = { "Healthy", "Degraded", "Offline" };

    public GridControl Grid { get; }

    public MainWindow()
    {
        Title = "WPF DevExpress Lightweight Cell Fixture";
        Width = 700;
        Height = 500;

        // Large enough that TableView virtualizes rows/cells, so most rows are never focused or
        // edited -- the condition under which DevExpress renders cells via LightweightCellEditor
        // instead of a full real editor.
        var rows = new ObservableCollection<RowItem>();
        for (int i = 1; i <= 5000; i++)
        {
            rows.Add(new RowItem
            {
                Id = i,
                Name = $"Row {i}",
                Status = StatusValues[i % StatusValues.Length],
                Notes = $"Notes for row {i}",
            });
        }

        Grid = new GridControl
        {
            Name = "MainGrid",
            ItemsSource = rows,
            AutoGenerateColumns = AutoGenerateColumnsMode.None,
        };
        Grid.Columns.Add(new GridColumn { FieldName = nameof(RowItem.Id), Header = "Id" });
        Grid.Columns.Add(new GridColumn { FieldName = nameof(RowItem.Name), Header = "Name" });
        Grid.Columns.Add(new GridColumn { FieldName = nameof(RowItem.Status), Header = "Status" });
        Grid.Columns.Add(new GridColumn { FieldName = nameof(RowItem.Notes), Header = "Notes" });

        var tableView = new TableView
        {
            Name = "MainTableView",
            AutoWidth = true,
            ShowGroupPanel = false,
        };
        Grid.View = tableView;

        Content = Grid;
    }
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new System.Windows.Application();
        app.Run(new MainWindow());
    }
}
