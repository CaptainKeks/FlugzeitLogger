using System.Text.Json;
using Uhrzeitrechner.Models;

namespace Uhrzeitrechner.Services;

public class FlightSessionStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public FlightSessionStore(string filePath) => _filePath = filePath;

    public async Task SaveAsync(Flight flight)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, flight, Options);
    }

    public async Task<Flight?> LoadAsync()
    {
        if (!File.Exists(_filePath)) return null;
        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<Flight>(stream, Options);
        }
        catch (JsonException)
        {
            return null; // beschädigte Datei -> wie keine Session behandeln
        }
    }

    public Task ClearAsync()
    {
        if (File.Exists(_filePath)) File.Delete(_filePath);
        return Task.CompletedTask;
    }
}
