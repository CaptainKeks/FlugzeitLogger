using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;
using Xunit;

namespace Uhrzeitrechner.Tests;

public class FlightSessionStoreTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"session-test-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    static Flight Sample() => new()
    {
        Date = new DateTime(2026, 6, 27),
        Registration = "D-ABCD",
        OffBlock = new DateTime(2026, 6, 27, 10, 0, 0, DateTimeKind.Utc),
        Legs = { new Leg { Takeoff = new DateTime(2026, 6, 27, 10, 10, 0, DateTimeKind.Utc) } },
    };

    [Fact]
    public async Task Load_ReturnsNull_WhenFileMissing()
    {
        var store = new FlightSessionStore(_path);
        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public async Task Save_ThenLoad_RoundTrips()
    {
        var store = new FlightSessionStore(_path);
        await store.SaveAsync(Sample());

        var loaded = await store.LoadAsync();
        Assert.NotNull(loaded);
        Assert.Equal("D-ABCD", loaded!.Registration);
        Assert.Single(loaded.Legs);
        Assert.Equal(new DateTime(2026, 6, 27, 10, 0, 0, DateTimeKind.Utc), loaded.OffBlock);
    }

    [Fact]
    public async Task Clear_RemovesFile()
    {
        var store = new FlightSessionStore(_path);
        await store.SaveAsync(Sample());
        Assert.True(File.Exists(_path));

        await store.ClearAsync();
        Assert.False(File.Exists(_path));
        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public async Task Load_ReturnsNull_WhenFileCorrupt()
    {
        await File.WriteAllTextAsync(_path, "{ this is not valid json");
        var store = new FlightSessionStore(_path);
        Assert.Null(await store.LoadAsync());
    }
}
