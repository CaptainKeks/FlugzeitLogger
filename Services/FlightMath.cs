using Uhrzeitrechner.Models;

namespace Uhrzeitrechner.Services;

public static class FlightMath
{
    public static TimeSpan? BlockTime(Flight f)
        => f.OffBlock is { } off && f.OnBlock is { } on ? on - off : null;

    // Flugzeit = erster Start bis zur letzten Landung (nicht die Summe der einzelnen Legs)
    // null, solange noch keine vollständige Flugzeit vorliegt -> Anzeige "—"
    public static TimeSpan? FlightTime(Flight f)
    {
        var firstTakeoff = f.Legs.FirstOrDefault(l => l.Takeoff is not null)?.Takeoff;
        var lastLanding = f.Legs.LastOrDefault(l => l.Landing is not null)?.Landing;
        if (firstTakeoff is { } start && lastLanding is { } end)
            return end - start;
        return null;
    }

    public static string FormatDuration(TimeSpan? t)
    {
        if (t is not { } v) return "—";
        // Sekunden auf ganze Minuten runden (kaufmännisch), damit keine Sekunden entstehen/angezeigt werden.
        long totalMinutes = (long)Math.Round(v.TotalMinutes, MidpointRounding.AwayFromZero);
        return $"{totalMinutes / 60}:{totalMinutes % 60:D2}";
    }
}
