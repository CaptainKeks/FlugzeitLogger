using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;
using Xunit;

namespace Uhrzeitrechner.Tests;

public class FlightLogServiceTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"flights-test-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    static Flight Sample(string reg) => new()
    {
        Date = new DateTime(2026, 6, 27),
        Registration = reg,
        OffBlock = new DateTime(2026, 6, 27, 10, 0, 0, DateTimeKind.Utc),
        OnBlock = new DateTime(2026, 6, 27, 11, 0, 0, DateTimeKind.Utc),
        Legs = { new Leg
        {
            Takeoff = new DateTime(2026, 6, 27, 10, 10, 0, DateTimeKind.Utc),
            Landing = new DateTime(2026, 6, 27, 10, 50, 0, DateTimeKind.Utc),
        }},
    };

    [Fact]
    public async Task Load_ReturnsEmpty_WhenFileMissing()
    {
        var svc = new FlightLogService(_path);
        Assert.Empty(await svc.LoadAsync());
    }

    [Fact]
    public async Task Add_ThenLoad_RoundTrips()
    {
        var svc = new FlightLogService(_path);
        await svc.AddAsync(Sample("D-ABCD"));

        var loaded = await svc.LoadAsync();
        Assert.Single(loaded);
        Assert.Equal("D-ABCD", loaded[0].Registration);
        Assert.Single(loaded[0].Legs);
        Assert.Equal(
            new DateTime(2026, 6, 27, 10, 50, 0, DateTimeKind.Utc),
            loaded[0].Legs[0].Landing);
        Assert.Equal(DateTimeKind.Utc, loaded[0].Legs[0].Landing!.Value.Kind);
    }

    [Fact]
    public async Task Delete_RemovesMatchingFlight()
    {
        var svc = new FlightLogService(_path);
        await svc.AddAsync(Sample("D-ABCD"));
        await svc.AddAsync(Sample("D-XYZ"));

        var toDelete = (await svc.LoadAsync())[0];
        await svc.DeleteAsync(toDelete);

        var loaded = await svc.LoadAsync();
        Assert.Single(loaded);
        Assert.Equal("D-XYZ", loaded[0].Registration);
    }

    [Fact]
    public void FilePath_ReturnsConfiguredPath()
    {
        var svc = new FlightLogService(_path);
        Assert.Equal(_path, svc.FilePath);
    }

    [Fact]
    public async Task Merge_AddsOnlyNewFlights()
    {
        var svc = new FlightLogService(_path);
        await svc.AddAsync(Sample("D-ABCD"));

        int added = await svc.MergeAsync(new[] { Sample("D-ABCD"), Sample("D-NEW") });

        Assert.Equal(1, added);
        var loaded = await svc.LoadAsync();
        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, f => f.Registration == "D-NEW");
    }

    [Fact]
    public async Task Merge_IntoEmpty_AddsAll()
    {
        var svc = new FlightLogService(_path);
        int added = await svc.MergeAsync(new[] { Sample("D-ABCD"), Sample("D-XYZ") });
        Assert.Equal(2, added);
    }

    [Fact]
    public async Task Load_ReturnsEmpty_WhenFileCorrupt()
    {
        await File.WriteAllTextAsync(_path, "{ this is not valid json");
        var svc = new FlightLogService(_path);
        Assert.Empty(await svc.LoadAsync());
    }
}
