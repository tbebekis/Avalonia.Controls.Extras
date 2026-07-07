// Copyright (c) 2026 Theodoros Bebekis
// Licensed under the MIT License.

namespace Avalonia.Controls.Extras.Grid.Tests;

/// <summary>
/// Tests public group grid control APIs that do not require rendering.
/// </summary>
public class GroupGridControlTests
{
    // ● private methods
    ObservableCollection<GridTestRow> CreateRows()
    {
        return new ObservableCollection<GridTestRow>
        {
            new() { Category = "A", Name = "Alpha", Quantity = 2, Amount = 10m },
            new() { Category = "A", Name = "Beta", Quantity = 5, Amount = 25m },
            new() { Category = "B", Name = "Gamma", Quantity = 1, Amount = 7m },
        };
    }
    GroupGrid CreateGrid(ObservableCollection<GridTestRow> Rows)
    {
        GroupGrid Result = new()
        {
            DataAdapter = new GroupGridListDataAdapter<GridTestRow>(Rows),
        };
        Result.Columns.Add(new GroupGridTextColumn { Name = nameof(GridTestRow.Category), Header = "Category", Width = 120 });
        Result.Columns.Add(new GroupGridTextColumn { Name = nameof(GridTestRow.Name), Header = "Name", Width = 180 });
        Result.Columns.Add(new GroupGridNumberColumn { Name = nameof(GridTestRow.Quantity), Header = "Quantity", Width = 90, TotalSummary = GroupGridAggregateKind.Sum });
        Result.Columns.Add(new GroupGridNumberColumn { Name = nameof(GridTestRow.Amount), Header = "Amount", Width = 100, TotalSummary = GroupGridAggregateKind.Sum });
        return Result;
    }
    GroupGridColumn Column(GroupGrid Grid, string Name)
    {
        return Grid.Columns.First(Item => Item.Name == Name);
    }
    string TempPath(string Extension)
    {
        return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "." + Extension);
    }

    // ● tests
    /// <summary>
    /// Verifies that settings capture layout, grouping, filters, summaries, sorting, and visibility state.
    /// </summary>
    [Fact]
    public void CreateSettings_WithConfiguredGrid_CapturesLayoutState()
    {
        GroupGrid Grid = CreateGrid(CreateRows());
        GroupGridColumn CategoryColumn = Column(Grid, nameof(GridTestRow.Category));
        GroupGridColumn NameColumn = Column(Grid, nameof(GridTestRow.Name));
        GroupGridColumn QuantityColumn = Column(Grid, nameof(GridTestRow.Quantity));
        Grid.IsToolBarVisible = false;
        Grid.IsEditButtonVisible = true;
        Grid.Engine.GroupColumn(CategoryColumn);
        Grid.Engine.SetColumnFilter(NameColumn, "ta");
        Grid.Engine.ToggleSort(QuantityColumn);
        QuantityColumn.Width = 140;
        QuantityColumn.GroupSummary = GroupGridAggregateKind.Sum;

        GroupGridSettings Settings = Grid.CreateSettings("Configured");

        Assert.Equal("Configured", Settings.Name);
        Assert.False(Settings.IsToolBarVisible);
        Assert.True(Settings.IsEditButtonVisible);
        Assert.Equal(nameof(GridTestRow.Quantity), Settings.SortColumnName);
        Assert.Equal(GroupGridSortDirection.Ascending, Settings.SortDirection);
        Assert.Equal(0, Settings.Columns.First(Item => Item.Name == nameof(GridTestRow.Category)).GroupIndex);
        Assert.Equal("ta", Settings.Columns.First(Item => Item.Name == nameof(GridTestRow.Name)).FilterText);
        Assert.Equal(140, Settings.Columns.First(Item => Item.Name == nameof(GridTestRow.Quantity)).Width);
        Assert.Equal(GroupGridAggregateKind.Sum, Settings.Columns.First(Item => Item.Name == nameof(GridTestRow.Quantity)).GroupSummary);
    }
    /// <summary>
    /// Verifies that applying settings restores layout state.
    /// </summary>
    [Fact]
    public void ApplySettings_WithSavedSettings_RestoresLayoutState()
    {
        GroupGrid Source = CreateGrid(CreateRows());
        GroupGridColumn SourceCategoryColumn = Column(Source, nameof(GridTestRow.Category));
        GroupGridColumn SourceNameColumn = Column(Source, nameof(GridTestRow.Name));
        GroupGridColumn SourceQuantityColumn = Column(Source, nameof(GridTestRow.Quantity));
        Source.IsColumnHeadersVisible = false;
        Source.Engine.GroupColumn(SourceCategoryColumn);
        Source.Engine.SetColumnFilter(SourceNameColumn, "ta");
        Source.Engine.ToggleSort(SourceQuantityColumn);
        SourceQuantityColumn.Width = 140;

        GroupGridSettings Settings = Source.CreateSettings();
        GroupGrid Target = CreateGrid(CreateRows());

        Target.ApplySettings(Settings);

        GroupGridColumn TargetCategoryColumn = Column(Target, nameof(GridTestRow.Category));
        GroupGridColumn TargetNameColumn = Column(Target, nameof(GridTestRow.Name));
        GroupGridColumn TargetQuantityColumn = Column(Target, nameof(GridTestRow.Quantity));
        Assert.False(Target.IsColumnHeadersVisible);
        Assert.True(Target.Engine.IsGroupedColumn(TargetCategoryColumn));
        Assert.Equal("ta", Target.Engine.GetColumnFilter(TargetNameColumn));
        Assert.Same(TargetQuantityColumn, Target.Engine.SortColumn);
        Assert.Equal(GroupGridSortDirection.Ascending, Target.Engine.SortDirection);
        Assert.Equal(140, TargetQuantityColumn.Width);
    }
    /// <summary>
    /// Verifies formatted settings save/load round-trips through a full file path.
    /// </summary>
    [Fact]
    public void SaveSettingsAndLoadSettings_WithFullFilePath_RoundTripsSettings()
    {
        string FilePath = TempPath("json");
        try
        {
            GroupGrid Source = CreateGrid(CreateRows());
            Source.IsFilterPanelVisible = false;
            Source.Engine.ToggleSort(Column(Source, nameof(GridTestRow.Name)));
            Source.SaveSettings(FilePath, "FileSettings");
            string Json = File.ReadAllText(FilePath);

            GroupGrid Target = CreateGrid(CreateRows());
            bool Loaded = Target.LoadSettings(FilePath);

            Assert.Contains(Environment.NewLine, Json);
            Assert.True(Loaded);
            Assert.False(Target.IsFilterPanelVisible);
            Assert.Equal("FileSettings", JsonDocument.Parse(Json).RootElement.GetProperty("Name").GetString());
            Assert.Same(Column(Target, nameof(GridTestRow.Name)), Target.Engine.SortColumn);
        }
        finally
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
    /// <summary>
    /// Verifies export snapshot content from the current visible projection.
    /// </summary>
    [Fact]
    public void CreateExportSnapshot_WithFilteredGrid_ReturnsVisibleDataRows()
    {
        GroupGrid Grid = CreateGrid(CreateRows());
        Grid.Engine.SetColumnFilter(Column(Grid, nameof(GridTestRow.Name)), "a");

        GroupGridExportSnapshot Snapshot = Grid.CreateExportSnapshot();

        Assert.Equal(4, Snapshot.Columns.Count);
        Assert.Equal(3, Snapshot.GetDataRows().Count);
        Assert.Equal("Alpha", Snapshot.GetDataRows()[0].Cells.First(Cell => Cell.Column.Name == nameof(GridTestRow.Name)).Text);
        Assert.Equal("sum=8", Snapshot.TotalSummaryCells.First(Cell => Cell.Column.Name == nameof(GridTestRow.Quantity)).Text);
    }
    /// <summary>
    /// Verifies the public Id-column visibility policy.
    /// </summary>
    [Fact]
    public void AreIdColumnsVisible_WithIdColumnNames_HidesAndShowsIdColumns()
    {
        GroupGrid Grid = new();
        GroupGridColumn IdColumn = new GroupGridTextColumn { Name = "Id" };
        GroupGridColumn CustomerIdColumn = new GroupGridTextColumn { Name = "CustomerId" };
        GroupGridColumn LowerCaseIdColumn = new GroupGridTextColumn { Name = "orderid" };
        GroupGridColumn NameColumn = new GroupGridTextColumn { Name = "Name" };
        Grid.Columns.Add(IdColumn);
        Grid.Columns.Add(CustomerIdColumn);
        Grid.Columns.Add(NameColumn);

        Grid.AreIdColumnsVisible = false;
        Grid.Columns.Add(LowerCaseIdColumn);

        Assert.False(IdColumn.IsVisible);
        Assert.False(CustomerIdColumn.IsVisible);
        Assert.False(LowerCaseIdColumn.IsVisible);
        Assert.True(NameColumn.IsVisible);

        Grid.AreIdColumnsVisible = true;

        Assert.True(IdColumn.IsVisible);
        Assert.True(CustomerIdColumn.IsVisible);
        Assert.True(LowerCaseIdColumn.IsVisible);
        Assert.True(NameColumn.IsVisible);
    }
    /// <summary>
    /// Verifies by-name column visibility API.
    /// </summary>
    [Fact]
    public void SetColumnVisibleAndSetColumnsVisible_WithFieldNames_ChangeVisibility()
    {
        GroupGrid Grid = CreateGrid(CreateRows());

        Assert.True(Grid.SetColumnVisible(nameof(GridTestRow.Name), false));
        Assert.False(Column(Grid, nameof(GridTestRow.Name)).IsVisible);
        Assert.True(Grid.SetColumnsVisible(new[] { nameof(GridTestRow.Quantity), nameof(GridTestRow.Amount) }, false));
        Assert.False(Column(Grid, nameof(GridTestRow.Quantity)).IsVisible);
        Assert.False(Column(Grid, nameof(GridTestRow.Amount)).IsVisible);
        Assert.True(Grid.SetColumnsVisible(new[] { nameof(GridTestRow.Name), nameof(GridTestRow.Quantity) }, true));
        Assert.True(Column(Grid, nameof(GridTestRow.Name)).IsVisible);
        Assert.True(Column(Grid, nameof(GridTestRow.Quantity)).IsVisible);
    }
    /// <summary>
    /// Verifies by-name column read-only API.
    /// </summary>
    [Fact]
    public void SetColumnReadOnlyAndSetColumnsReadOnly_WithFieldNames_ChangeReadOnlyState()
    {
        GroupGrid Grid = CreateGrid(CreateRows());

        Assert.True(Grid.SetColumnReadOnly(nameof(GridTestRow.Name), true));
        Assert.True(Column(Grid, nameof(GridTestRow.Name)).IsReadOnly);
        Assert.True(Grid.SetColumnsReadOnly(new[] { nameof(GridTestRow.Quantity), nameof(GridTestRow.Amount) }, true));
        Assert.True(Column(Grid, nameof(GridTestRow.Quantity)).IsReadOnly);
        Assert.True(Column(Grid, nameof(GridTestRow.Amount)).IsReadOnly);
        Assert.True(Grid.SetColumnsReadOnly(new[] { nameof(GridTestRow.Name), nameof(GridTestRow.Quantity) }, false));
        Assert.False(Column(Grid, nameof(GridTestRow.Name)).IsReadOnly);
        Assert.False(Column(Grid, nameof(GridTestRow.Quantity)).IsReadOnly);
    }
}
