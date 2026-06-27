using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;

namespace Uhrzeitrechner;

public partial class FlightDetailPage : ContentPage, IQueryAttributable
{
    public FlightDetailPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("flight", out var value) && value is Flight flight)
            Render(flight);
    }

    private void Render(Flight flight)
    {
        HeaderLabel.Text = $"{flight.Date:dd.MM.yyyy} · {flight.Registration}";
        OffBlockLabel.Text = flight.OffBlock is { } off ? off.ToString("HH:mm:ss") + " UTC" : "—";
        OnBlockLabel.Text = flight.OnBlock is { } on ? on.ToString("HH:mm:ss") + " UTC" : "—";

        var legLines = new List<string>();
        for (int i = 0; i < flight.Legs.Count; i++)
        {
            var leg = flight.Legs[i];
            string to = leg.Takeoff?.ToString("HH:mm:ss") ?? "—";
            string la = leg.Landing?.ToString("HH:mm:ss") ?? "—";
            legLines.Add($"Start {i + 1}: {to}   /   Landung {i + 1}: {la}");
        }
        BindableLayout.SetItemsSource(LegsStack, legLines);

        BlockTimeLabel.Text = FlightMath.FormatDuration(FlightMath.BlockTime(flight));
        FlightTimeLabel.Text = FlightMath.FormatDuration(FlightMath.FlightTime(flight));
        LandingCountLabel.Text = flight.Legs.Count(l => l.Landing is not null).ToString();
    }
}
