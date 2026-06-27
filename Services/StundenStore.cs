using System.Collections.ObjectModel;
using Uhrzeitrechner.Models;

namespace Uhrzeitrechner.Services;

public class StundenStore
{
    public static StundenStore Instance { get; } = new();

    public ObservableCollection<TimeEntry> Entries { get; } = new();

    public TimeSpan Total => Entries.Aggregate(TimeSpan.Zero, (sum, x) => sum + x.Time);

    public static string FormatTotal(TimeSpan total)
        => $"{(int)total.TotalHours}:{total.Minutes:D2}";

    public void Add(TimeSpan time) => Entries.Add(new TimeEntry(time));

    public void Remove(TimeEntry entry) => Entries.Remove(entry);

    public void Clear() => Entries.Clear();
}
