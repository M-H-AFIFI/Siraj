using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Siraj.App.Controls;

internal sealed class MasterDetail : Grid
{
    private readonly ScrollViewer _list = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 12, 0) };
    private readonly ScrollViewer _detail = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Grid _detailFrame = new();
    private readonly Button _back;
    private bool _narrow;
    private bool _showingDetail;

    public double ListWidth { get; init; } = 380;

    public MasterDetail(UIElement header)
    {
        Padding = new Thickness(32, 12, 32, 0);
        RowSpacing = 14;
        ColumnSpacing = 20;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        SetColumnSpan((FrameworkElement)header, 2);
        Children.Add(header);
        SetRow(_list, 1);
        Children.Add(_list);

        _back = Kit.Back(() => ShowList());
        _back.Margin = new Thickness(0, 0, 0, 4);
        _back.HorizontalAlignment = HorizontalAlignment.Left;
        _detailFrame.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _detailFrame.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _detailFrame.Children.Add(_back);
        SetRow(_detail, 1);
        _detailFrame.Children.Add(_detail);
        SetRow(_detailFrame, 1);
        SetColumn(_detailFrame, 1);
        Children.Add(_detailFrame);
        SizeChanged += (_, e) => Arrange(e.NewSize.Width);
        Loaded += (_, _) => ColumnDefinitions[0].Width = _narrow ? new GridLength(1, GridUnitType.Star) : new GridLength(ListWidth);
    }

    public UIElement List { set => _list.Content = value; }

    public ScrollViewer ListScroller => _list;

    public void Show(UIElement content, bool open = true)
    {
        _detail.Content = content;
        _detail.ChangeView(null, 0, null, true);
        if (open) _showingDetail = true;
        Apply();
    }

    private void ShowList()
    {
        _showingDetail = false;
        Apply();
    }

    private void Arrange(double width)
    {
        bool narrow = width < 860;
        if (narrow == _narrow && ColumnDefinitions[0].Width.Value > 0) { Apply(); return; }
        _narrow = narrow;
        Apply();
    }

    private void Apply()
    {
        if (_narrow)
        {
            ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinitions[1].Width = new GridLength(0);
            SetColumn(_detailFrame, 0);
            _list.Visibility = _showingDetail ? Visibility.Collapsed : Visibility.Visible;
            _detailFrame.Visibility = _showingDetail ? Visibility.Visible : Visibility.Collapsed;
            _back.Visibility = Visibility.Visible;
            _list.Padding = new Thickness(0);
        }
        else
        {
            ColumnDefinitions[0].Width = new GridLength(ListWidth);
            ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            SetColumn(_detailFrame, 1);
            _list.Visibility = Visibility.Visible;
            _detailFrame.Visibility = Visibility.Visible;
            _back.Visibility = Visibility.Collapsed;
            _list.Padding = new Thickness(0, 0, 12, 0);
        }
    }
}
