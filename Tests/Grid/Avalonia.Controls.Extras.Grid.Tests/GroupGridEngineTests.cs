namespace Avalonia.Controls.Extras.Grid.Tests;

/// <summary>
/// Tests the non-visual group grid engine behavior.
/// </summary>
public class GroupGridEngineTests
{
    // ● private methods
    ObservableCollection<GridTestRow> CreateRows()
    {
        return new ObservableCollection<GridTestRow>
        {
            new() { Category = "A", Name = "Alpha", Quantity = 2, Amount = 10m },
            new() { Category = "A", Name = "Beta", Quantity = 5, Amount = 25m },
            new() { Category = "B", Name = "Gamma", Quantity = 1, Amount = 7m },
            new() { Category = "B", Name = "Delta", Quantity = 8, Amount = 40m },
        };
    }
    GroupGridEngine CreateEngine(ObservableCollection<GridTestRow> Rows)
    {
        GroupGridEngine Result = new();
        Result.Columns.Add(new GroupGridTextColumn { Name = nameof(GridTestRow.Category), Header = "Category" });
        Result.Columns.Add(new GroupGridTextColumn { Name = nameof(GridTestRow.Name), Header = "Name" });
        Result.Columns.Add(new GroupGridNumberColumn { Name = nameof(GridTestRow.Quantity), Header = "Quantity", TotalSummary = GroupGridAggregateKind.Sum });
        Result.Columns.Add(new GroupGridNumberColumn { Name = nameof(GridTestRow.Amount), Header = "Amount", TotalSummary = GroupGridAggregateKind.Sum });
        Result.DataAdapter = new GroupGridListDataAdapter<GridTestRow>(Rows);
        return Result;
    }
    GroupGridColumn Column(GroupGridEngine Engine, string Name)
    {
        return Engine.Columns.First(Item => Item.Name == Name);
    }
    List<int> VisibleDataRowIndexes(GroupGridEngine Engine)
    {
        List<int> Result = new();
        for (int Index = 0; Index < Engine.VisibleNodeCount; Index++)
        {
            GroupGridRowInfo RowInfo = Engine.GetVisibleRowInfo(Index);
            if (RowInfo.IsDataRow)
                Result.Add(RowInfo.RowIndex);
        }

        return Result;
    }

    // ● tests
    /// <summary>
    /// Verifies that the engine projects flat data rows in source order.
    /// </summary>
    [Fact]
    public void RebuildProjection_WithFlatRows_ProjectsDataRowsInSourceOrder()
    {
        ObservableCollection<GridTestRow> Rows = CreateRows();
        GroupGridEngine Engine = CreateEngine(Rows);

        Assert.Equal(4, Engine.VisibleNodeCount);
        Assert.Equal(new[] { 0, 1, 2, 3 }, VisibleDataRowIndexes(Engine));
    }
    /// <summary>
    /// Verifies that grouping creates group and data rows.
    /// </summary>
    [Fact]
    public void GroupColumn_WithCategory_CreatesGroupedProjection()
    {
        ObservableCollection<GridTestRow> Rows = CreateRows();
        GroupGridEngine Engine = CreateEngine(Rows);
        GroupGridColumn CategoryColumn = Column(Engine, nameof(GridTestRow.Category));

        Assert.True(Engine.GroupColumn(CategoryColumn));

        Assert.True(Engine.VisibleNodeCount > Rows.Count);
        Assert.True(Engine.GetVisibleRowInfo(0).IsGroup);
        Assert.Equal("A", Engine.GetVisibleRowInfo(0).Key);
        Assert.Equal(new[] { 0, 1, 2, 3 }, VisibleDataRowIndexes(Engine));
    }
    /// <summary>
    /// Verifies that sorting changes visible data-row order.
    /// </summary>
    [Fact]
    public void ToggleSort_WithQuantity_SortsAscendingAndDescending()
    {
        GroupGridEngine Engine = CreateEngine(CreateRows());
        GroupGridColumn QuantityColumn = Column(Engine, nameof(GridTestRow.Quantity));

        Assert.True(Engine.ToggleSort(QuantityColumn));
        Assert.Equal(GroupGridSortDirection.Ascending, Engine.SortDirection);
        Assert.Equal(new[] { 2, 0, 1, 3 }, VisibleDataRowIndexes(Engine));

        Assert.True(Engine.ToggleSort(QuantityColumn));
        Assert.Equal(GroupGridSortDirection.Descending, Engine.SortDirection);
        Assert.Equal(new[] { 3, 1, 0, 2 }, VisibleDataRowIndexes(Engine));
    }
    /// <summary>
    /// Verifies contains, comparison, and wildcard filters.
    /// </summary>
    [Fact]
    public void SetColumnFilter_WithSupportedFilterText_FiltersProjection()
    {
        GroupGridEngine Engine = CreateEngine(CreateRows());
        GroupGridColumn NameColumn = Column(Engine, nameof(GridTestRow.Name));
        GroupGridColumn QuantityColumn = Column(Engine, nameof(GridTestRow.Quantity));

        Assert.True(Engine.SetColumnFilter(NameColumn, "ta"));
        Assert.Equal(new[] { 1, 3 }, VisibleDataRowIndexes(Engine));

        Assert.True(Engine.SetColumnFilter(QuantityColumn, ">=5"));
        Assert.Equal(new[] { 1, 3 }, VisibleDataRowIndexes(Engine));

        Assert.True(Engine.ClearFilters());
        Assert.True(Engine.SetColumnFilter(NameColumn, "G*"));
        Assert.Equal(new[] { 2 }, VisibleDataRowIndexes(Engine));
    }
    /// <summary>
    /// Verifies total summaries over the projected rows.
    /// </summary>
    [Fact]
    public void GetTotalSummary_WithSumColumn_ReturnsProjectedSum()
    {
        GroupGridEngine Engine = CreateEngine(CreateRows());
        GroupGridColumn QuantityColumn = Column(Engine, nameof(GridTestRow.Quantity));

        GroupGridSummaryValue Summary = Engine.GetTotalSummary(QuantityColumn);

        Assert.Equal(GroupGridAggregateKind.Sum, Summary.AggregateKind);
        Assert.Equal(16m, Convert.ToDecimal(Summary.Value));
    }
}
