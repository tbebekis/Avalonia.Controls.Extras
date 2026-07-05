namespace Avalonia.Controls;

/// <summary>
/// Provides the Avalonia visual surface for a <see cref="GroupGridEngine"/>.
/// </summary>
public class GroupGrid: Control
{
    // ● private fields
    static readonly IBrush fBackgroundBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    static readonly IBrush fToolBarBrush = new SolidColorBrush(Color.FromRgb(246, 247, 248));
    static readonly IBrush fGroupPanelBrush = new SolidColorBrush(Color.FromRgb(235, 239, 244));
    static readonly IBrush fHeaderBrush = new SolidColorBrush(Color.FromRgb(241, 243, 245));
    static readonly IBrush fFilterBrush = new SolidColorBrush(Color.FromRgb(250, 250, 250));
    static readonly IBrush fGroupRowBrush = new SolidColorBrush(Color.FromRgb(232, 238, 246));
    static readonly IBrush fGroupSummaryBrush = new SolidColorBrush(Color.FromRgb(247, 249, 252));
    static readonly IBrush fSelectedBrush = new SolidColorBrush(Color.FromRgb(218, 236, 255));
    static readonly IBrush fCurrentBrush = new SolidColorBrush(Color.FromRgb(255, 247, 213));
    static readonly IBrush fEditingBrush = new SolidColorBrush(Color.FromRgb(255, 238, 204));
    static readonly IBrush fFooterBrush = new SolidColorBrush(Color.FromRgb(238, 240, 242));
    static readonly IBrush fTextBrush = new SolidColorBrush(Color.FromRgb(32, 37, 42));
    static readonly IBrush fMutedTextBrush = new SolidColorBrush(Color.FromRgb(84, 91, 99));
    static readonly Pen fLinePen = new(new SolidColorBrush(Color.FromRgb(211, 216, 222)), 1);
    static readonly Pen fCurrentPen = new(new SolidColorBrush(Color.FromRgb(64, 122, 190)), 1);
    static readonly Pen fEditingPen = new(new SolidColorBrush(Color.FromRgb(194, 105, 0)), 2);
    GroupGridEngine fEngine;
    object fItemsSource;
    IDisposable fOwnedDataAdapter;
    int fFirstVisibleNodeIndex;
    bool fAutoGenerateColumns;

    // ● private methods
    void Engine_Changed(object Sender, EventArgs Args)
    {
        UpdateViewport(Bounds.Size);
        InvalidateVisual();
    }
    void AttachEngine(GroupGridEngine Engine)
    {
        if (Engine == null)
            return;

        Engine.DataAdapterChanged += Engine_Changed;
        Engine.DataChanged += Engine_Changed;
        Engine.ColumnsChanged += Engine_Changed;
        Engine.GroupColumnsChanged += Engine_Changed;
        Engine.VisibleNodesChanged += Engine_Changed;
        Engine.CurrentCellChanged += Engine_Changed;
        Engine.CurrentRowChanged += Engine_Changed;
        Engine.SelectionChanged += Engine_Changed;
        Engine.EditingCellChanged += Engine_Changed;
        Engine.ViewportChanged += Engine_Changed;
        Engine.SummariesChanged += Engine_Changed;
    }
    void DetachEngine(GroupGridEngine Engine)
    {
        if (Engine == null)
            return;

        Engine.DataAdapterChanged -= Engine_Changed;
        Engine.DataChanged -= Engine_Changed;
        Engine.ColumnsChanged -= Engine_Changed;
        Engine.GroupColumnsChanged -= Engine_Changed;
        Engine.VisibleNodesChanged -= Engine_Changed;
        Engine.CurrentCellChanged -= Engine_Changed;
        Engine.CurrentRowChanged -= Engine_Changed;
        Engine.SelectionChanged -= Engine_Changed;
        Engine.EditingCellChanged -= Engine_Changed;
        Engine.ViewportChanged -= Engine_Changed;
        Engine.SummariesChanged -= Engine_Changed;
    }
    void UpdateViewport(Size Size)
    {
        if (fEngine == null)
            return;

        double BodyHeight = Math.Max(0, Size.Height - FixedBandHeight);
        int Count = fEngine.LayoutMetrics.RowHeight <= 0
            ? 0
            : Math.Min(fEngine.VisibleNodeCount, (int)Math.Floor(BodyHeight / fEngine.LayoutMetrics.RowHeight));
        fFirstVisibleNodeIndex = ClampFirstVisibleNodeIndex(fFirstVisibleNodeIndex, Count);
        GroupGridViewport Viewport = Count <= 0
            ? GroupGridViewport.Empty
            : new GroupGridViewport(fFirstVisibleNodeIndex, fFirstVisibleNodeIndex + Count - 1);

        fEngine.SetViewport(Viewport);
    }
    int ClampFirstVisibleNodeIndex(int Value, int ViewportCount)
    {
        if (fEngine == null || fEngine.VisibleNodeCount == 0 || ViewportCount <= 0)
            return 0;

        int Max = Math.Max(0, fEngine.VisibleNodeCount - ViewportCount);
        if (Value < 0)
            return 0;
        if (Value > Max)
            return Max;

        return Value;
    }
    bool ScrollViewport(int Delta)
    {
        if (fEngine == null || Delta == 0)
            return false;

        return SetFirstVisibleNodeIndexCore(fFirstVisibleNodeIndex + Delta);
    }
    bool ScrollCurrentCellIntoViewCore()
    {
        if (fEngine == null || fEngine.CurrentCell.IsEmpty || fEngine.Viewport.IsEmpty)
            return false;

        int VisibleNodeIndex = fEngine.IndexOfVisibleRow(fEngine.CurrentCell.RowIndex);
        if (VisibleNodeIndex < 0)
            return false;

        if (VisibleNodeIndex < fEngine.Viewport.FirstVisibleNodeIndex)
            return SetFirstVisibleNodeIndexCore(VisibleNodeIndex);
        if (VisibleNodeIndex > fEngine.Viewport.LastVisibleNodeIndex)
            return SetFirstVisibleNodeIndexCore(VisibleNodeIndex - fEngine.Viewport.Count + 1);

        return false;
    }
    bool SetFirstVisibleNodeIndexCore(int Value)
    {
        if (fEngine == null)
            return false;

        int NewFirstVisibleNodeIndex = ClampFirstVisibleNodeIndex(Value, fEngine.Viewport.Count);
        if (NewFirstVisibleNodeIndex == fFirstVisibleNodeIndex)
            return false;

        fFirstVisibleNodeIndex = NewFirstVisibleNodeIndex;
        UpdateViewport(Bounds.Size);
        InvalidateVisual();
        return true;
    }
    Type FindListItemType(object ItemsSource)
    {
        if (ItemsSource == null)
            return null;

        Type SourceType = ItemsSource.GetType();
        Type ListType = SourceType
            .GetInterfaces()
            .Concat(new[] { SourceType })
            .FirstOrDefault(Item => Item.IsGenericType && Item.GetGenericTypeDefinition() == typeof(IList<>));

        return ListType == null ? null : ListType.GetGenericArguments()[0];
    }
    IGroupGridDataAdapter CreateDataAdapter(object ItemsSource)
    {
        if (ItemsSource == null)
            return null;

        Type ItemType = FindListItemType(ItemsSource);
        if (ItemType == null)
            throw new ArgumentException("ItemsSource must implement IList<T>.", nameof(ItemsSource));

        Type AdapterType = typeof(GroupGridListDataAdapter<>).MakeGenericType(ItemType);
        return (IGroupGridDataAdapter)Activator.CreateInstance(AdapterType, ItemsSource);
    }
    GroupGridColumn CreateAutoColumn(PropertyInfo Property)
    {
        Type Type = Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType;
        GroupGridColumn Column;

        if (Type == typeof(bool))
            Column = new GroupGridCheckBoxColumn();
        else if (Type == typeof(DateTime) || Type == typeof(DateTimeOffset))
            Column = new GroupGridDateColumn();
        else if (Type == typeof(byte)
                 || Type == typeof(short)
                 || Type == typeof(int)
                 || Type == typeof(long)
                 || Type == typeof(float)
                 || Type == typeof(double)
                 || Type == typeof(decimal))
            Column = new GroupGridNumberColumn();
        else
            Column = new GroupGridTextColumn();

        Column.Name = Property.Name;
        Column.Header = Property.Name;
        Column.ValueType = Type;
        Column.IsReadOnly = !Property.CanWrite;
        return Column;
    }
    void GenerateColumnsFromItemType(Type ItemType)
    {
        if (!fAutoGenerateColumns || ItemType == null || Columns.Count > 0)
            return;

        foreach (PropertyInfo Property in ItemType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (Property.GetIndexParameters().Length == 0)
                Columns.Add(CreateAutoColumn(Property));
    }
    void SetItemsSource(object Value)
    {
        if (ReferenceEquals(fItemsSource, Value))
            return;

        if (fOwnedDataAdapter != null)
        {
            fOwnedDataAdapter.Dispose();
            fOwnedDataAdapter = null;
        }

        fItemsSource = Value;
        GenerateColumnsFromItemType(FindListItemType(Value));

        IGroupGridDataAdapter Adapter = CreateDataAdapter(Value);
        fOwnedDataAdapter = Adapter as IDisposable;
        DataAdapter = Adapter;
    }
    bool IsCurrentCell(GroupGridRowInfo RowInfo, GroupGridColumn Column)
    {
        return RowInfo.IsDataRow && fEngine.CurrentCell == new GroupGridCell(RowInfo.RowIndex, Column);
    }
    bool IsSelectedRow(GroupGridRowInfo RowInfo)
    {
        return RowInfo.IsDataRow && fEngine.IsSelectedRow(RowInfo.RowIndex);
    }
    bool IsEditingCell(GroupGridRowInfo RowInfo, GroupGridColumn Column)
    {
        return RowInfo.IsDataRow && fEngine.EditingCell == new GroupGridCell(RowInfo.RowIndex, Column);
    }
    Rect InsetRect(Rect Rect, double Value)
    {
        return new Rect(Rect.X + Value, Rect.Y + Value, Math.Max(0, Rect.Width - (Value * 2)), Math.Max(0, Rect.Height - (Value * 2)));
    }
    FormattedText CreateText(string Text, IBrush Brush, double MaxWidth, FontWeight Weight = FontWeight.Normal)
    {
        FormattedText Result = new(Text ?? string.Empty, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI", FontStyle.Normal, Weight, FontStretch.Normal), 12, Brush);
        Result.MaxTextWidth = Math.Max(0, MaxWidth);
        Result.MaxLineCount = 1;
        Result.Trimming = TextTrimming.CharacterEllipsis;
        return Result;
    }
    void DrawText(DrawingContext Context, string Text, Rect Rect, IBrush Brush, FontWeight Weight = FontWeight.Normal)
    {
        if (string.IsNullOrEmpty(Text) || Rect.Width <= 4 || Rect.Height <= 4)
            return;

        using (Context.PushClip(Rect))
        {
            FormattedText FormattedText = CreateText(Text, Brush, Rect.Width - 8, Weight);
            double Y = Rect.Y + Math.Max(2, (Rect.Height - FormattedText.Height) / 2);
            Context.DrawText(FormattedText, new Point(Rect.X + 4, Y));
        }
    }
    void DrawBand(DrawingContext Context, Rect Rect, IBrush Brush)
    {
        Context.DrawRectangle(Brush, fLinePen, Rect);
    }
    double DrawColumns(DrawingContext Context, double Y, double Height, IBrush Brush, bool DrawFilterText)
    {
        double X = 0;
        foreach (GroupGridColumn Column in fEngine.GetVisibleValueColumns())
        {
            double Width = Math.Max(Column.MinWidth, Column.Width);
            Rect Rect = new(X, Y, Width, Height);
            DrawBand(Context, Rect, Brush);

            string Text = DrawFilterText
                ? string.Empty
                : string.IsNullOrWhiteSpace(Column.Header) ? Column.Name : Column.Header;
            DrawText(Context, Text, Rect, fTextBrush, FontWeight.SemiBold);
            X += Width;
        }

        return X;
    }
    void DrawGroupPanel(DrawingContext Context, double Y, double Height, double Width)
    {
        DrawBand(Context, new Rect(0, Y, Width, Height), fGroupPanelBrush);

        double X = 6;
        foreach (GroupGridColumn Column in fEngine.GroupColumns)
        {
            string Text = string.IsNullOrWhiteSpace(Column.Header) ? Column.Name : Column.Header;
            double ItemWidth = Math.Max(80, Text.Length * 8 + 24);
            Rect Rect = new(X, Y + 5, ItemWidth, Math.Max(0, Height - 10));
            Context.DrawRectangle(fHeaderBrush, fLinePen, Rect, 3, 3);
            DrawText(Context, Text, Rect, fTextBrush, FontWeight.SemiBold);
            X += ItemWidth + 6;
        }
    }
    void DrawBody(DrawingContext Context, double Y, double Width)
    {
        double RowHeight = fEngine.LayoutMetrics.RowHeight;
        if (RowHeight <= 0 || fEngine.Viewport.IsEmpty)
            return;

        for (int Index = 0; Index < fEngine.Viewport.Count; Index++)
        {
            int VisibleNodeIndex = fEngine.Viewport.FirstVisibleNodeIndex + Index;
            GroupGridRowInfo RowInfo = fEngine.GetVisibleRowInfo(VisibleNodeIndex);
            Rect RowRect = new(0, Y + (Index * RowHeight), Width, RowHeight);

            if (RowInfo.IsGroup)
                DrawGroupRow(Context, RowRect, VisibleNodeIndex, RowInfo);
            else
                DrawValueRow(Context, RowRect, VisibleNodeIndex, RowInfo);
        }
    }
    void DrawGroupRow(DrawingContext Context, Rect RowRect, int VisibleNodeIndex, GroupGridRowInfo RowInfo)
    {
        DrawBand(Context, RowRect, fGroupRowBrush);

        double X = Math.Max(0, RowInfo.Level) * fEngine.LayoutMetrics.GroupIndentWidth;
        Rect ExpanderRect = new(X, RowRect.Y, fEngine.LayoutMetrics.GroupExpanderWidth, RowRect.Height);
        string Expander = RowInfo.IsExpanded ? "-" : "+";
        DrawText(Context, Expander, ExpanderRect, fMutedTextBrush, FontWeight.SemiBold);
        DrawText(Context, fEngine.GetGroupHeaderText(VisibleNodeIndex), new Rect(ExpanderRect.Right, RowRect.Y, RowRect.Width - ExpanderRect.Right, RowRect.Height), fTextBrush, FontWeight.SemiBold);
    }
    void DrawValueRow(DrawingContext Context, Rect RowRect, int VisibleNodeIndex, GroupGridRowInfo RowInfo)
    {
        IBrush RowBrush = RowInfo.IsGroupSummary ? fGroupSummaryBrush : fBackgroundBrush;
        DrawBand(Context, RowRect, RowBrush);

        double X = 0;
        foreach (GroupGridColumn Column in fEngine.GetVisibleValueColumns())
        {
            double Width = Math.Max(Column.MinWidth, Column.Width);
            Rect CellRect = new(X, RowRect.Y, Width, RowRect.Height);
            IBrush CellBrush = RowBrush;
            if (IsSelectedRow(RowInfo))
                CellBrush = fSelectedBrush;
            if (IsCurrentCell(RowInfo, Column))
                CellBrush = fCurrentBrush;
            if (IsEditingCell(RowInfo, Column))
                CellBrush = fEditingBrush;

            Context.DrawRectangle(CellBrush, fLinePen, CellRect);
            DrawText(Context, fEngine.GetDisplayText(VisibleNodeIndex, Column), CellRect, RowInfo.IsGroupSummary ? fMutedTextBrush : fTextBrush);

            if (IsCurrentCell(RowInfo, Column))
                Context.DrawRectangle(null, fCurrentPen, CellRect);
            if (IsEditingCell(RowInfo, Column))
                Context.DrawRectangle(null, fEditingPen, InsetRect(CellRect, 1));

            X += Width;
        }
    }
    void DrawFooter(DrawingContext Context, double Y, double Height)
    {
        double X = 0;
        foreach (GroupGridColumn Column in fEngine.GetVisibleValueColumns())
        {
            double Width = Math.Max(Column.MinWidth, Column.Width);
            Rect Rect = new(X, Y, Width, Height);
            DrawBand(Context, Rect, fFooterBrush);
            DrawText(Context, fEngine.GetTotalSummaryText(Column), Rect, fMutedTextBrush, FontWeight.SemiBold);
            X += Width;
        }
    }
    void HandleHitTest(GroupGridHitTestResult Hit)
    {
        if (Hit == null || fEngine == null)
            return;

        if (Hit.Kind == GroupGridHitTestKind.GroupExpander)
        {
            fEngine.ToggleGroupExpanded(Hit.VisibleNodeIndex);
            return;
        }

        if (Hit.Kind == GroupGridHitTestKind.BodyCell && Hit.RowKind == GroupGridRowKind.DataRow && Hit.HasCell)
        {
            fEngine.SetCurrentCell(Hit.Cell);
            fEngine.SetSelectedCell(Hit.Cell);
        }
    }
    bool HandleKey(KeyEventArgs Args)
    {
        if (fEngine == null)
            return false;

        if (fEngine.IsEditing)
        {
            if (Args.Key == Key.Escape)
                return fEngine.CancelEdit();

            return false;
        }

        switch (Args.Key)
        {
            case Key.Left:
                return fEngine.MoveCurrentColumn(-1);
            case Key.Right:
                return fEngine.MoveCurrentColumn(1);
            case Key.Up:
                return fEngine.MoveCurrentRow(-1);
            case Key.Down:
                return fEngine.MoveCurrentRow(1);
            case Key.Home:
                return fEngine.MoveCurrentToFirstColumn();
            case Key.End:
                return fEngine.MoveCurrentToLastColumn();
            case Key.Tab:
                return fEngine.MoveCurrentToNextEditableCell(!Args.KeyModifiers.HasFlag(KeyModifiers.Shift));
            case Key.Enter:
            case Key.F2:
                return fEngine.BeginEdit();
        }

        return false;
    }

    // ● protected methods
    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs Args)
    {
        base.OnPointerPressed(Args);

        if (fEngine == null)
            return;

        if (!Args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Focus(NavigationMethod.Pointer, KeyModifiers.None);

        Point Point = Args.GetPosition(this);
        HandleHitTest(fEngine.HitTest(Point.X, Point.Y));
        Args.Handled = true;
    }
    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs Args)
    {
        base.OnKeyDown(Args);

        if (HandleKey(Args))
        {
            ScrollCurrentCellIntoViewCore();
            Args.Handled = true;
        }
    }
    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs Args)
    {
        base.OnPointerWheelChanged(Args);

        if (Args.Delta.Y == 0)
            return;

        int Delta = Args.Delta.Y < 0 ? 3 : -3;
        if (ScrollViewport(Delta))
            Args.Handled = true;
    }
    /// <inheritdoc />
    protected override Size ArrangeOverride(Size FinalSize)
    {
        UpdateViewport(FinalSize);
        return base.ArrangeOverride(FinalSize);
    }

    // ● constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupGrid"/> class.
    /// </summary>
    public GroupGrid()
    {
        Focusable = true;
        Engine = new GroupGridEngine();
    }

    // ● public methods
    /// <summary>
    /// Sets the first visible node index in the virtual viewport.
    /// </summary>
    /// <param name="VisibleNodeIndex">The first visible node index.</param>
    /// <returns>True if the viewport changed; otherwise, false.</returns>
    public bool SetFirstVisibleNodeIndex(int VisibleNodeIndex)
    {
        return SetFirstVisibleNodeIndexCore(VisibleNodeIndex);
    }
    /// <summary>
    /// Scrolls the current cell into the virtual viewport.
    /// </summary>
    /// <returns>True if the viewport changed; otherwise, false.</returns>
    public bool ScrollCurrentCellIntoView()
    {
        return ScrollCurrentCellIntoViewCore();
    }
    /// <summary>
    /// Returns the visible columns in display order.
    /// </summary>
    /// <returns>The visible columns.</returns>
    public IReadOnlyList<GroupGridColumn> GetVisibleColumns()
    {
        return fEngine.GetVisibleColumns();
    }
    /// <summary>
    /// Returns the visible value columns in display order.
    /// </summary>
    /// <returns>The visible value columns.</returns>
    public IReadOnlyList<GroupGridColumn> GetVisibleValueColumns()
    {
        return fEngine.GetVisibleValueColumns();
    }
    /// <summary>
    /// Adds a column to the grouping list.
    /// </summary>
    /// <param name="Column">The grid column.</param>
    /// <param name="GroupIndex">The group index, or -1 to append.</param>
    /// <returns>True if the column was grouped; otherwise, false.</returns>
    public bool GroupColumn(GroupGridColumn Column, int GroupIndex = -1)
    {
        return fEngine.GroupColumn(Column, GroupIndex);
    }
    /// <summary>
    /// Removes a column from the grouping list.
    /// </summary>
    /// <param name="Column">The grid column.</param>
    /// <returns>True if the column was ungrouped; otherwise, false.</returns>
    public bool UngroupColumn(GroupGridColumn Column)
    {
        return fEngine.UngroupColumn(Column);
    }
    /// <summary>
    /// Moves a grouped column to a new group index.
    /// </summary>
    /// <param name="Column">The grouped column.</param>
    /// <param name="GroupIndex">The new group index.</param>
    /// <returns>True if the column moved; otherwise, false.</returns>
    public bool MoveGroupedColumn(GroupGridColumn Column, int GroupIndex)
    {
        return fEngine.MoveGroupedColumn(Column, GroupIndex);
    }
    /// <summary>
    /// Moves a column to a new display index in the column collection.
    /// </summary>
    /// <param name="Column">The grid column.</param>
    /// <param name="ColumnIndex">The new column index.</param>
    /// <returns>True if the column moved; otherwise, false.</returns>
    public bool MoveColumn(GroupGridColumn Column, int ColumnIndex)
    {
        return fEngine.MoveColumn(Column, ColumnIndex);
    }
    /// <summary>
    /// Shows or hides a column.
    /// </summary>
    /// <param name="Column">The grid column.</param>
    /// <param name="IsVisible">True to show the column; false to hide it.</param>
    /// <returns>True if the column visibility changed; otherwise, false.</returns>
    public bool SetColumnVisible(GroupGridColumn Column, bool IsVisible)
    {
        return fEngine.SetColumnVisible(Column, IsVisible);
    }
    /// <summary>
    /// Sets the expanded state of a group node by visible-node index.
    /// </summary>
    /// <param name="VisibleNodeIndex">The visible-node index.</param>
    /// <param name="IsExpanded">True to expand; false to collapse.</param>
    /// <returns>True if the group state changed; otherwise, false.</returns>
    public bool SetGroupExpanded(int VisibleNodeIndex, bool IsExpanded)
    {
        return fEngine.SetGroupExpanded(VisibleNodeIndex, IsExpanded);
    }
    /// <summary>
    /// Toggles the expanded state of a group node by visible-node index.
    /// </summary>
    /// <param name="VisibleNodeIndex">The visible-node index.</param>
    /// <returns>True if the group state changed; otherwise, false.</returns>
    public bool ToggleGroupExpanded(int VisibleNodeIndex)
    {
        return fEngine.ToggleGroupExpanded(VisibleNodeIndex);
    }
    /// <summary>
    /// Clears the current cell.
    /// </summary>
    public void ClearCurrentCell()
    {
        fEngine.ClearCurrentCell();
    }
    /// <summary>
    /// Sets the current cell.
    /// </summary>
    /// <param name="Cell">The current cell.</param>
    /// <returns>True if the current cell changed; otherwise, false.</returns>
    public bool SetCurrentCell(GroupGridCell Cell)
    {
        bool Result = fEngine.SetCurrentCell(Cell);
        ScrollCurrentCellIntoViewCore();
        return Result;
    }
    /// <summary>
    /// Sets the current cell by row index and column.
    /// </summary>
    /// <param name="RowIndex">The adapter row index.</param>
    /// <param name="Column">The grid column.</param>
    /// <returns>True if the current cell changed; otherwise, false.</returns>
    public bool SetCurrentCell(int RowIndex, GroupGridColumn Column) => SetCurrentCell(new GroupGridCell(RowIndex, Column));
    /// <summary>
    /// Clears the selected cell.
    /// </summary>
    public void ClearSelection()
    {
        fEngine.ClearSelection();
    }
    /// <summary>
    /// Sets the selected cell.
    /// </summary>
    /// <param name="Cell">The selected cell.</param>
    /// <returns>True if the selected cell changed; otherwise, false.</returns>
    public bool SetSelectedCell(GroupGridCell Cell)
    {
        return fEngine.SetSelectedCell(Cell);
    }
    /// <summary>
    /// Selects the current cell.
    /// </summary>
    /// <returns>True if the selected cell changed; otherwise, false.</returns>
    public bool SelectCurrentCell()
    {
        return fEngine.SelectCurrentCell();
    }
    /// <summary>
    /// Begins editing the current cell.
    /// </summary>
    /// <returns>True if editing started; otherwise, false.</returns>
    public bool BeginEdit()
    {
        return fEngine.BeginEdit();
    }
    /// <summary>
    /// Commits the edited value.
    /// </summary>
    /// <param name="Value">The editor value.</param>
    /// <returns>True if the edit was committed; otherwise, false.</returns>
    public bool CommitEdit(object Value)
    {
        return fEngine.CommitEdit(Value);
    }
    /// <summary>
    /// Cancels the current edit.
    /// </summary>
    /// <returns>True if an edit was canceled; otherwise, false.</returns>
    public bool CancelEdit()
    {
        return fEngine.CancelEdit();
    }
    /// <summary>
    /// Moves the current cell by visible row and column deltas.
    /// </summary>
    /// <param name="RowDelta">The visible row delta.</param>
    /// <param name="ColumnDelta">The visible column delta.</param>
    /// <returns>True if the current cell moved; otherwise, false.</returns>
    public bool MoveCurrentCell(int RowDelta, int ColumnDelta)
    {
        bool Result = fEngine.MoveCurrentCell(RowDelta, ColumnDelta);
        ScrollCurrentCellIntoViewCore();
        return Result;
    }
    /// <inheritdoc />
    public override void Render(DrawingContext Context)
    {
        base.Render(Context);

        Rect BoundsRect = new(0, 0, Bounds.Width, Bounds.Height);
        DrawBand(Context, BoundsRect, fBackgroundBrush);

        if (fEngine == null)
            return;

        double Y = 0;
        DrawBand(Context, new Rect(0, Y, Bounds.Width, fEngine.LayoutMetrics.ToolBarHeight), fToolBarBrush);
        Y += fEngine.LayoutMetrics.ToolBarHeight;

        DrawGroupPanel(Context, Y, fEngine.LayoutMetrics.GroupPanelHeight, Bounds.Width);
        Y += fEngine.LayoutMetrics.GroupPanelHeight;

        DrawColumns(Context, Y, fEngine.LayoutMetrics.ColumnHeaderHeight, fHeaderBrush, false);
        Y += fEngine.LayoutMetrics.ColumnHeaderHeight;

        DrawColumns(Context, Y, fEngine.LayoutMetrics.FilterRowHeight, fFilterBrush, true);
        Y += fEngine.LayoutMetrics.FilterRowHeight;

        DrawBody(Context, Y, Bounds.Width);
        Y += fEngine.Viewport.Count * fEngine.LayoutMetrics.RowHeight;

        DrawFooter(Context, Y, fEngine.LayoutMetrics.FooterSummaryHeight);
    }

    // ● properties
    /// <summary>
    /// Gets or sets the group grid engine rendered by this control.
    /// </summary>
    public GroupGridEngine Engine
    {
        get => fEngine;
        set
        {
            value ??= new GroupGridEngine();

            if (ReferenceEquals(fEngine, value))
                return;

            DetachEngine(fEngine);
            fEngine = value;
            AttachEngine(fEngine);
            InvalidateMeasure();
            InvalidateVisual();
        }
    }
    /// <summary>
    /// Gets the grid columns.
    /// </summary>
    public ObservableCollection<GroupGridColumn> Columns => fEngine.Columns;
    /// <summary>
    /// Gets the grouped columns in grouping order.
    /// </summary>
    public IReadOnlyList<GroupGridColumn> GroupColumns => fEngine.GroupColumns;
    /// <summary>
    /// Gets or sets the data adapter.
    /// </summary>
    public IGroupGridDataAdapter DataAdapter
    {
        get => fEngine.DataAdapter;
        set
        {
            if (fOwnedDataAdapter != null && !ReferenceEquals(fOwnedDataAdapter, value))
            {
                fOwnedDataAdapter.Dispose();
                fOwnedDataAdapter = null;
                fItemsSource = null;
            }

            fEngine.DataAdapter = value;
        }
    }
    /// <summary>
    /// Gets or sets an item source that implements <see cref="IList{T}"/>.
    /// </summary>
    public object ItemsSource
    {
        get => fItemsSource;
        set => SetItemsSource(value);
    }
    /// <summary>
    /// Gets or sets a value indicating whether columns are generated from public item properties when columns are empty.
    /// </summary>
    public bool AutoGenerateColumns
    {
        get => fAutoGenerateColumns;
        set
        {
            if (fAutoGenerateColumns == value)
                return;

            fAutoGenerateColumns = value;
            GenerateColumnsFromItemType(FindListItemType(fItemsSource));
        }
    }
    /// <summary>
    /// Gets the layout metrics used by the grid.
    /// </summary>
    public GroupGridLayoutMetrics LayoutMetrics => fEngine.LayoutMetrics;
    /// <summary>
    /// Gets the current cell.
    /// </summary>
    public GroupGridCell CurrentCell => fEngine.CurrentCell;
    /// <summary>
    /// Gets the selected cell.
    /// </summary>
    public GroupGridCell SelectedCell => fEngine.SelectedCell;
    /// <summary>
    /// Gets the editing cell.
    /// </summary>
    public GroupGridCell EditingCell => fEngine.EditingCell;
    /// <summary>
    /// Gets a value indicating whether the grid has a current cell.
    /// </summary>
    public bool HasCurrentCell => fEngine.HasCurrentCell;
    /// <summary>
    /// Gets a value indicating whether the grid has a selected cell.
    /// </summary>
    public bool HasSelectedCell => fEngine.HasSelectedCell;
    /// <summary>
    /// Gets a value indicating whether the grid is editing a cell.
    /// </summary>
    public bool IsEditing => fEngine.IsEditing;
    /// <summary>
    /// Gets the viewport window into the logical visible-node list.
    /// </summary>
    public GroupGridViewport Viewport => fEngine.Viewport;
    /// <summary>
    /// Gets the number of visible projected nodes.
    /// </summary>
    public int VisibleNodeCount => fEngine.VisibleNodeCount;
    /// <summary>
    /// Gets the number of adapter rows.
    /// </summary>
    public int RowCount => fEngine.RowCount;
    /// <summary>
    /// Gets the first visible node index in the virtual viewport.
    /// </summary>
    public int FirstVisibleNodeIndex => fFirstVisibleNodeIndex;
    /// <summary>
    /// Gets the total height of the fixed non-body bands.
    /// </summary>
    public double FixedBandHeight
    {
        get
        {
            if (fEngine == null)
                return 0;

            return fEngine.LayoutMetrics.ToolBarHeight
                   + fEngine.LayoutMetrics.GroupPanelHeight
                   + fEngine.LayoutMetrics.ColumnHeaderHeight
                   + fEngine.LayoutMetrics.FilterRowHeight
                   + fEngine.LayoutMetrics.FooterSummaryHeight;
        }
    }
}
