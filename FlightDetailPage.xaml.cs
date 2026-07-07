using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;

namespace Uhrzeitrechner;

public partial class FlightDetailPage : ContentPage, IQueryAttributable
{
    private readonly FlightLogService _log = new(AppPaths.FlightLogPath);
    private Flight? _flight;

    public FlightDetailPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("flight", out var value) && value is Flight flight)
        {
            _flight = flight;
            Render(flight);
        }
    }

    private async void OnEditRegistrationClicked(object? sender, EventArgs e)
    {
        if (_flight is null) return;

        string? result = await DisplayPromptAsync(
            "Flugnamen ändern",
            "Neuer Flugname:",
            accept: "Speichern",
            cancel: "Abbrechen",
            initialValue: _flight.Registration);

        if (string.IsNullOrWhiteSpace(result)) return;

        result = result.Trim();
        if (result == _flight.Registration) return;

        bool saved = await _log.UpdateRegistrationAsync(_flight, result);
        if (!saved)
        {
            await DisplayAlertAsync("Fehler", "Flug konnte nicht gefunden werden.", "OK");
            return;
        }

        _flight.Registration = result;
        Render(_flight);
    }

    private void Render(Flight flight)
    {
        HeaderLabel.FormattedText = new FormattedString
        {
            Spans =
            {
                new Span { Text = $"{flight.Date:dd.MM.yyyy} : " },
                new Span { Text = flight.Registration, TextColor = Colors.DodgerBlue, FontAttributes = FontAttributes.Bold },
            }
        };
        OffBlockLabel.Text = flight.OffBlock is { } off ? off.ToString("HH:mm:ss") + " UTC" : "—";
        OnBlockLabel.Text = flight.OnBlock is { } on ? on.ToString("HH:mm:ss") + " UTC" : "—";

        var legLines = new List<string>();
        for (int i = 0; i < flight.Legs.Count; i++)
        {
            var leg = flight.Legs[i];
            string to = leg.Takeoff?.ToString("HH:mm:ss") ?? "—";
            string la = leg.Landing?.ToString("HH:mm:ss") ?? "—";
            string endLabel = leg.GoAround ? "Go-Around" : "Landung";
            legLines.Add($"Start {i + 1}: {to}   /   {endLabel} {i + 1}: {la}");
        }
        BindableLayout.SetItemsSource(LegsStack, legLines);

        BlockTimeLabel.Text = FlightMath.FormatDuration(FlightMath.BlockTime(flight));
        FlightTimeLabel.Text = FlightMath.FormatDuration(FlightMath.FlightTime(flight));
        LandingCountLabel.Text = flight.Legs.Count(l => l.Landing is not null && !l.GoAround).ToString();
        GoAroundCountLabel.Text = flight.Legs.Count(l => l.Landing is not null && l.GoAround).ToString();
    }
}
