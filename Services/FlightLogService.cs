using System.Text.Json;
using Uhrzeitrechner.Models;

namespace Uhrzeitrechner.Services;

public class FlightLogService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public FlightLogService(string filePath) => _filePath = filePath;

    public async Task<List<Flight>> LoadAsync()
    {
        if (!File.Exists(_filePath)) return new();
        await using var stream = File.OpenRead(_filePath);
        var flights = await JsonSerializer.DeserializeAsync<List<Flight>>(stream, Options);
        return flights ?? new();
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
}
