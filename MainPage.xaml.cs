using System.Collections.ObjectModel;

namespace Uhrzeitrechner;

public partial class MainPage : ContentPage
{
    private readonly ObservableCollection<TimeEntry> _entries = new();
    private IDispatcherTimer? _clockTimer;

    public MainPage()
    {
        InitializeComponent();
        EntriesView.ItemsSource = _entries;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        UpdateClock();   // sofort einmal setzen, damit nicht "--:--:--" stehen bleibt

        _clockTimer = Dispatcher.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (s, e) => UpdateClock();
        _clockTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _clockTimer?.Stop();
    }

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

        // Minuten über 59 in Stunden umrechnen (z.B. 90 Min -> 1:30)
        var time = new TimeSpan(hours, minutes, 0);

        _entries.Add(new TimeEntry(time));

        HoursEntry.Text = "";
        MinutesEntry.Text = "";
        HoursEntry.Focus();

        UpdateTotal();
    }

    private void OnRemoveClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: TimeEntry entry })
        {
            _entries.Remove(entry);
            UpdateTotal();
        }
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        _entries.Clear();
        UpdateTotal();
    }

    private void UpdateTotal()
    {
        var total = _entries.Aggregate(TimeSpan.Zero, (sum, x) => sum + x.Time);

        // Gesamtstunden auch über 24h korrekt anzeigen (z.B. 26:15)
        int totalHours = (int)total.TotalHours;
        TotalLabel.Text = $"{totalHours}:{total.Minutes:D2}";
    }

    public class TimeEntry
    {
        public TimeSpan Time { get; }
        public string Display => $"{(int)Time.TotalHours}:{Time.Minutes:D2} Std";

        public TimeEntry(TimeSpan time) => Time = time;
    }
}
