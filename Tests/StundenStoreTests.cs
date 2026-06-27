using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;
using Xunit;

namespace Uhrzeitrechner.Tests;

public class StundenStoreTests
{
    [Fact]
    public void Add_AccumulatesTotal()
    {
        var store = new StundenStore();
        store.Add(new TimeSpan(1, 0, 0));
        store.Add(new TimeSpan(0, 30, 0));

        Assert.Equal(2, store.Entries.Count);
        Assert.Equal(new TimeSpan(1, 30, 0), store.Total);
    }

    [Fact]
    public void Remove_UpdatesEntriesAndTotal()
    {
        var store = new StundenStore();
        store.Add(new TimeSpan(1, 0, 0));
        store.Add(new TimeSpan(0, 30, 0));

        store.Remove(store.Entries[0]);

        Assert.Single(store.Entries);
        Assert.Equal(new TimeSpan(0, 30, 0), store.Total);
    }

    [Fact]
    public void Clear_EmptiesEntries()
    {
        var store = new StundenStore();
        store.Add(new TimeSpan(2, 0, 0));

        store.Clear();

        Assert.Empty(store.Entries);
        Assert.Equal(TimeSpan.Zero, store.Total);
    }

    [Fact]
    public void FormatTotal_PadsMinutes()
        => Assert.Equal("1:05", StundenStore.FormatTotal(new TimeSpan(1, 5, 0)));

    [Fact]
    public void FormatTotal_HandlesOver24Hours()
        => Assert.Equal("25:15", StundenStore.FormatTotal(new TimeSpan(25, 15, 0)));

    [Fact]
    public void FormatTotal_Zero()
        => Assert.Equal("0:00", StundenStore.FormatTotal(TimeSpan.Zero));
}
