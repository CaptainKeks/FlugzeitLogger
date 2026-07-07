namespace Uhrzeitrechner.Models;

public class Leg
{
    public DateTime? Takeoff { get; set; }
    public DateTime? Landing { get; set; }

    // Leg wurde mit einem Go-Around (Durchstart) abgeschlossen statt mit einer Landung.
    // Zeitlich zählt es wie eine Landung, wird aber getrennt gezählt.
    public bool GoAround { get; set; }
}
