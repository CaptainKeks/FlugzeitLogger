using System.Collections.ObjectModel;
using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;

namespace Uhrzeitrechner;

public partial class LogbookPage : ContentPage
{
    private readonly FlightLogService _log =
        new(Path.Combine(FileSystem.AppDataDirectory, "flights.json"));
    private readonly ObservableCollection<FlightRow> _rows = new();

    public LogbookPage()
    {
        InitializeComponent();
        FlightsView.ItemsSource = _rows;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var flights = await _log.LoadAsync();
        flights.Sort((a, b) => DateTime.Compare(
            b.OffBlock ?? b.Date, a.OffBlock ?? a.Date)); // neueste oben
        _rows.Clear();
        foreach (var f in flights)
            _rows.Add(new FlightRow(f));
    }

    private async void OnFlightSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not FlightRow row) return;
        FlightsView.SelectedItem = null; // Auswahl zurücksetzen
        await Shell.Current.GoToAsync(nameof(FlightDetailPage),
            new Dictionary<string, object> { ["flight"] = row.Flight });
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Flight flight }) return;
        bool ok = await DisplayAlertAsync("Löschen",
            $"Flug {flight.Registration} löschen?", "Ja", "Abbrechen");
        if (!ok) return;
        await _log.DeleteAsync(flight);
        await ReloadAsync();
    }

    public class FlightRow
    {
        public Flight Flight { get; }
        public string DateText => Flight.Date.ToString("dd.MM.yyyy");
        public string Registration => Flight.Registration;
        public string Summary =>
            $"Block {FlightMath.FormatDuration(FlightMath.BlockTime(Flight))} · " +
            $"Flug {FlightMath.FormatDuration(FlightMath.FlightTime(Flight))}";
        public FlightRow(Flight flight) => Flight = flight;
    }
}
