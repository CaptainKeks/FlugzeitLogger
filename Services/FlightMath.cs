using Uhrzeitrechner.Models;

namespace Uhrzeitrechner.Services;

public static class FlightMath
{
    public static TimeSpan? BlockTime(Flight f)
        => f.OffBlock is { } off && f.OnBlock is { } on ? on - off : null;

    public static TimeSpan FlightTime(Flight f)
    {
        var total = TimeSpan.Zero;
        foreach (var leg in f.Legs)
            if (leg.Takeoff is { } to && leg.Landing is { } la)
                total += la - to;
        return total;
    }

    public static string FormatDuration(TimeSpan? t)
    {
        if (t is not { } v) return "—";
        return $"{(int)v.TotalHours}:{v.Minutes:D2}";
    }
}
