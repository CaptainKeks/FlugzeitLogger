using Uhrzeitrechner.Models;

namespace Uhrzeitrechner.Services;

public class FlightSession
{
    public const int MaxLegs = 12;

    private readonly Func<DateTime> _nowUtc;

    public FlightSession(Func<DateTime>? nowUtc = null)
        => _nowUtc = nowUtc ?? (() => DateTime.UtcNow);

    public Flight Flight { get; private set; } = new();

    public IReadOnlyList<Leg> Legs => Flight.Legs;

    public string Registration
    {
        get => Flight.Registration;
        set => Flight.Registration = value ?? string.Empty;
    }

    private Leg? OpenLeg
        => Flight.Legs.Count > 0 && Flight.Legs[^1].Landing is null
            ? Flight.Legs[^1] : null;

    private bool HasCompletedLeg
        => Flight.Legs.Any(l => l.Takeoff is not null && l.Landing is not null);

    public bool CanOffBlock => Flight.OffBlock is null;
    public bool CanStart => Flight.OffBlock is not null && Flight.OnBlock is null
                            && OpenLeg is null && Flight.Legs.Count < MaxLegs;
    public bool CanLanding => OpenLeg is not null;
    public bool CanOnBlock => Flight.OffBlock is not null && Flight.OnBlock is null
                              && OpenLeg is null && HasCompletedLeg;
    public bool CanUndo => Flight.OffBlock is not null || Flight.Legs.Count > 0;
    public bool CanSave => Flight.OffBlock is not null && Flight.OnBlock is not null
                           && HasCompletedLeg
                           && !string.IsNullOrWhiteSpace(Flight.Registration);

    public void OffBlock()
    {
        if (!CanOffBlock) return;
        var now = _nowUtc();
        Flight.OffBlock = now;
        Flight.Date = now.Date;
    }

    public void Start()
    {
        if (!CanStart) return;
        Flight.Legs.Add(new Leg { Takeoff = _nowUtc() });
    }

    public void Landing()
    {
        if (OpenLeg is not { } leg) return;
        leg.Landing = _nowUtc();
    }

    public void OnBlock()
    {
        if (!CanOnBlock) return;
        Flight.OnBlock = _nowUtc();
    }

    public void Undo()
    {
        if (Flight.OnBlock is not null) { Flight.OnBlock = null; return; }
        if (OpenLeg is not null) { Flight.Legs.RemoveAt(Flight.Legs.Count - 1); return; }
        if (Flight.Legs.Count > 0)
        {
            Flight.Legs[^1].Landing = null; // letztes abgeschlossenes Leg -> Landing weg
            return;
        }
        if (Flight.OffBlock is not null) { Flight.OffBlock = null; }
    }

    public void Reset() => Flight = new();
}
