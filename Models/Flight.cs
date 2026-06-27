namespace Uhrzeitrechner.Models;

public class Flight
{
    public DateTime Date { get; set; }
    public string Registration { get; set; } = string.Empty;
    public DateTime? OffBlock { get; set; }
    public DateTime? OnBlock { get; set; }
    public List<Leg> Legs { get; set; } = new();
}
