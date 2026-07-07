// Copyright (c) 2026 Theodoros Bebekis
// Licensed under the MIT License.

namespace Demo00.GroupGrid;

public partial class MainWindow : Window
{
    // ● private fields
    private bool fIsWindowInitialized;

    // ● private
    /// <summary>
    /// Initializes the window after it is opened.
    /// </summary>
    private void WindowInitialize()
    {
        SourceComboBox.SelectedIndex = 0;
        ApplySelectedSource();
    }
    /// <summary>
    /// Adds the demo columns.
    /// </summary>
    private void AddColumns()
    {
        Grid.Columns.Add(new GroupGridTextColumn { Name = "Source", Header = "Source", Width = 110 });
        Grid.Columns.Add(new GroupGridLookupColumn { Name = "CustomerId", Header = "Customer", Width = 170, ValueType = typeof(int), LookupItemsSource = CreateCustomers(), DisplayMember = "Name", ValueMember = "Id" });
        Grid.Columns.Add(new GroupGridTextColumn { Name = "Region", Header = "Region", Width = 120 });
        Grid.Columns.Add(new GroupGridDateColumn { Name = "OrderDate", Header = "Order Date", Width = 120, DisplayFormat = "yyyy-MM-dd", EditFormat = "yyyy-MM-dd" });
        Grid.Columns.Add(new GroupGridNumberColumn { Name = "Quantity", Header = "Qty", Width = 80, ValueType = typeof(int), GroupSummary = GroupGridAggregateKind.Sum, TotalSummary = GroupGridAggregateKind.Sum });
        Grid.Columns.Add(new GroupGridNumberColumn { Name = "Amount", Header = "Amount", Width = 120, ValueType = typeof(decimal), DisplayFormat = "N2", GroupSummary = GroupGridAggregateKind.Sum, TotalSummary = GroupGridAggregateKind.Sum });
        Grid.Columns.Add(new GroupGridCheckBoxColumn { Name = "IsPaid", Header = "Paid", Width = 80 });
        Grid.Columns.Add(new GroupGridTextColumn { Name = "Notes", Header = "Notes", Width = 900 });
    }
    /// <summary>
    /// Applies the selected demo data source to the grid.
    /// </summary>
    private void ApplySelectedSource()
    {
        Grid.ItemsSource = null;
        Grid.Columns.Clear();
        AddColumns();
        Grid.ItemsSource = CreateSelectedItemsSource();
        Grid.GroupColumn(Grid.Columns[2]);
        Grid.SetFirstVisibleNodeIndex(0);
        Grid.SetHorizontalOffset(0);
        Grid.SetCurrentCell(0, Grid.Columns[1]);
        Grid.SelectCurrentCell();
    }
    /// <summary>
    /// Creates the selected demo item source.
    /// </summary>
    /// <returns>The selected item source.</returns>
    private object CreateSelectedItemsSource()
    {
        switch (SourceComboBox.SelectedIndex)
        {
            case 1:
                return CreateTable("DataTable");
            case 2:
                return CreateTable("DataView").DefaultView;
        }

        return CreateRows("POCO List");
    }
    /// <summary>
    /// Creates the demo rows.
    /// </summary>
    /// <returns>The demo rows.</returns>
    private List<SalesRow> CreateRows(string Source)
    {
        List<SalesRow> Result = new();
        string[] Regions = { "North", "South", "East", "West" };
        DateTime StartDate = new(2026, 1, 5);

        for (int Index = 0; Index < 80; Index++)
        {
            int Quantity = 1 + (Index % 9);
            Result.Add(new SalesRow
            {
                Source = Source,
                CustomerId = 1 + (Index % 8),
                Region = Regions[Index % Regions.Length],
                OrderDate = StartDate.AddDays(Index),
                Quantity = Quantity,
                Amount = Quantity * (18.75m + (Index % 7)),
                IsPaid = Index % 3 != 0,
                Notes = string.Format("Order line {0:000} generated for horizontal scrolling checks.", Index + 1),
            });
        }

        return Result;
    }
    /// <summary>
    /// Creates the demo rows as a data table.
    /// </summary>
    /// <returns>The demo data table.</returns>
    private DataTable CreateTable(string Source)
    {
        DataTable Result = new("Sales");
        Result.Columns.Add("Source", typeof(string));
        Result.Columns.Add("CustomerId", typeof(int));
        Result.Columns.Add("Region", typeof(string));
        Result.Columns.Add("OrderDate", typeof(DateTime));
        Result.Columns.Add("Quantity", typeof(int));
        Result.Columns.Add("Amount", typeof(decimal));
        Result.Columns.Add("IsPaid", typeof(bool));
        Result.Columns.Add("Notes", typeof(string));

        foreach (SalesRow Row in CreateRows(Source))
            Result.Rows.Add(Row.Source, Row.CustomerId, Row.Region, Row.OrderDate, Row.Quantity, Row.Amount, Row.IsPaid, Row.Notes);

        return Result;
    }
    /// <summary>
    /// Creates demo lookup customers.
    /// </summary>
    /// <returns>The demo customers.</returns>
    private List<CustomerLookupRow> CreateCustomers()
    {
        return new List<CustomerLookupRow>
        {
            new() { Id = 1, Name = "Alpha" },
            new() { Id = 2, Name = "Beacon" },
            new() { Id = 3, Name = "Canyon" },
            new() { Id = 4, Name = "Delta" },
            new() { Id = 5, Name = "Eclipse" },
            new() { Id = 6, Name = "Falcon" },
            new() { Id = 7, Name = "Galaxy" },
            new() { Id = 8, Name = "Harbor" },
            new() { Id = 9, Name = "Ion" },
            new() { Id = 10, Name = "Jupiter" },
            new() { Id = 11, Name = "Keystone" },
            new() { Id = 12, Name = "Lagoon" },
            new() { Id = 13, Name = "Meridian" },
            new() { Id = 14, Name = "Nimbus" },
            new() { Id = 15, Name = "Orion" },
            new() { Id = 16, Name = "Pioneer" },
            new() { Id = 17, Name = "Quartz" },
            new() { Id = 18, Name = "River" },
            new() { Id = 19, Name = "Summit" },
            new() { Id = 20, Name = "Timber" },
            new() { Id = 21, Name = "Union" },
            new() { Id = 22, Name = "Vertex" },
            new() { Id = 23, Name = "Willow" },
            new() { Id = 24, Name = "Xenon" },
            new() { Id = 25, Name = "Yonder" },
            new() { Id = 26, Name = "Zenith" },
            new() { Id = 27, Name = "Atlas" },
            new() { Id = 28, Name = "Boreal" },
            new() { Id = 29, Name = "Cobalt" },
            new() { Id = 30, Name = "Drift" },
            new() { Id = 31, Name = "Everest" },
            new() { Id = 32, Name = "Frontier" },
        };
    }
    /// <summary>
    /// Handles data source selection changes.
    /// </summary>
    /// <param name="Sender">The event sender.</param>
    /// <param name="Args">The event arguments.</param>
    private void SourceComboBox_SelectionChanged(object Sender, SelectionChangedEventArgs Args)
    {
        if (!fIsWindowInitialized)
            return;

        ApplySelectedSource();
    }
    /// <summary>
    /// Clears the grid item source.
    /// </summary>
    /// <param name="Sender">The event sender.</param>
    /// <param name="Args">The event arguments.</param>
    private void ClearButton_Click(object Sender, Avalonia.Interactivity.RoutedEventArgs Args)
    {
        Grid.ItemsSource = null;
        Grid.ClearCurrentCell();
        Grid.ClearSelection();
        Grid.SetFirstVisibleNodeIndex(0);
        Grid.SetHorizontalOffset(0);
    }

    // ● protected
    /// <summary>
    /// Handles the window opened event.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (fIsWindowInitialized)
            return;

        WindowInitialize();
        fIsWindowInitialized = true;
        //LogBox.AppendLine("Application Started.");
    }


    // ● constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}

/// <summary>
/// Represents a demo sales row.
/// </summary>
public class SalesRow
{
    // ● constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="SalesRow"/> class.
    /// </summary>
    public SalesRow()
    {
    }

    // ● properties
    /// <summary>
    /// Gets or sets the demo source name.
    /// </summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the customer id.
    /// </summary>
    public int CustomerId { get; set; }
    /// <summary>
    /// Gets or sets the region.
    /// </summary>
    public string Region { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the order date.
    /// </summary>
    public DateTime OrderDate { get; set; }
    /// <summary>
    /// Gets or sets the quantity.
    /// </summary>
    public int Quantity { get; set; }
    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether the order is paid.
    /// </summary>
    public bool IsPaid { get; set; }
    /// <summary>
    /// Gets or sets the row notes.
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Represents a demo lookup customer.
/// </summary>
public class CustomerLookupRow
{
    // ● constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerLookupRow"/> class.
    /// </summary>
    public CustomerLookupRow()
    {
    }

    // ● properties
    /// <summary>
    /// Gets or sets the lookup id.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Gets or sets the lookup display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
