using System.Text.Json;
using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;

namespace Uhrzeitrechner;

public partial class SettingsPage : ContentPage
{
    private readonly FlightLogService _log = new(AppPaths.FlightLogPath);

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        PathLabel.Text = _log.FilePath;
        var flights = await _log.LoadAsync();
        CountLabel.Text = $"{flights.Count} Flüge im Logbuch";
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (!File.Exists(_log.FilePath))
        {
            await DisplayAlertAsync("Export", "Es sind noch keine Flüge gespeichert.", "OK");
            return;
        }
        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Logbuch exportieren",
                File = new ShareFile(_log.FilePath),
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Fehler", $"Export fehlgeschlagen: {ex.Message}", "OK");
        }
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Logbuch-Datei wählen",
            });
            if (result is null) return; // abgebrochen

            await using var stream = await result.OpenReadAsync();
            var incoming = await JsonSerializer.DeserializeAsync<List<Flight>>(stream);
            if (incoming is null)
            {
                await DisplayAlertAsync("Import", "Datei enthält keine Flüge.", "OK");
                return;
            }

            int added = await _log.MergeAsync(incoming);
            int skipped = incoming.Count - added;
            await DisplayAlertAsync("Import",
                $"{added} Flüge importiert, {skipped} übersprungen.", "OK");

            var flights = await _log.LoadAsync();
            CountLabel.Text = $"{flights.Count} Flüge im Logbuch";
        }
        catch (JsonException)
        {
            await DisplayAlertAsync("Fehler", "Die Datei ist keine gültige Logbuch-Datei.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Fehler", $"Import fehlgeschlagen: {ex.Message}", "OK");
        }
    }
}
