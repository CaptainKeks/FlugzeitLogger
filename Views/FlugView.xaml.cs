using System.Collections.ObjectModel;
using Uhrzeitrechner.Services;

namespace Uhrzeitrechner.Views;

public partial class FlugView : ContentView, ITabView
{
    private readonly FlightSession _session = new();
    private readonly FlightLogService _log = new(AppPaths.FlightLogPath);
    private readonly FlightSessionStore _store = new(AppPaths.SessionPath);
    private bool _restored;
    private readonly ObservableCollection<LegRow> _legRows = new();
    private IDispatcherTimer? _clockTimer;

    public FlugView()
    {
        InitializeComponent();
        LegsView.ItemsSource = _legRows;
        RefreshState();
    }

    public async void OnSelected()
    {
        _clockTimer?.Stop(); // guard against ghost timer on double-select

        if (!_restored)
        {
            _restored = true;
            var saved = await _store.LoadAsync();
            if (saved is not null)
            {
                _session.Restore(saved);
                RegistrationEntry.Text = _session.Registration;
                RefreshState();
            }
        }

        UpdateClock();
        _clockTimer = Dispatcher.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (s, e) => UpdateClock();
        _clockTimer.Start();
    }

    public void OnDeselected()
    {
        _clockTimer?.Stop();
        _clockTimer = null;
        _ = PersistAsync();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        UtcTimeLabel.Text = now.ToUniversalTime().ToString("HH:mm:ss");
        LocalTimeLabel.Text = now.ToString("HH:mm:ss");
        DateLabel.Text = now.ToString("dd.MM.yyyy");
    }

    private void OnRegistrationChanged(object? sender, TextChangedEventArgs e)
    {
        _session.Registration = e.NewTextValue ?? string.Empty;
        RefreshState();
    }

    private async void OnRegistrationCompleted(object? sender, EventArgs e) { RegistrationEntry.Unfocus(); await PersistAsync(); }

    private async void OnOffBlockClicked(object? sender, EventArgs e) { _session.OffBlock(); RefreshState(); await PersistAsync(); }
    private async void OnStartClicked(object? sender, EventArgs e) { _session.Start(); RefreshState(); await PersistAsync(); }
    private async void OnLandingClicked(object? sender, EventArgs e) { _session.Landing(); RefreshState(); await PersistAsync(); }
    private async void OnGoAroundClicked(object? sender, EventArgs e) { _session.GoAround(); RefreshState(); await PersistAsync(); }
    private async void OnOnBlockClicked(object? sender, EventArgs e) { _session.OnBlock(); RefreshState(); await PersistAsync(); }
    private async void OnUndoClicked(object? sender, EventArgs e) { _session.Undo(); RefreshState(); await PersistAsync(); }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!_session.CanSave) return;
        await _log.AddAsync(_session.Flight);
        _session.Reset();
        await _store.ClearAsync();
        RegistrationEntry.Text = string.Empty;
        RefreshState();
        await Shell.Current.DisplayAlertAsync("Gespeichert", "Flug wurde im Logbuch gespeichert.", "OK");
    }

    private async void OnResetClicked(object? sender, EventArgs e)
    {
        bool ok = await Shell.Current.DisplayAlertAsync("Zurücksetzen",
            "Aktuellen Flug verwerfen?", "Ja", "Abbrechen");
        if (!ok) return;
        _session.Reset();
        await _store.ClearAsync();
        RegistrationEntry.Text = string.Empty;
        RefreshState();
    }

    private async Task PersistAsync()
    {
        try { await _store.SaveAsync(_session.Flight); }
        catch { /* best-effort: Persistenz darf die UI nicht stören */ }
    }

    private void RefreshState()
    {
        OffBlockButton.IsEnabled = _session.CanOffBlock;
        StartButton.IsEnabled = _session.CanStart;
        LandingButton.IsEnabled = _session.CanLanding;
        GoAroundButton.IsEnabled = _session.CanGoAround;
        OnBlockButton.IsEnabled = _session.CanOnBlock;
        UndoButton.IsEnabled = _session.CanUndo;
        SaveButton.IsEnabled = _session.CanSave;

        _legRows.Clear();
        for (int i = 0; i < _session.Legs.Count; i++)
            _legRows.Add(new LegRow(i + 1, _session.Legs[i]));

        OffBlockResultLabel.Text = _session.Flight.OffBlock?.ToString("HH:mm") ?? "—";
        OnBlockResultLabel.Text = _session.Flight.OnBlock?.ToString("HH:mm") ?? "—";

        FirstTakeoffLabel.Text = _session.Legs.Count > 0
            ? _session.Legs[0].Takeoff?.ToString("HH:mm") ?? "—"
            : "—";

        var lastLanding = _session.Legs.LastOrDefault(l => l.Landing is not null)?.Landing;
        LastLandingLabel.Text = lastLanding?.ToString("HH:mm") ?? "—";

        LandingCountLabel.Text = _session.LandingCount > 0 ? _session.LandingCount.ToString() : "—";
        GoAroundCountLabel.Text = _session.GoAroundCount > 0 ? _session.GoAroundCount.ToString() : "—";

        BlockTimeLabel.Text = FlightMath.FormatDuration(FlightMath.BlockTime(_session.Flight));
        FlightTimeLabel.Text = FlightMath.FormatDuration(FlightMath.FlightTime(_session.Flight));
    }

    public class LegRow
    {
        public string Display { get; }
        public LegRow(int index, Models.Leg leg)
        {
            string to = leg.Takeoff?.ToString("HH:mm:ss") ?? "—";
            string la = leg.Landing?.ToString("HH:mm:ss") ?? "—";
            string endLabel = leg.GoAround ? "Go-Around" : "Landung";
            Display = $"Start {index}: {to}   /   {endLabel} {index}: {la}";
        }
    }
}
