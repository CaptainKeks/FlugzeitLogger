using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;
using Xunit;

namespace Uhrzeitrechner.Tests;

public class FlightSessionRestoreTests
{
    static DateTime Utc(int h, int m) => new(2026, 6, 27, h, m, 0, DateTimeKind.Utc);

    [Fact]
    public void Restore_SetsCurrentFlight()
    {
        var s = new FlightSession();
        var flight = new Flight
        {
            Registration = "D-TEST",
            Date = new DateTime(2026, 6, 27),
            OffBlock = Utc(9, 0),
            Legs = { new Leg { Takeoff = Utc(9, 10), Landing = Utc(9, 40) } },
        };

        s.Restore(flight);

        Assert.Equal("D-TEST", s.Registration);
        Assert.Equal(Utc(9, 0), s.Flight.OffBlock);
        Assert.Single(s.Legs);
        Assert.False(s.CanOffBlock); // OffBlock bereits gesetzt
    }

    [Fact]
    public void Restore_WithNull_ResetsToEmpty()
    {
        var s = new FlightSession();
        s.Restore(null!);

        Assert.True(s.CanOffBlock);
        Assert.Empty(s.Legs);
        Assert.Equal(string.Empty, s.Registration);
    }
}
