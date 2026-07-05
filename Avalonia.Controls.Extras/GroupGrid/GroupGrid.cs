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
    static readonly IBrush fFooterBrush = new SolidColorBrush(Color.FromRgb(238, 240, 242));
    static readonly IBrush fTextBrush = new SolidColorBrush(Color.FromRgb(32, 37, 42));
    static readonly IBrush fMutedTextBrush = new SolidColorBrush(Color.FromRgb(84, 91, 99));
    static readonly Pen fLinePen = new(new SolidColorBrush(Color.FromRgb(211, 216, 222)), 1);
    static readonly Pen fCurrentPen = new(new SolidColorBrush(Color.FromRgb(64, 122, 190)), 1);
    GroupGridEngine fEngine;

    // ● private methods
    void Engine_Changed(object Sender, EventArgs Args)
    {
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
        GroupGridViewport Viewport = Count <= 0
            ? GroupGridViewport.Empty
            : new GroupGridViewport(0, Count - 1);

        fEngine.SetViewport(Viewport);
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
            if (RowInfo.IsDataRow && fEngine.IsSelectedRow(RowInfo.RowIndex))
                CellBrush = fSelectedBrush;
            if (RowInfo.IsDataRow && fEngine.CurrentCell == new GroupGridCell(RowInfo.RowIndex, Column))
                CellBrush = fCurrentBrush;

            Context.DrawRectangle(CellBrush, fLinePen, CellRect);
            DrawText(Context, fEngine.GetDisplayText(VisibleNodeIndex, Column), CellRect, RowInfo.IsGroupSummary ? fMutedTextBrush : fTextBrush);

            if (RowInfo.IsDataRow && fEngine.CurrentCell == new GroupGridCell(RowInfo.RowIndex, Column))
                Context.DrawRectangle(null, fCurrentPen, CellRect);

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

    // ● protected methods
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
        Engine = new GroupGridEngine();
    }

    // ● public methods
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
