using Uhrzeitrechner.Services;
using Xunit;

namespace Uhrzeitrechner.Tests;

public class FlightSessionTests
{
    static FlightSession NewSession(Queue<DateTime> times)
        => new(() => times.Dequeue());

    static DateTime Utc(int h, int m) => new(2026, 6, 27, h, m, 0, DateTimeKind.Utc);

    [Fact]
    public void InitialState_OnlyOffBlockAllowed()
    {
        var s = new FlightSession();
        Assert.True(s.CanOffBlock);
        Assert.False(s.CanStart);
        Assert.False(s.CanLanding);
        Assert.False(s.CanOnBlock);
        Assert.False(s.CanSave);
    }

    [Fact]
    public void FullFlow_TwoLegs_ComputesAndAllowsSave()
    {
        var times = new Queue<DateTime>(new[]
        {
            Utc(10, 0),  // OffBlock
            Utc(10, 10), // Start 1
            Utc(10, 40), // Landing 1
            Utc(11, 0),  // Start 2
            Utc(11, 20), // Landing 2
            Utc(11, 30), // OnBlock
        });
        var s = NewSession(times);
        s.Registration = "D-ABCD";

        s.OffBlock();
        Assert.False(s.CanOffBlock);
        Assert.True(s.CanStart);

        s.Start();
        Assert.True(s.CanLanding);
        Assert.False(s.CanStart);   // offenes Leg -> kein neuer Start
        Assert.False(s.CanOnBlock);

        s.Landing();
        Assert.True(s.CanStart);    // nächstes Paar möglich
        Assert.True(s.CanOnBlock);

        s.Start();
        s.Landing();
        s.OnBlock();

        Assert.Equal(2, s.Legs.Count);
        Assert.True(s.CanSave);
        Assert.Equal(TimeSpan.FromMinutes(90), FlightMath.BlockTime(s.Flight));
        // Flugzeit = erster Start (10:10) bis letzte Landung (11:20) = 70 min
        Assert.Equal(TimeSpan.FromMinutes(70), FlightMath.FlightTime(s.Flight));
        Assert.Equal(new DateTime(2026, 6, 27), s.Flight.Date.Date);
    }

    [Fact]
    public void CannotSave_WithoutRegistration()
    {
        var times = new Queue<DateTime>(new[]
        { Utc(10,0), Utc(10,10), Utc(10,40), Utc(11,0) });
        var s = NewSession(times);
        s.OffBlock(); s.Start(); s.Landing(); s.OnBlock();
        Assert.False(s.CanSave); // kein Kennzeichen
        s.Registration = "D-XYZ";
        Assert.True(s.CanSave);
    }

    [Fact]
    public void Undo_RemovesLastCaptureInOrder()
    {
        var times = new Queue<DateTime>(new[]
        { Utc(10,0), Utc(10,10), Utc(10,40), Utc(11,0) });
        var s = NewSession(times);
        s.OffBlock(); s.Start(); s.Landing(); s.OnBlock();

        s.Undo(); // OnBlock weg
        Assert.Null(s.Flight.OnBlock);
        Assert.Single(s.Legs);

        s.Undo(); // Landing weg
        Assert.Null(s.Legs[0].Landing);
        Assert.True(s.CanLanding);

        s.Undo(); // Leg (Takeoff) weg
        Assert.Empty(s.Legs);

        s.Undo(); // OffBlock weg
        Assert.Null(s.Flight.OffBlock);
        Assert.True(s.CanOffBlock);
        Assert.False(s.CanUndo);
    }

    [Fact]
    public void MaxLegs_BlocksFurtherStarts()
    {
        var times = new Queue<DateTime>();
        var t = Utc(8, 0);
        times.Enqueue(t); // OffBlock
        for (int i = 0; i < FlightSession.MaxLegs; i++)
        {
            times.Enqueue(t.AddMinutes(1)); // Start
            times.Enqueue(t.AddMinutes(2)); // Landing
            t = t.AddMinutes(10);
        }
        var s = new FlightSession(() => times.Dequeue());
        s.OffBlock();
        for (int i = 0; i < FlightSession.MaxLegs; i++) { s.Start(); s.Landing(); }
        Assert.Equal(FlightSession.MaxLegs, s.Legs.Count);
        Assert.False(s.CanStart); // Maximum erreicht
        Assert.True(s.CanOnBlock);
    }
}
