namespace Uhrzeitrechner.Models;

public class TimeEntry
{
    public TimeSpan Time { get; }
    public string Display => $"{(int)Time.TotalHours}:{Time.Minutes:D2} Std";

    public TimeEntry(TimeSpan time) => Time = time;
}
