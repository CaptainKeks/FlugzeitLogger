using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;
using Xunit;

namespace Uhrzeitrechner.Tests;

public class FlightMathTests
{
    static DateTime Utc(int h, int m) => new(2026, 6, 27, h, m, 0, DateTimeKind.Utc);

    [Fact]
    public void BlockTime_IsOnBlockMinusOffBlock()
    {
        var f = new Flight { OffBlock = Utc(10, 0), OnBlock = Utc(12, 30) };
        Assert.Equal(TimeSpan.FromMinutes(150), FlightMath.BlockTime(f));
    }

    [Fact]
    public void BlockTime_NullWhenOnBlockMissing()
    {
        var f = new Flight { OffBlock = Utc(10, 0) };
        Assert.Null(FlightMath.BlockTime(f));
    }

    [Fact]
    public void FlightTime_SumsCompletedLegsOnly()
    {
        var f = new Flight();
        f.Legs.Add(new Leg { Takeoff = Utc(10, 10), Landing = Utc(10, 40) }); // 30
        f.Legs.Add(new Leg { Takeoff = Utc(11, 0), Landing = Utc(11, 20) });  // 20
        f.Legs.Add(new Leg { Takeoff = Utc(12, 0) });                          // offen -> 0
        Assert.Equal(TimeSpan.FromMinutes(50), FlightMath.FlightTime(f));
    }

    [Fact]
    public void FormatDuration_FormatsHoursAndMinutes()
    {
        Assert.Equal("2:05", FlightMath.FormatDuration(TimeSpan.FromMinutes(125)));
        Assert.Equal("26:00", FlightMath.FormatDuration(TimeSpan.FromHours(26)));
        Assert.Equal("—", FlightMath.FormatDuration(null));
    }
}
