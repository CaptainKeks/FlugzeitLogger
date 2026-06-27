using Uhrzeitrechner.Services;

namespace Uhrzeitrechner.Views;

public partial class StundenView : ContentView, ITabView
{
    private readonly StundenStore _store = StundenStore.Instance;
    private IDispatcherTimer? _clockTimer;

    public StundenView()
    {
        InitializeComponent();
        EntriesView.ItemsSource = _store.Entries;
        UpdateTotal();
    }

    public void OnSelected()
    {
        UpdateClock();
        _clockTimer = Dispatcher.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (s, e) => UpdateClock();
        _clockTimer.Start();
    }

    public void OnDeselected() => _clockTimer?.Stop();

    private void UpdateClock()
    {
        var now = DateTime.Now;
        LocalTimeLabel.Text = now.ToString("HH:mm:ss");
        UtcTimeLabel.Text = now.ToUniversalTime().ToString("HH:mm:ss");
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        int.TryParse(HoursEntry.Text, out int hours);
        int.TryParse(MinutesEntry.Text, out int minutes);

        if (hours == 0 && minutes == 0)
            return;

        _store.Add(new TimeSpan(hours, minutes, 0));

        HoursEntry.Text = "";
        MinutesEntry.Text = "";
        HoursEntry.Focus();

        UpdateTotal();
    }

    private void OnRemoveClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Models.TimeEntry entry })
        {
            _store.Remove(entry);
            UpdateTotal();
        }
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        _store.Clear();
        UpdateTotal();
    }

    private void UpdateTotal() => TotalLabel.Text = StundenStore.FormatTotal(_store.Total);
}
