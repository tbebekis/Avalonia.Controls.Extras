// Copyright (c) 2026 Theodoros Bebekis
// Licensed under the MIT License.

namespace Avalonia.Controls;

/// <summary>
/// Provides a custom drop-down in-place editor for lookup group grid cells.
/// </summary>
public class GroupGridLookupInplaceEditor: GroupGridInplaceEditorBase
{
    // ● private fields
    readonly TextBox fTextBox;
    readonly Button fDropDownButton;
    readonly GroupGridLookupColumn fColumn;
    readonly List<GroupGridLookupEditorItem> fItems = new();
    GroupGridLookupEditorItem fSelectedItem;

    // ● private methods
    object GetMemberValue(object Item, string Member)
    {
        if (Item == null || string.IsNullOrWhiteSpace(Member))
            return null;
        if (Item is DataRowView RowView)
            return RowView.Row.Table.Columns.Contains(Member) ? RowView[Member] : null;
        if (Item is DataRow Row)
            return Row.Table.Columns.Contains(Member) ? Row[Member] : null;

        PropertyInfo Property = Item.GetType().GetProperty(Member, BindingFlags.Public | BindingFlags.Instance);
        return Property == null ? null : Property.GetValue(Item);
    }
    void LoadItems()
    {
        fItems.Clear();
        if (fColumn.LookupItemsSource == null)
            return;

        foreach (object Item in fColumn.LookupItemsSource)
        {
            object DisplayValue = GetMemberValue(Item, fColumn.DisplayMember);
            fItems.Add(new GroupGridLookupEditorItem
            {
                Item = Item,
                Text = Convert.ToString(DisplayValue, CultureInfo.CurrentCulture) ?? string.Empty,
            });
        }
    }
    void SelectValue(object Value)
    {
        foreach (GroupGridLookupEditorItem Item in fItems)
        {
            object ItemValue = GetMemberValue(Item.Item, fColumn.ValueMember);
            if (Equals(ItemValue, Value))
            {
                SetSelectedItem(Item);
                return;
            }
        }

        SetSelectedItem(null);
    }
    void SetSelectedItem(GroupGridLookupEditorItem Item)
    {
        fSelectedItem = Item;
        fTextBox.Text = Item?.Text ?? string.Empty;
        RaiseValueChanged();
    }
    void MoveSelection(int Delta)
    {
        if (fItems.Count == 0)
            return;

        int Index = fSelectedItem == null ? -1 : fItems.IndexOf(fSelectedItem);
        int NewIndex = Math.Clamp(Index + Delta, 0, fItems.Count - 1);
        SetSelectedItem(fItems[NewIndex]);
    }
    void DropDownButton_PointerPressed(object Sender, PointerPressedEventArgs Args)
    {
        RaiseDropDownRequested();
        FocusEditor();
        Args.Handled = true;
    }
    void Editor_KeyDown(object Sender, KeyEventArgs Args)
    {
        if (Args.Key == Key.Down && !Args.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            MoveSelection(1);
            Args.Handled = true;
            return;
        }
        if (Args.Key == Key.Up && !Args.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            MoveSelection(-1);
            Args.Handled = true;
            return;
        }
        if (Args.Key == Key.Down && Args.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            MoveSelection(1);
            Args.Handled = true;
            return;
        }
        if (Args.Key == Key.Up && Args.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            MoveSelection(-1);
            Args.Handled = true;
            return;
        }
        if (Args.Key == Key.F4)
        {
            RaiseDropDownRequested();
            Args.Handled = true;
        }
    }
    void CleanTextBoxChrome()
    {
        fTextBox.BorderThickness = new Thickness(0);
        fTextBox.FocusAdorner = null;
        foreach (Border Border in fTextBox.GetVisualDescendants().OfType<Border>())
        {
            Border.BorderThickness = new Thickness(0);
            Border.CornerRadius = new CornerRadius(0);
        }
    }
    TextBox CreateTextBox()
    {
        return new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FocusAdorner = null,
            IsReadOnly = true,
            FontSize = 12,
            Padding = new Thickness(2, 0, 2, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = 0,
        };
    }
    Button CreateDropDownButton()
    {
        return new Button
        {
            Content = "▾",
            Width = 16,
            FontSize = 10,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Focusable = false,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
    }
    Control CreateEditorPanel()
    {
        Grid Panel = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Background = Brushes.Transparent,
        };
        Grid.SetColumn(fTextBox, 0);
        Grid.SetColumn(fDropDownButton, 1);
        Panel.Children.Add(fTextBox);
        Panel.Children.Add(fDropDownButton);
        return Panel;
    }

    // ● protected methods
    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs Args)
    {
        base.OnPointerPressed(Args);

        Point Point = Args.GetPosition(this);
        if (Point.X < Math.Max(0, Bounds.Width - 20))
            return;

        RaiseDropDownRequested();
        FocusEditor();
        Args.Handled = true;
    }

    // ● constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupGridLookupInplaceEditor"/> class.
    /// </summary>
    /// <param name="Column">The lookup column.</param>
    public GroupGridLookupInplaceEditor(GroupGridLookupColumn Column)
    {
        fColumn = Column ?? throw new ArgumentNullException(nameof(Column));
        LoadItems();
        fTextBox = CreateTextBox();
        fDropDownButton = CreateDropDownButton();
        fDropDownButton.AddHandler(InputElement.PointerPressedEvent, DropDownButton_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        fTextBox.KeyDown += Editor_KeyDown;
        SetEditorControl(CreateEditorPanel());
        Dispatcher.UIThread.Post(CleanTextBoxChrome, DispatcherPriority.Background);
    }

    // ● public methods
    /// <inheritdoc />
    public override void FocusEditor()
    {
        fTextBox.Focus();
    }
    /// <inheritdoc />
    public override void Cleanup()
    {
        fDropDownButton.RemoveHandler(InputElement.PointerPressedEvent, DropDownButton_PointerPressed);
        fTextBox.KeyDown -= Editor_KeyDown;
    }

    // ● properties
    /// <inheritdoc />
    public override object Value
    {
        get => fSelectedItem?.Item;
        set => SelectValue(value);
    }
    /// <inheritdoc />
    public override bool HasDropDown => true;
    /// <inheritdoc />
    public override IEnumerable DropDownItems => fItems;
    /// <inheritdoc />
    public override object SelectedDropDownItem => fSelectedItem;
    /// <inheritdoc />
    public override void SelectDropDownItem(object Item)
    {
        SetSelectedItem(Item as GroupGridLookupEditorItem);
    }

    // ● private types
    /// <summary>
    /// Represents a display item for the lookup drop-down list.
    /// </summary>
    class GroupGridLookupEditorItem
    {
        // ● properties
        /// <summary>
        /// Gets or sets the source lookup item.
        /// </summary>
        public object Item { get; set; }
        /// <summary>
        /// Gets or sets the display text.
        /// </summary>
        public string Text { get; set; } = string.Empty;
        /// <inheritdoc />
        public override string ToString()
        {
            return Text;
        }
    }
}
