using System.Text.Json;
using Uhrzeitrechner.Models;

namespace Uhrzeitrechner.Services;

public class FlightLogService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public FlightLogService(string filePath) => _filePath = filePath;

    public string FilePath => _filePath;

    public async Task<List<Flight>> LoadAsync()
    {
        if (!File.Exists(_filePath)) return new();
        try
        {
            await using var stream = File.OpenRead(_filePath);
            var flights = await JsonSerializer.DeserializeAsync<List<Flight>>(stream, Options);
            return flights ?? new();
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            return new(); // beschädigt oder nicht lesbar -> wie leeres Logbuch behandeln
        }
    }

    public async Task SaveAllAsync(List<Flight> flights)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, flights, Options);
    }

    public async Task AddAsync(Flight flight)
    {
        var all = await LoadAsync();
        all.Add(flight);
        await SaveAllAsync(all);
    }

    public async Task DeleteAsync(Flight flight)
    {
        var all = await LoadAsync();
        // Match by composite key (Date + Registration + OffBlock); assumes this combination is effectively unique per user.
        var match = all.FirstOrDefault(f =>
            f.Date == flight.Date &&
            f.Registration == flight.Registration &&
            f.OffBlock == flight.OffBlock);
        if (match is not null)
        {
            all.Remove(match);
            await SaveAllAsync(all);
        }
    }

    public async Task<bool> UpdateRegistrationAsync(Flight flight, string newRegistration)
    {
        var all = await LoadAsync();
        // Match by composite key (Date + Registration + OffBlock) using the flight's current values.
        var match = all.FirstOrDefault(f =>
            f.Date == flight.Date &&
            f.Registration == flight.Registration &&
            f.OffBlock == flight.OffBlock);
        if (match is null) return false;
        match.Registration = newRegistration;
        await SaveAllAsync(all);
        return true;
    }

    public async Task<int> MergeAsync(IEnumerable<Flight> incoming)
    {
        var all = await LoadAsync();
        int added = 0;
        foreach (var flight in incoming)
        {
            bool exists = all.Any(f =>
                f.Date == flight.Date &&
                f.Registration == flight.Registration &&
                f.OffBlock == flight.OffBlock);
            if (!exists)
            {
                all.Add(flight);
                added++;
            }
        }
        if (added > 0) await SaveAllAsync(all);
        return added;
    }
}
