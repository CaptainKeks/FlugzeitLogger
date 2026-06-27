using Uhrzeitrechner.Views;

namespace Uhrzeitrechner;

public partial class MainTabsPage : ContentPage
{
    private const int StartIndex = 1; // Flug

    private readonly List<View> _views;
    private readonly ITabView[] _tabs;
    private readonly Button[] _tabButtons;
    private int _activeIndex = -1;
    private bool _started;

    public MainTabsPage()
    {
        InitializeComponent();

        var stunden = new StundenView();
        var flug = new FlugView();
        var logbuch = new LogbuchView();
        var einstellungen = new EinstellungenView();

        _views = new List<View> { stunden, flug, logbuch, einstellungen };
        _tabs = new ITabView[] { stunden, flug, logbuch, einstellungen };
        _tabButtons = new[] { TabStunden, TabFlug, TabLogbuch, TabEinstellungen };

        Pager.ItemsSource = _views;
        UpdateHighlight(StartIndex);

        Pager.PositionChanged += OnPositionChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_started)
        {
            _started = true;
            Pager.Position = StartIndex;
            Activate(StartIndex);
        }
        else
        {
            Activate(Pager.Position);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Deactivate();
    }

    private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
        => Activate(e.CurrentPosition);

    private void OnTabStunden(object? sender, EventArgs e) => Pager.Position = 0;
    private void OnTabFlug(object? sender, EventArgs e) => Pager.Position = 1;
    private void OnTabLogbuch(object? sender, EventArgs e) => Pager.Position = 2;
    private void OnTabEinstellungen(object? sender, EventArgs e) => Pager.Position = 3;

    private void Activate(int index)
    {
        if (index < 0 || index >= _tabs.Length) return;
        if (_activeIndex == index) return;
        if (_activeIndex >= 0) _tabs[_activeIndex].OnDeselected();
        _activeIndex = index;
        _tabs[index].OnSelected();
        UpdateHighlight(index);
    }

    private void Deactivate()
    {
        if (_activeIndex < 0) return;
        _tabs[_activeIndex].OnDeselected();
        _activeIndex = -1;
    }

    private void UpdateHighlight(int index)
    {
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            _tabButtons[i].TextColor = i == index ? Colors.DodgerBlue : Colors.Gray;
            _tabButtons[i].FontAttributes = i == index ? FontAttributes.Bold : FontAttributes.None;
        }
    }
}
