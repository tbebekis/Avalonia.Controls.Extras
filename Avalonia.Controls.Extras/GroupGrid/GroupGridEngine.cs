namespace Avalonia.Controls;

/// <summary>
/// Represents the non-visual state, data adapter, columns, and row projection of a group grid.
/// </summary>
public class GroupGridEngine
{
    // ● private fields
    readonly GroupGridNodeProjection fProjection = new();
    readonly ObservableCollection<GroupGridColumn> fGroupColumns = new();
    IGroupGridDataAdapter fDataAdapter;
    GroupGridCell fCurrentCell = GroupGridCell.Empty;
    GroupGridCell fSelectedCell = GroupGridCell.Empty;
    GroupGridCell fEditingCell = GroupGridCell.Empty;
    GroupGridViewport fViewport = GroupGridViewport.Empty;

    // ● private methods
    void Columns_CollectionChanged(object Sender, NotifyCollectionChangedEventArgs Args)
    {
        if (Args.Action == NotifyCollectionChangedAction.Reset)
        {
            fGroupColumns.Clear();
        }
        else if (Args.Action == NotifyCollectionChangedAction.Remove && Args.OldItems != null)
        {
            foreach (GroupGridColumn Column in Args.OldItems)
                fGroupColumns.Remove(Column);
        }
        else if (Args.Action == NotifyCollectionChangedAction.Replace && Args.OldItems != null)
        {
            foreach (GroupGridColumn Column in Args.OldItems)
                if (!Columns.Contains(Column))
                    fGroupColumns.Remove(Column);
        }

        ColumnsChanged?.Invoke(this, EventArgs.Empty);
        CoerceCurrentCell();
        CoerceSelectedCell();
        CoerceEditingCell();
    }
    void GroupColumns_CollectionChanged(object Sender, NotifyCollectionChangedEventArgs Args)
    {
        GroupColumnsChanged?.Invoke(this, EventArgs.Empty);
        RebuildProjection();
        CoerceCurrentCell();
        CoerceSelectedCell();
        CoerceEditingCell();
    }
    void DataAdapter_Changed(object Sender, GroupGridDataChangedEventArgs Args)
    {
        if (Args.Kind == GroupGridDataChangeKind.Reset
            || Args.Kind == GroupGridDataChangeKind.RowAdded
            || Args.Kind == GroupGridDataChangeKind.RowRemoved
            || Args.Kind == GroupGridDataChangeKind.RowMoved
            || Args.Kind == GroupGridDataChangeKind.RowChanged
            || (Args.Kind == GroupGridDataChangeKind.CellChanged && IsGroupedColumnName(Args.ColumnName)))
        {
            RebuildProjection();
            CoerceCurrentCell();
            CoerceSelectedCell();
            CoerceEditingCell();
        }
        else if (Args.Kind == GroupGridDataChangeKind.CellChanged)
        {
            CalculateSummaries();
            SummariesChanged?.Invoke(this, EventArgs.Empty);
        }

        DataChanged?.Invoke(this, Args);
    }
    void SetDataAdapter(IGroupGridDataAdapter Value)
    {
        if (ReferenceEquals(fDataAdapter, Value))
            return;

        if (fDataAdapter != null)
            fDataAdapter.Changed -= DataAdapter_Changed;

        fDataAdapter = Value;

        if (fDataAdapter != null)
            fDataAdapter.Changed += DataAdapter_Changed;

        RebuildProjection();
        CoerceCurrentCell();
        CoerceSelectedCell();
        CoerceEditingCell();
        DataAdapterChanged?.Invoke(this, EventArgs.Empty);
    }
    void CoerceCurrentCell()
    {
        if (fCurrentCell.IsEmpty)
            return;

        if (!IsValidValueCell(fCurrentCell))
            ClearCurrentCell();
    }
    void CoerceSelectedCell()
    {
        if (fSelectedCell.IsEmpty)
            return;

        if (!IsValidValueCell(fSelectedCell))
            ClearSelection();
    }
    void CoerceEditingCell()
    {
        if (fEditingCell.IsEmpty)
            return;

        if (!IsValidEditableCell(fEditingCell))
            CancelEdit();
    }
    bool IsValidValueCell(GroupGridCell Cell)
    {
        return !Cell.IsEmpty
               && fDataAdapter != null
               && Cell.RowIndex >= 0
               && Cell.RowIndex < fDataAdapter.RowCount
               && fProjection.IndexOfRow(Cell.RowIndex) >= 0
               && Columns.Contains(Cell.Column)
               && Cell.Column.IsVisible
               && !fGroupColumns.Contains(Cell.Column);
    }
    bool IsValidEditableCell(GroupGridCell Cell)
    {
        return IsValidValueCell(Cell)
               && !Cell.Column.IsReadOnly
               && CanSetValue(Cell.RowIndex, Cell.Column);
    }
    List<GroupGridColumn> GetVisibleColumnList()
    {
        return Columns.Where(Column => Column.IsVisible).ToList();
    }
    List<GroupGridColumn> GetVisibleValueColumnList()
    {
        return Columns
            .Where(Column => Column.IsVisible && !fGroupColumns.Contains(Column))
            .ToList();
    }
    GroupGridColumn GetColumnAt(IReadOnlyList<GroupGridColumn> Columns, double X, out int ColumnIndex, out double ColumnRight)
    {
        double Left = 0;
        ColumnIndex = -1;
        ColumnRight = 0;

        for (int Index = 0; Index < Columns.Count; Index++)
        {
            GroupGridColumn Column = Columns[Index];
            double Right = Left + Math.Max(Column.MinWidth, Column.Width);
            if (X >= Left && X < Right)
            {
                ColumnIndex = Index;
                ColumnRight = Right;
                return Column;
            }

            Left = Right;
        }

        return null;
    }
    GroupGridHitTestResult CreateBandHitTestResult(double X, double Y, GroupGridBand Band, GroupGridHitTestKind Kind)
    {
        return new GroupGridHitTestResult
        {
            X = X,
            Y = Y,
            Band = Band,
            Kind = Kind,
        };
    }
    GroupGridHitTestResult CreateColumnHitTestResult(double X, double Y, GroupGridBand Band, GroupGridHitTestKind Kind, IReadOnlyList<GroupGridColumn> Columns)
    {
        GroupGridColumn Column = GetColumnAt(Columns, X, out int ColumnIndex, out double ColumnRight);
        if (Column == null)
            return CreateBandHitTestResult(X, Y, Band, Kind);

        GroupGridHitTestKind ResultKind = Kind;
        if (Band == GroupGridBand.ColumnHeader
            && Column.CanUserResize
            && ColumnRight - X <= LayoutMetrics.ColumnResizeHandleWidth)
            ResultKind = GroupGridHitTestKind.ColumnResizer;

        return new GroupGridHitTestResult
        {
            X = X,
            Y = Y,
            Band = Band,
            Kind = ResultKind,
            Column = Column,
            ColumnIndex = ColumnIndex,
        };
    }
    GroupGridHitTestResult CreateBodyHitTestResult(double X, double Y, double BodyTop)
    {
        if (fViewport.IsEmpty || LayoutMetrics.RowHeight <= 0)
            return CreateBandHitTestResult(X, Y, GroupGridBand.Body, GroupGridHitTestKind.BodyRow);

        int ViewportOffset = (int)((Y - BodyTop) / LayoutMetrics.RowHeight);
        int VisibleNodeIndex = fViewport.FirstVisibleNodeIndex + ViewportOffset;
        GroupGridRowInfo RowInfo = GetVisibleRowInfo(VisibleNodeIndex);
        if (RowInfo.IsEmpty)
            return CreateBandHitTestResult(X, Y, GroupGridBand.Body, GroupGridHitTestKind.BodyRow);

        GroupGridHitTestKind Kind = GroupGridHitTestKind.BodyRow;
        GroupGridColumn Column = null;
        int ColumnIndex = -1;

        if (RowInfo.IsGroup)
        {
            double ExpanderLeft = Math.Max(0, RowInfo.Level) * LayoutMetrics.GroupIndentWidth;
            double ExpanderRight = ExpanderLeft + LayoutMetrics.GroupExpanderWidth;
            if (X >= ExpanderLeft && X < ExpanderRight)
                Kind = GroupGridHitTestKind.GroupExpander;
        }
        else if (RowInfo.IsDataRow || RowInfo.IsGroupSummary)
        {
            Column = GetColumnAt(GetVisibleValueColumns(), X, out ColumnIndex, out double _);
            if (Column != null)
                Kind = GroupGridHitTestKind.BodyCell;
        }

        return new GroupGridHitTestResult
        {
            X = X,
            Y = Y,
            Band = GroupGridBand.Body,
            Kind = Kind,
            VisibleNodeIndex = VisibleNodeIndex,
            RowKind = RowInfo.Kind,
            RowIndex = RowInfo.RowIndex,
            Column = Column,
            ColumnIndex = ColumnIndex,
        };
    }
    List<GroupGridNode> GetVisibleDataRowNodes()
    {
        return fProjection.VisibleNodes
            .Where(Node => Node.IsDataRow)
            .ToList();
    }
    GroupGridColumn GetFirstValueColumn()
    {
        return GetVisibleValueColumnList().FirstOrDefault();
    }
    GroupGridColumn GetLastValueColumn()
    {
        return GetVisibleValueColumnList().LastOrDefault();
    }
    int IndexOfVisibleDataRow(IReadOnlyList<GroupGridNode> Rows, int RowIndex)
    {
        for (int Index = 0; Index < Rows.Count; Index++)
            if (Rows[Index].RowIndex == RowIndex)
                return Index;

        return -1;
    }
    bool MoveCurrentToDataRowColumn(GroupGridNode RowNode, GroupGridColumn Column)
    {
        return RowNode != null && Column != null && SetCurrentCell(RowNode.RowIndex, Column);
    }
    bool IsGroupedColumnName(string ColumnName)
    {
        if (string.IsNullOrWhiteSpace(ColumnName))
            return false;

        foreach (GroupGridColumn Column in fGroupColumns)
            if (string.Equals(Column.Name, ColumnName, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
    GroupGridRowInfo CreateRowInfo(GroupGridNode Node, int VisibleNodeIndex)
    {
        if (Node == null)
            return GroupGridRowInfo.Empty;

        GroupGridRowKind Kind = GroupGridRowKind.None;
        if (Node.IsDataRow)
            Kind = GroupGridRowKind.DataRow;
        else if (Node.IsGroup)
            Kind = GroupGridRowKind.Group;
        else if (Node.IsGroupSummary)
            Kind = GroupGridRowKind.GroupSummary;

        return new GroupGridRowInfo(Kind, VisibleNodeIndex, Node.RowIndex, Node.Level, Node.Column, Node.Key, Node.Row, Node.IsExpanded);
    }
    IEnumerable<GroupGridNode> EnumerateDataRows(GroupGridNode Node)
    {
        if (Node == null)
            yield break;

        if (Node.IsDataRow)
        {
            yield return Node;
            yield break;
        }

        foreach (GroupGridNode Child in Node.Children)
            foreach (GroupGridNode DataRowNode in EnumerateDataRows(Child))
                yield return DataRowNode;
    }
    object CalculateSummaryValue(IEnumerable<GroupGridNode> DataRowNodes, GroupGridColumn Column, GroupGridAggregateKind AggregateKind)
    {
        List<GroupGridNode> Nodes = DataRowNodes.ToList();

        if (AggregateKind == GroupGridAggregateKind.Count)
            return Nodes.Count;

        List<object> Values = Nodes
            .Select(Node => GetValue(Node.RowIndex, Column))
            .Where(Value => Value != null && Value != DBNull.Value)
            .ToList();

        if (Values.Count == 0)
            return null;

        switch (AggregateKind)
        {
            case GroupGridAggregateKind.Sum:
                return Values.Sum(Value => Convert.ToDecimal(Value, CultureInfo.CurrentCulture));
            case GroupGridAggregateKind.Min:
                return Values
                    .OfType<IComparable>()
                    .OrderBy(Value => Value)
                    .FirstOrDefault();
            case GroupGridAggregateKind.Max:
                return Values
                    .OfType<IComparable>()
                    .OrderByDescending(Value => Value)
                    .FirstOrDefault();
            case GroupGridAggregateKind.Average:
                return Values.Average(Value => Convert.ToDecimal(Value, CultureInfo.CurrentCulture));
        }

        return null;
    }
    void CalculateGroupSummaries(GroupGridNode Node)
    {
        if (Node == null)
            return;

        foreach (GroupGridNode Child in Node.Children)
            CalculateGroupSummaries(Child);

        if (!Node.IsGroup)
            return;

        List<GroupGridNode> DataRowNodes = EnumerateDataRows(Node).ToList();
        foreach (GroupGridColumn Column in Columns)
            if (Column.GroupSummary != GroupGridAggregateKind.None)
            {
                object Value = CalculateSummaryValue(DataRowNodes, Column, Column.GroupSummary);
                Node.SetSummary(new GroupGridSummaryValue(Column, Column.GroupSummary, Value));
            }
    }
    void CalculateTotalSummaries()
    {
        List<GroupGridNode> DataRowNodes = EnumerateDataRows(fProjection.Root).ToList();
        foreach (GroupGridColumn Column in Columns)
            if (Column.TotalSummary != GroupGridAggregateKind.None)
            {
                object Value = CalculateSummaryValue(DataRowNodes, Column, Column.TotalSummary);
                fProjection.Root.SetSummary(new GroupGridSummaryValue(Column, Column.TotalSummary, Value));
            }
    }
    void CalculateSummaries()
    {
        CalculateGroupSummaries(fProjection.Root);
        CalculateTotalSummaries();
    }

    // ● constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupGridEngine"/> class.
    /// </summary>
    public GroupGridEngine()
    {
        Columns.CollectionChanged += Columns_CollectionChanged;
        fGroupColumns.CollectionChanged += GroupColumns_CollectionChanged;
    }

    // ● public methods
    /// <summary>
    /// Rebuilds the root-node and visible-node projection from the current data adapter.
    /// </summary>
    public void RebuildProjection()
    {
        fProjection.Rebuild(fDataAdapter, fGroupColumns);
        CalculateSummaries();
        VisibleNodesChanged?.Invoke(this, EventArgs.Empty);
        SummariesChanged?.Invoke(this, EventArgs.Empty);
    }
    /// <summary>
    /// Clears the current cell.
    /// </summary>
    public void ClearCurrentCell()
    {
        SetCurrentCell(GroupGridCell.Empty);
    }
    /// <summary>
    /// Clears the selected cell.
    /// </summary>
    public void ClearSelection()
    {
        SetSelectedCell(GroupGridCell.Empty);
    }
    /// <summary>
    /// Sets the current cell.
    /// </summary>
    /// <param name="Cell">The current cell.</param>
    /// <returns>True if the current cell changed; otherwise, false.</returns>
    public bool SetCurrentCell(GroupGridCell Cell)
    {
        if (Cell.IsEmpty)
            Cell = GroupGridCell.Empty;

        if (Cell == fCurrentCell)
            return false;

        if (!Cell.IsEmpty && !IsValidValueCell(Cell))
            return false;

        int OldRowIndex = fCurrentCell.RowIndex;
        fCurrentCell = Cell;
        CurrentCellChanged?.Invoke(this, EventArgs.Empty);
        if (OldRowIndex != fCurrentCell.RowIndex)
            CurrentRowChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
    /// <summary>
    /// Sets the current cell by row index and column.
    /// </summary>
    /// <param name="RowIndex">The adapter row index.</param>
    /// <param name="Column">The grid column.</param>
    /// <returns>True if the current cell changed; otherwise, false.</returns>
    public bool SetCurrentCell(int RowIndex, GroupGridColumn Column) => SetCurrentCell(new GroupGridCell(RowIndex, Column));
    /// <summary>
    /// Sets the selected cell.
    /// </summary>
    /// <param name="Cell">The selected cell.</param>
    /// <returns>True if the selected cell changed; otherwise, false.</returns>
    public bool SetSelectedCell(GroupGridCell Cell)
    {
        if (Cell.IsEmpty)
            Cell = GroupGridCell.Empty;

        if (Cell == fSelectedCell)
            return false;

        if (!Cell.IsEmpty && !IsValidValueCell(Cell))
            return false;

        fSelectedCell = Cell;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
    /// <summary>
    /// Sets the selected cell by row index and column.
    /// </summary>
    /// <param name="RowIndex">The adapter row index.</param>
    /// <param name="Column">The grid column.</param>
    /// <returns>True if the selected cell changed; otherwise, false.</returns>
    public bool SetSelectedCell(int RowIndex, GroupGridColumn Column) => SetSelectedCell(new GroupGridCell(RowIndex, Column));
    /// <summary>
    /// Selects the current cell.
    /// </summary>
    /// <returns>True if the selected cell changed; otherwise, false.</returns>
    public bool SelectCurrentCell()
    {
        return SetSelectedCell(fCurrentCell);
    }
    /// <summary>
    /// Returns true when a cell is selected.
    /// </summary>
    /// <param name="Cell">The cell to check.</param>
    /// <returns>True if the cell is selected; otherwise, false.</returns>
    public bool IsSelectedCell(GroupGridCell Cell)
    {
        return !fSelectedCell.IsEmpty && fSelectedCell == Cell;
    }
    /// <summary>
    /// Returns true when an adapter row is selected.
    /// </summary>
    /// <param name="RowIndex">The adapter row index.</param>
    /// <returns>True if the row is selected; otherwise, false.</returns>
    public bool IsSelectedRow(int RowIndex)
    {
        return !fSelectedCell.IsEmpty && fSelectedCell.RowIndex == RowIndex;
    }
    /// <summary>
    /// Begins editing a cell.
    /// </summary>
    /// <param name="Cell">The cell to edit.</param>
    /// <returns>True if editing started; otherwise, false.</returns>
    public bool BeginEdit(GroupGridCell Cell)
    {
        if (Cell == fEditingCell)
            return true;
        if (!IsValidEditableCell(Cell))
            return false;

        if (!fEditingCell.IsEmpty)
            CancelEdit();

        GroupGridCellEditEventArgs Args = new(Cell, GetValue(Cell.RowIndex, Cell.Column));
        BeginningEdit?.Invoke(this, Args);
        if (Args.Cancel)
            return false;

        fEditingCell = Cell;
        EditingCellChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
    /// <summary>
    /// Begins editing a cell by row index and column.
    /// </summary>
    /// <param name="RowIndex">The adapter row index.</param>
    /// <param name="Column">The grid column.</param>
    /// <returns>True if editing started; otherwise, false.</returns>
    public bool BeginEdit(int RowIndex, GroupGridColumn Column) => BeginEdit(new GroupGridCell(RowIndex, Column));
    /// <summary>
    /// Begins editing the current cell.
    /// </summary>
    /// <returns>True if editing started; otherwise, false.</returns>
    public bool BeginEdit()
    {
        return BeginEdit(fCurrentCell);
    }
    /// <summary>
    /// Commits the edited value.
    /// </summary>
    /// <param name="Value">The editor value.</param>
    /// <returns>True if the edit was committed; otherwise, false.</returns>
    public bool CommitEdit(object Value)
    {
        if (fEditingCell.IsEmpty || !IsValidEditableCell(fEditingCell))
            return false;

        GroupGridCell Cell = fEditingCell;
        object ParsedValue = Cell.Column.ParseValue(Value);
        GroupGridCellEditEventArgs Args = new(Cell, ParsedValue);

        CellValidating?.Invoke(this, Args);
        if (Args.Cancel)
            return false;

        CellValueCommitting?.Invoke(this, Args);
        if (Args.Cancel)
            return false;

        SetValue(Cell.RowIndex, Cell.Column, Args.Value);
        fEditingCell = GroupGridCell.Empty;
        EditingCellChanged?.Invoke(this, EventArgs.Empty);
        CellValueCommitted?.Invoke(this, Args);
        return true;
    }
    /// <summary>
    /// Cancels the current edit.
    /// </summary>
    /// <returns>True if an edit was canceled; otherwise, false.</returns>
    public bool CancelEdit()
    {
        if (fEditingCell.IsEmpty)
            return false;

        GroupGridCell Cell = fEditingCell;
        object Value = IsValidValueCell(Cell) ? GetValue(Cell.RowIndex, Cell.Column) : null;
        fEditingCell = GroupGridCell.Empty;
        EditingCellChanged?.Invoke(this, EventArgs.Empty);
        EditCanceled?.Invoke(this, new GroupGridCellEditEventArgs(Cell, Value));
        return true;
    }
    /// <summary>
    /// Returns the visible columns in display order.
    /// </summary>
    /// <returns>The visible columns.</returns>
    public IReadOnlyList<GroupGridColumn> GetVisibleColumns()
    {
        return GetVisibleColumnList();
    }
    /// <summary>
    /// Returns the visible column index of a specified column.
    /// </summary>
    /// <param name="Column">The grid column.</param>
    /// <returns>The visible column index, or -1 if not found.</returns>
    public int IndexOfVisibleColumn(GroupGridColumn Column)
    {
        if (Column == null || !Column.IsVisible)
            return -1;

        int Index = 0;
        foreach (GroupGridColumn Item in Columns)
        {
            if (!Item.IsVisible)
                continue;
            if (ReferenceEquals(Item, Column))
                return Index;

            Index++;
        }

        return -1;
    }
    /// <summary>
    /// Returns the visible value columns in display order.
    /// </summary>
    /// <returns>The visible value columns.</returns>
    public IReadOnlyList<GroupGridColumn> GetVisibleValueColumns()
    {
        return GetVisibleValueColumnList();
    }
    /// <summary>
    /// Returns true when a column is grouped.
    /// </summary>
    /// <param name="Column">The grid column.</param>
    /// <returns>True if the column is grouped; otherwise, false.</returns>
    public bool IsGroupedColumn(GroupGridColumn Column)
    {
        return Column != null && fGroupColumns.Contains(Column);
    }
    /// <summary>
    /// Adds a column to the grouping list.
    /// </summary>
    /// <param name="Column">The grid column.</param>
    /// <param name="GroupIndex">The group index, or -1 to append.</param>
    /// <returns>True if the column was grouped; otherwise, false.</returns>
    public bool GroupColumn(GroupGridColumn Column, int GroupIndex = -1)
    {
        if (Column == null || !Columns.Contains(Column) || !Column.CanUserGroup || fGroupColumns.Contains(Column))
            return false;

        if (GroupIndex < 0 || GroupIndex > fGroupColumns.Count)
            fGroupColumns.Add(Column);
        else
            fGroupColumns.Insert(GroupIndex, Column);

        return true;
    }
    /// <summary>
    /// Removes a column from the grouping list.
    /// </summary>
    /// <param name="Column">The grid column.</param>
    /// <returns>True if the column was ungrouped; otherwise, false.</returns>
    public bool UngroupColumn(GroupGridColumn Column)
    {
        return Column != null && fGroupColumns.Remove(Column);
    }
    /// <summary>
    /// Moves a grouped column to a new group index.
    /// </summary>
    /// <param name="Column">The grouped column.</param>
    /// <param name="GroupIndex">The new group index.</param>
    /// <returns>True if the column moved; otherwise, false.</returns>
    public bool MoveGroupedColumn(GroupGridColumn Column, int GroupIndex)
    {
        if (Column == null)
            return false;

        int OldIndex = fGroupColumns.IndexOf(Column);
        if (OldIndex < 0 || GroupIndex < 0 || GroupIndex >= fGroupColumns.Count || OldIndex == GroupIndex)
            return false;

        fGroupColumns.Move(OldIndex, GroupIndex);
        return true;
    }
    /// <summary>
    /// Moves a column to a new display index in the column collection.
    /// </summary>
    /// <param name="Column">The grid column.</param>
    /// <param name="ColumnIndex">The new column index.</param>
    /// <returns>True if the column moved; otherwise, false.</returns>
    public bool MoveColumn(GroupGridColumn Column, int ColumnIndex)
    {
        if (Column == null || !Column.CanUserReorder)
            return false;

        int OldIndex = Columns.IndexOf(Column);
        if (OldIndex < 0 || ColumnIndex < 0 || ColumnIndex >= Columns.Count || OldIndex == ColumnIndex)
            return false;

        Columns.Move(OldIndex, ColumnIndex);
        return true;
    }
    /// <summary>
    /// Shows or hides a column.
    /// </summary>
    /// <param name="Column">The grid column.</param>
    /// <param name="IsVisible">True to show the column; false to hide it.</param>
    /// <returns>True if the column visibility changed; otherwise, false.</returns>
    public bool SetColumnVisible(GroupGridColumn Column, bool IsVisible)
    {
        if (Column == null || !Columns.Contains(Column) || Column.IsVisible == IsVisible)
            return false;
        if (!IsVisible && !Column.CanUserHide)
            return false;

        Column.IsVisible = IsVisible;
        ColumnsChanged?.Invoke(this, EventArgs.Empty);

        if (!fCurrentCell.IsEmpty && ReferenceEquals(fCurrentCell.Column, Column) && !IsVisible)
            ClearCurrentCell();
        if (!fSelectedCell.IsEmpty && ReferenceEquals(fSelectedCell.Column, Column) && !IsVisible)
            ClearSelection();
        if (!fEditingCell.IsEmpty && ReferenceEquals(fEditingCell.Column, Column) && !IsVisible)
            CancelEdit();

        return true;
    }
    /// <summary>
    /// Returns the visible-node index of a specified adapter row index.
    /// </summary>
    /// <param name="RowIndex">The adapter row index.</param>
    /// <returns>The visible-node index, or -1 if not found.</returns>
    public int IndexOfVisibleRow(int RowIndex)
    {
        return fProjection.IndexOfRow(RowIndex);
    }
    /// <summary>
    /// Returns information about a visible row.
    /// </summary>
    /// <param name="VisibleNodeIndex">The visible-node index.</param>
    /// <returns>The visible row information.</returns>
    public GroupGridRowInfo GetVisibleRowInfo(int VisibleNodeIndex)
    {
        return CreateRowInfo(fProjection.NodeByVisibleIndex(VisibleNodeIndex), VisibleNodeIndex);
    }
    /// <summary>
    /// Moves the current cell horizontally by a visible column delta.
    /// </summary>
    /// <param name="ColumnDelta">The visible column delta.</param>
    /// <returns>True if the current cell moved; otherwise, false.</returns>
    public bool MoveCurrentColumn(int ColumnDelta)
    {
        if (fCurrentCell.IsEmpty || ColumnDelta == 0)
            return false;

        List<GroupGridColumn> VisibleColumns = GetVisibleValueColumnList();
        int Index = VisibleColumns.IndexOf(fCurrentCell.Column);
        if (Index < 0)
            return false;

        int NewIndex = Index + ColumnDelta;
        if (NewIndex < 0 || NewIndex >= VisibleColumns.Count)
            return false;

        return SetCurrentCell(fCurrentCell.RowIndex, VisibleColumns[NewIndex]);
    }
    /// <summary>
    /// Moves the current cell vertically by a visible data-row delta.
    /// </summary>
    /// <param name="RowDelta">The visible row delta.</param>
    /// <returns>True if the current cell moved; otherwise, false.</returns>
    public bool MoveCurrentRow(int RowDelta)
    {
        if (fCurrentCell.IsEmpty || RowDelta == 0)
            return false;

        int VisibleNodeIndex = fProjection.IndexOfRow(fCurrentCell.RowIndex);
        if (VisibleNodeIndex < 0)
            return false;

        GroupGridNode Node = fProjection.FindDataRowNode(VisibleNodeIndex, RowDelta);
        if (Node == null)
            return false;

        return SetCurrentCell(Node.RowIndex, fCurrentCell.Column);
    }
    /// <summary>
    /// Moves the current cell by visible row and column deltas.
    /// </summary>
    /// <param name="RowDelta">The visible row delta.</param>
    /// <param name="ColumnDelta">The visible column delta.</param>
    /// <returns>True if the current cell moved; otherwise, false.</returns>
    public bool MoveCurrentCell(int RowDelta, int ColumnDelta)
    {
        bool Result = false;

        if (RowDelta != 0)
            Result = MoveCurrentRow(RowDelta);
        if (ColumnDelta != 0)
            Result = MoveCurrentColumn(ColumnDelta) || Result;

        return Result;
    }
    /// <summary>
    /// Moves the current cell to the first visible value column in the current row.
    /// </summary>
    /// <returns>True if the current cell moved; otherwise, false.</returns>
    public bool MoveCurrentToFirstColumn()
    {
        if (fCurrentCell.IsEmpty)
            return false;

        GroupGridColumn Column = GetFirstValueColumn();
        return Column != null && SetCurrentCell(fCurrentCell.RowIndex, Column);
    }
    /// <summary>
    /// Moves the current cell to the last visible value column in the current row.
    /// </summary>
    /// <returns>True if the current cell moved; otherwise, false.</returns>
    public bool MoveCurrentToLastColumn()
    {
        if (fCurrentCell.IsEmpty)
            return false;

        GroupGridColumn Column = GetLastValueColumn();
        return Column != null && SetCurrentCell(fCurrentCell.RowIndex, Column);
    }
    /// <summary>
    /// Moves the current cell to the first visible data row.
    /// </summary>
    /// <returns>True if the current cell moved; otherwise, false.</returns>
    public bool MoveCurrentToFirstRow()
    {
        List<GroupGridNode> Rows = GetVisibleDataRowNodes();
        GroupGridColumn Column = fCurrentCell.IsEmpty ? GetFirstValueColumn() : fCurrentCell.Column;
        return Rows.Count > 0 && MoveCurrentToDataRowColumn(Rows[0], Column);
    }
    /// <summary>
    /// Moves the current cell to the last visible data row.
    /// </summary>
    /// <returns>True if the current cell moved; otherwise, false.</returns>
    public bool MoveCurrentToLastRow()
    {
        List<GroupGridNode> Rows = GetVisibleDataRowNodes();
        GroupGridColumn Column = fCurrentCell.IsEmpty ? GetFirstValueColumn() : fCurrentCell.Column;
        return Rows.Count > 0 && MoveCurrentToDataRowColumn(Rows[Rows.Count - 1], Column);
    }
    /// <summary>
    /// Moves the current cell to the next editable visible cell.
    /// </summary>
    /// <param name="Forward">True to move forward; false to move backward.</param>
    /// <returns>True if the current cell moved; otherwise, false.</returns>
    public bool MoveCurrentToNextEditableCell(bool Forward)
    {
        List<GroupGridNode> Rows = GetVisibleDataRowNodes();
        List<GroupGridColumn> Columns = GetVisibleValueColumnList()
            .Where(Column => !Column.IsReadOnly)
            .ToList();
        if (Rows.Count == 0 || Columns.Count == 0)
            return false;

        int RowIndex = fCurrentCell.IsEmpty ? -1 : IndexOfVisibleDataRow(Rows, fCurrentCell.RowIndex);
        int ColumnIndex = fCurrentCell.IsEmpty ? -1 : Columns.IndexOf(fCurrentCell.Column);

        if (RowIndex < 0 || ColumnIndex < 0)
        {
            RowIndex = Forward ? 0 : Rows.Count - 1;
            ColumnIndex = Forward ? 0 : Columns.Count - 1;
        }
        else if (Forward)
        {
            ColumnIndex++;
            if (ColumnIndex >= Columns.Count)
            {
                ColumnIndex = 0;
                RowIndex++;
            }
        }
        else
        {
            ColumnIndex--;
            if (ColumnIndex < 0)
            {
                ColumnIndex = Columns.Count - 1;
                RowIndex--;
            }
        }

        while (RowIndex >= 0 && RowIndex < Rows.Count)
        {
            GroupGridCell Cell = new(Rows[RowIndex].RowIndex, Columns[ColumnIndex]);
            if (IsValidEditableCell(Cell))
                return SetCurrentCell(Cell);

            if (Forward)
            {
                ColumnIndex++;
                if (ColumnIndex >= Columns.Count)
                {
                    ColumnIndex = 0;
                    RowIndex++;
                }
            }
            else
            {
                ColumnIndex--;
                if (ColumnIndex < 0)
                {
                    ColumnIndex = Columns.Count - 1;
                    RowIndex--;
                }
            }
        }

        return false;
    }
    /// <summary>
    /// Sets the expanded state of a group node.
    /// </summary>
    /// <param name="Node">The group node.</param>
    /// <param name="IsExpanded">True to expand; false to collapse.</param>
    /// <returns>True if the group state changed; otherwise, false.</returns>
    internal bool SetGroupExpanded(GroupGridNode Node, bool IsExpanded)
    {
        if (Node == null || !Node.IsGroup)
            return false;

        if (Node.IsExpanded == IsExpanded)
            return false;

        Node.IsExpanded = IsExpanded;
        fProjection.UpdateVisibleNodes();
        VisibleNodesChanged?.Invoke(this, EventArgs.Empty);
        CoerceCurrentCell();
        CoerceSelectedCell();
        CoerceEditingCell();
        return true;
    }
    /// <summary>
    /// Sets the expanded state of a group node by visible-node index.
    /// </summary>
    /// <param name="VisibleNodeIndex">The visible-node index.</param>
    /// <param name="IsExpanded">True to expand; false to collapse.</param>
    /// <returns>True if the group state changed; otherwise, false.</returns>
    public bool SetGroupExpanded(int VisibleNodeIndex, bool IsExpanded)
    {
        return SetGroupExpanded(fProjection.NodeByVisibleIndex(VisibleNodeIndex), IsExpanded);
    }
    /// <summary>
    /// Toggles the expanded state of a group node by visible-node index.
    /// </summary>
    /// <param name="VisibleNodeIndex">The visible-node index.</param>
    /// <returns>True if the group state changed; otherwise, false.</returns>
    public bool ToggleGroupExpanded(int VisibleNodeIndex)
    {
        GroupGridNode Node = fProjection.NodeByVisibleIndex(VisibleNodeIndex);
        return Node != null && Node.IsGroup && SetGroupExpanded(Node, !Node.IsExpanded);
    }
    /// <summary>
    /// Returns a group summary value by visible-node index and column.
    /// </summary>
    /// <param name="VisibleNodeIndex">The visible-node index.</param>
    /// <param name="Column">The grid column.</param>
    /// <returns>The summary value.</returns>
    public GroupGridSummaryValue GetGroupSummary(int VisibleNodeIndex, GroupGridColumn Column)
    {
        GroupGridNode Node = fProjection.NodeByVisibleIndex(VisibleNodeIndex);
        if (Node == null)
            return GroupGridSummaryValue.Empty;

        if (Node.IsGroupSummary)
            Node = Node.Parent;

        return Node != null && Node.IsGroup
            ? Node.GetSummary(Column)
            : GroupGridSummaryValue.Empty;
    }
    /// <summary>
    /// Returns a total summary value by column.
    /// </summary>
    /// <param name="Column">The grid column.</param>
    /// <returns>The summary value.</returns>
    public GroupGridSummaryValue GetTotalSummary(GroupGridColumn Column)
    {
        return fProjection.Root.GetSummary(Column);
    }
    /// <summary>
    /// Returns the display text for a group header row.
    /// </summary>
    /// <param name="VisibleNodeIndex">The visible-node index.</param>
    /// <returns>The group header display text.</returns>
    public string GetGroupHeaderText(int VisibleNodeIndex)
    {
        GroupGridRowInfo RowInfo = GetVisibleRowInfo(VisibleNodeIndex);
        if (!RowInfo.IsGroup || RowInfo.Column == null)
            return string.Empty;

        string Header = string.IsNullOrWhiteSpace(RowInfo.Column.Header)
            ? RowInfo.Column.Name
            : RowInfo.Column.Header;
        string ValueText = RowInfo.Column.FormatValue(RowInfo.Key);

        return string.IsNullOrWhiteSpace(Header)
            ? ValueText
            : string.Format(CultureInfo.CurrentCulture, "{0}: {1}", Header, ValueText);
    }
    /// <summary>
    /// Returns the display text for a visible body row and value column.
    /// </summary>
    /// <param name="VisibleNodeIndex">The visible-node index.</param>
    /// <param name="Column">The grid column.</param>
    /// <returns>The display text.</returns>
    public string GetDisplayText(int VisibleNodeIndex, GroupGridColumn Column)
    {
        if (Column == null)
            return string.Empty;

        GroupGridNode Node = fProjection.NodeByVisibleIndex(VisibleNodeIndex);
        if (Node == null)
            return string.Empty;

        if (Node.IsDataRow)
            return Column.FormatValue(GetValue(Node.RowIndex, Column));
        if (Node.IsGroupSummary)
            return Column.FormatSummaryValue(GetGroupSummary(VisibleNodeIndex, Column));

        return string.Empty;
    }
    /// <summary>
    /// Returns the display text for a footer summary cell.
    /// </summary>
    /// <param name="Column">The grid column.</param>
    /// <returns>The footer summary display text.</returns>
    public string GetTotalSummaryText(GroupGridColumn Column)
    {
        return Column == null
            ? string.Empty
            : Column.FormatSummaryValue(GetTotalSummary(Column));
    }
    /// <summary>
    /// Returns hit-test information for a point in local grid coordinates.
    /// </summary>
    /// <param name="X">The local x-coordinate.</param>
    /// <param name="Y">The local y-coordinate.</param>
    /// <returns>The hit-test result.</returns>
    public GroupGridHitTestResult HitTest(double X, double Y)
    {
        if (X < 0 || Y < 0)
            return GroupGridHitTestResult.Empty;

        double Top = 0;
        double Bottom = Top + LayoutMetrics.ToolBarHeight;
        if (Y < Bottom)
            return CreateBandHitTestResult(X, Y, GroupGridBand.ToolBar, GroupGridHitTestKind.ToolBar);

        Top = Bottom;
        Bottom = Top + LayoutMetrics.GroupPanelHeight;
        if (Y < Bottom)
            return CreateColumnHitTestResult(X, Y, GroupGridBand.GroupPanel, GroupGridHitTestKind.GroupPanel, GroupColumns);

        Top = Bottom;
        Bottom = Top + LayoutMetrics.ColumnHeaderHeight;
        if (Y < Bottom)
            return CreateColumnHitTestResult(X, Y, GroupGridBand.ColumnHeader, GroupGridHitTestKind.ColumnHeader, GetVisibleValueColumns());

        Top = Bottom;
        Bottom = Top + LayoutMetrics.FilterRowHeight;
        if (Y < Bottom)
            return CreateColumnHitTestResult(X, Y, GroupGridBand.FilterRow, GroupGridHitTestKind.FilterCell, GetVisibleValueColumns());

        Top = Bottom;
        Bottom = Top + (fViewport.Count * LayoutMetrics.RowHeight);
        if (Y < Bottom)
            return CreateBodyHitTestResult(X, Y, Top);

        Top = Bottom;
        Bottom = Top + LayoutMetrics.FooterSummaryHeight;
        if (Y < Bottom)
        {
            GroupGridHitTestResult Result = CreateColumnHitTestResult(X, Y, GroupGridBand.FooterSummary, GroupGridHitTestKind.FooterSummaryCell, GetVisibleValueColumns());
            Result.RowKind = GroupGridRowKind.TotalSummary;
            return Result;
        }

        return GroupGridHitTestResult.Empty;
    }
    /// <summary>
    /// Returns the source row at a visible node index.
    /// </summary>
    /// <param name="VisibleNodeIndex">The visible node index.</param>
    /// <returns>The source row, or null when the visible node is not a data row.</returns>
    public object GetVisibleRow(int VisibleNodeIndex)
    {
        GroupGridNode Node = fProjection.NodeByVisibleIndex(VisibleNodeIndex);
        return Node != null && Node.IsDataRow ? Node.Row : null;
    }
    /// <summary>
    /// Returns the cell value at a specified adapter row index and column.
    /// </summary>
    /// <param name="RowIndex">The adapter row index.</param>
    /// <param name="Column">The grid column.</param>
    /// <returns>The cell value.</returns>
    public object GetValue(int RowIndex, GroupGridColumn Column)
    {
        return fDataAdapter == null ? null : fDataAdapter.GetValue(RowIndex, Column);
    }
    /// <summary>
    /// Sets the cell value at a specified adapter row index and column.
    /// </summary>
    /// <param name="RowIndex">The adapter row index.</param>
    /// <param name="Column">The grid column.</param>
    /// <param name="Value">The value to set.</param>
    public void SetValue(int RowIndex, GroupGridColumn Column, object Value)
    {
        if (fDataAdapter != null && fDataAdapter.CanSetValue(RowIndex, Column))
            fDataAdapter.SetValue(RowIndex, Column, Value);
    }
    /// <summary>
    /// Returns true when a value can be set at a specified adapter row index and column.
    /// </summary>
    /// <param name="RowIndex">The adapter row index.</param>
    /// <param name="Column">The grid column.</param>
    /// <returns>True when the cell value can be set; otherwise, false.</returns>
    public bool CanSetValue(int RowIndex, GroupGridColumn Column)
    {
        return fDataAdapter != null && fDataAdapter.CanSetValue(RowIndex, Column);
    }
    /// <summary>
    /// Sets the viewport window into the logical visible-node list.
    /// </summary>
    /// <param name="Viewport">The viewport.</param>
    /// <returns>True if the viewport changed; otherwise, false.</returns>
    public bool SetViewport(GroupGridViewport Viewport)
    {
        if (Viewport == fViewport)
            return false;

        fViewport = Viewport;
        ViewportChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    // ● properties
    /// <summary>
    /// Gets the grid columns.
    /// </summary>
    public ObservableCollection<GroupGridColumn> Columns { get; } = new();
    /// <summary>
    /// Gets the grouped columns in grouping order.
    /// </summary>
    public IReadOnlyList<GroupGridColumn> GroupColumns => fGroupColumns;
    /// <summary>
    /// Gets or sets the data adapter.
    /// </summary>
    public IGroupGridDataAdapter DataAdapter
    {
        get => fDataAdapter;
        set => SetDataAdapter(value);
    }
    /// <summary>
    /// Gets the current cell.
    /// </summary>
    public GroupGridCell CurrentCell => fCurrentCell;
    /// <summary>
    /// Gets the selected cell.
    /// </summary>
    public GroupGridCell SelectedCell => fSelectedCell;
    /// <summary>
    /// Gets the editing cell.
    /// </summary>
    public GroupGridCell EditingCell => fEditingCell;
    /// <summary>
    /// Gets a value indicating whether the grid has a current cell.
    /// </summary>
    public bool HasCurrentCell => !fCurrentCell.IsEmpty;
    /// <summary>
    /// Gets a value indicating whether the grid has a selected cell.
    /// </summary>
    public bool HasSelectedCell => !fSelectedCell.IsEmpty;
    /// <summary>
    /// Gets a value indicating whether the grid is editing a cell.
    /// </summary>
    public bool IsEditing => !fEditingCell.IsEmpty;
    /// <summary>
    /// Gets the current row index, or -1 when there is no current cell.
    /// </summary>
    public int CurrentRowIndex => fCurrentCell.RowIndex;
    /// <summary>
    /// Gets the selected row index, or -1 when there is no selected cell.
    /// </summary>
    public int SelectedRowIndex => fSelectedCell.RowIndex;
    /// <summary>
    /// Gets the current column, or null when there is no current cell.
    /// </summary>
    public GroupGridColumn CurrentColumn => fCurrentCell.Column;
    /// <summary>
    /// Gets the selected column, or null when there is no selected cell.
    /// </summary>
    public GroupGridColumn SelectedColumn => fSelectedCell.Column;
    /// <summary>
    /// Gets the number of visible projected nodes.
    /// </summary>
    public int VisibleNodeCount => fProjection.VisibleNodes.Count;
    /// <summary>
    /// Gets the number of grouped columns.
    /// </summary>
    public int GroupColumnCount => fGroupColumns.Count;
    /// <summary>
    /// Gets the viewport window into the logical visible-node list.
    /// </summary>
    public GroupGridViewport Viewport => fViewport;
    /// <summary>
    /// Gets the layout metrics used by non-visual hit testing.
    /// </summary>
    public GroupGridLayoutMetrics LayoutMetrics { get; } = new();
    /// <summary>
    /// Gets the number of adapter rows.
    /// </summary>
    public int RowCount => fDataAdapter == null ? 0 : fDataAdapter.RowCount;
    /// <summary>
    /// Gets the internal node projection.
    /// </summary>
    internal GroupGridNodeProjection Projection => fProjection;

    // ● events
    /// <summary>
    /// Occurs when the data adapter changes.
    /// </summary>
    public event EventHandler DataAdapterChanged;
    /// <summary>
    /// Occurs when the adapter reports a data change.
    /// </summary>
    public event EventHandler<GroupGridDataChangedEventArgs> DataChanged;
    /// <summary>
    /// Occurs when the columns collection changes.
    /// </summary>
    public event EventHandler ColumnsChanged;
    /// <summary>
    /// Occurs when the grouped columns list changes.
    /// </summary>
    public event EventHandler GroupColumnsChanged;
    /// <summary>
    /// Occurs when the logical flat list of visible nodes changes.
    /// This is the full row projection after grouping or expansion rules, not the viewport subset currently rendered on screen.
    /// </summary>
    public event EventHandler VisibleNodesChanged;
    /// <summary>
    /// Occurs when the current cell changes.
    /// </summary>
    public event EventHandler CurrentCellChanged;
    /// <summary>
    /// Occurs when the current row changes.
    /// </summary>
    public event EventHandler CurrentRowChanged;
    /// <summary>
    /// Occurs when the selected cell changes.
    /// </summary>
    public event EventHandler SelectionChanged;
    /// <summary>
    /// Occurs before cell editing begins.
    /// </summary>
    public event EventHandler<GroupGridCellEditEventArgs> BeginningEdit;
    /// <summary>
    /// Occurs when the editing cell changes.
    /// </summary>
    public event EventHandler EditingCellChanged;
    /// <summary>
    /// Occurs before an edited value is committed.
    /// </summary>
    public event EventHandler<GroupGridCellEditEventArgs> CellValidating;
    /// <summary>
    /// Occurs after validation and before the value is written to the adapter.
    /// </summary>
    public event EventHandler<GroupGridCellEditEventArgs> CellValueCommitting;
    /// <summary>
    /// Occurs after the value is written to the adapter.
    /// </summary>
    public event EventHandler<GroupGridCellEditEventArgs> CellValueCommitted;
    /// <summary>
    /// Occurs when editing is canceled.
    /// </summary>
    public event EventHandler<GroupGridCellEditEventArgs> EditCanceled;
    /// <summary>
    /// Occurs when the viewport window into the logical visible-node list changes.
    /// </summary>
    public event EventHandler ViewportChanged;
    /// <summary>
    /// Occurs when summary values change.
    /// </summary>
    public event EventHandler SummariesChanged;
}
