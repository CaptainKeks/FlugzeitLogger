# Flugzeiten-Tracker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die MAUI-App um einen Flugzeiten-Tracker mit Off-/On-Block- und Start-/Landungs-Erfassung, Block-/Flugzeit-Berechnung und Logbuch erweitern.

**Architecture:** Code-Behind-Stil wie die bestehende `MainPage` (kein MVVM). Die reine Logik (Modelle, Berechnung, Flug-State-Machine, Persistenz) liegt in MAUI-unabhängigen Klassen unter `Models/` und `Services/` und wird per separatem Test-Projekt unit-getestet. Die UI-Seiten (`FlightPage`, `LogbookPage`, `FlightDetailPage`) verdrahten nur Events mit diesen Klassen. Navigation über eine Shell-`TabBar`.

**Tech Stack:** .NET 10 MAUI, C#, `System.Text.Json` für Persistenz, xUnit für Tests.

## Global Constraints

- Zielframework App (Build/Run unter Windows): `net10.0-windows10.0.19041.0`.
- `Nullable` und `ImplicitUsings` sind aktiviert (siehe `.csproj`) — Code muss nullable-sauber sein.
- Alle erfassten Flugzeiten sind **UTC** (`DateTime` mit `DateTimeKind.Utc`).
- **Datum** wird automatisch beim Off-Block gesetzt (kein Eingabefeld). **Kennzeichen** wird manuell eingegeben.
- Zeiterfassung per Button ist ein nicht-editierbarer Snapshot der aktuellen UTC-Zeit; Korrektur nur per „Rückgängig".
- Max. **12** Start/Landung-Paare (Legs) pro Flug.
- Zeitformat für Dauer wie im Stundenrechner: `H:MM`, auch > 24 h korrekt (`(int)TotalHours`).
- Stil schlicht wie `MainPage`: `Padding="20"`, Akzent `#512BD4` / DodgerBlue, `BoxView`-Trenner (1 px, LightGray).
- Reine Logik-Klassen (`Models/`, `Services/`) dürfen **keine** MAUI-Typen (z. B. `FileSystem`) referenzieren, damit sie testbar bleiben.

---

## File Structure

- `Models/Flight.cs` — Datenmodell eines Fluges (neu)
- `Models/Leg.cs` — Start/Landung-Paar (neu)
- `Services/FlightMath.cs` — statische Berechnung + Formatierung (neu)
- `Services/FlightSession.cs` — State-Machine des laufenden Fluges (neu)
- `Services/FlightLogService.cs` — JSON-Persistenz, Dateipfad injizierbar (neu)
- `FlightPage.xaml` / `.cs` — Tracker-Seite (neu)
- `LogbookPage.xaml` / `.cs` — Logbuch-Liste (neu)
- `FlightDetailPage.xaml` / `.cs` — Detailansicht (neu)
- `AppShell.xaml` — Umbau auf `TabBar` (ändern)
- `Tests/Uhrzeitrechner.Tests.csproj` — Test-Projekt, link-kompiliert die Logik-Dateien (neu)
- `Tests/FlightMathTests.cs`, `Tests/FlightSessionTests.cs`, `Tests/FlightLogServiceTests.cs` (neu)

---

## Task 1: Test-Projekt, Modelle und Berechnung

**Files:**
- Create: `Models/Flight.cs`, `Models/Leg.cs`
- Create: `Services/FlightMath.cs`
- Create: `Tests/Uhrzeitrechner.Tests.csproj`
- Test: `Tests/FlightMathTests.cs`

**Interfaces:**
- Produces:
  - `Uhrzeitrechner.Models.Leg { DateTime? Takeoff; DateTime? Landing; }`
  - `Uhrzeitrechner.Models.Flight { DateTime Date; string Registration; DateTime? OffBlock; DateTime? OnBlock; List<Leg> Legs; }`
  - `static class FlightMath`:
    - `TimeSpan? BlockTime(Flight f)` → `OnBlock - OffBlock`, sonst `null`
    - `TimeSpan FlightTime(Flight f)` → Summe `Landing - Takeoff` über abgeschlossene Legs
    - `string FormatDuration(TimeSpan? t)` → `"H:MM"` oder `"—"` bei `null`

- [ ] **Step 1: Modelle anlegen**

`Models/Leg.cs`:
```csharp
namespace Uhrzeitrechner.Models;

public class Leg
{
    public DateTime? Takeoff { get; set; }
    public DateTime? Landing { get; set; }
}
```

`Models/Flight.cs`:
```csharp
namespace Uhrzeitrechner.Models;

public class Flight
{
    public DateTime Date { get; set; }
    public string Registration { get; set; } = string.Empty;
    public DateTime? OffBlock { get; set; }
    public DateTime? OnBlock { get; set; }
    public List<Leg> Legs { get; set; } = new();
}
```

- [ ] **Step 2: Test-Projekt anlegen**

`Tests/Uhrzeitrechner.Tests.csproj` (link-kompiliert die reinen Logik-Dateien, referenziert nicht das MAUI-Projekt):
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <Compile Include="..\Models\Flight.cs" Link="Models\Flight.cs" />
    <Compile Include="..\Models\Leg.cs" Link="Models\Leg.cs" />
    <Compile Include="..\Services\FlightMath.cs" Link="Services\FlightMath.cs" />
    <Compile Include="..\Services\FlightSession.cs" Link="Services\FlightSession.cs" />
    <Compile Include="..\Services\FlightLogService.cs" Link="Services\FlightLogService.cs" />
  </ItemGroup>

</Project>
```
Hinweis: `FlightSession.cs` und `FlightLogService.cs` werden in Task 2/3 erstellt. Damit das Test-Projekt in Task 1 baut, lege diese beiden Dateien jetzt als leere Platzhalter mit nur dem `namespace`/Klassenrumpf an ODER nimm die beiden `<Compile>`-Zeilen erst in Task 2/3 hinzu. Empfohlen: die beiden Zeilen jetzt **auskommentieren** und in Task 2/3 aktivieren.

- [ ] **Step 3: Failing test schreiben**

`Tests/FlightMathTests.cs`:
```csharp
using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;
using Xunit;

namespace Uhrzeitrechner.Tests;

public class FlightMathTests
{
    static DateTime Utc(int h, int m) => new(2026, 6, 27, h, m, 0, DateTimeKind.Utc);

    [Fact]
    public void BlockTime_IsOnBlockMinusOffBlock()
    {
        var f = new Flight { OffBlock = Utc(10, 0), OnBlock = Utc(12, 30) };
        Assert.Equal(TimeSpan.FromMinutes(150), FlightMath.BlockTime(f));
    }

    [Fact]
    public void BlockTime_NullWhenOnBlockMissing()
    {
        var f = new Flight { OffBlock = Utc(10, 0) };
        Assert.Null(FlightMath.BlockTime(f));
    }

    [Fact]
    public void FlightTime_SumsCompletedLegsOnly()
    {
        var f = new Flight();
        f.Legs.Add(new Leg { Takeoff = Utc(10, 10), Landing = Utc(10, 40) }); // 30
        f.Legs.Add(new Leg { Takeoff = Utc(11, 0), Landing = Utc(11, 20) });  // 20
        f.Legs.Add(new Leg { Takeoff = Utc(12, 0) });                          // offen -> 0
        Assert.Equal(TimeSpan.FromMinutes(50), FlightMath.FlightTime(f));
    }

    [Fact]
    public void FormatDuration_FormatsHoursAndMinutes()
    {
        Assert.Equal("2:05", FlightMath.FormatDuration(TimeSpan.FromMinutes(125)));
        Assert.Equal("26:00", FlightMath.FormatDuration(TimeSpan.FromHours(26)));
        Assert.Equal("—", FlightMath.FormatDuration(null));
    }
}
```

- [ ] **Step 4: Test ausführen, Fehlschlag bestätigen**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj`
Expected: FAIL (FlightMath existiert noch nicht / Kompilierfehler).

- [ ] **Step 5: Implementierung schreiben**

`Services/FlightMath.cs`:
```csharp
using Uhrzeitrechner.Models;

namespace Uhrzeitrechner.Services;

public static class FlightMath
{
    public static TimeSpan? BlockTime(Flight f)
        => f.OffBlock is { } off && f.OnBlock is { } on ? on - off : null;

    public static TimeSpan FlightTime(Flight f)
    {
        var total = TimeSpan.Zero;
        foreach (var leg in f.Legs)
            if (leg.Takeoff is { } to && leg.Landing is { } la)
                total += la - to;
        return total;
    }

    public static string FormatDuration(TimeSpan? t)
    {
        if (t is not { } v) return "—";
        return $"{(int)v.TotalHours}:{v.Minutes:D2}";
    }
}
```
(In Task 1 die `<Compile>`-Zeilen für `FlightSession.cs`/`FlightLogService.cs` im csproj auskommentiert lassen.)

- [ ] **Step 6: Test ausführen, Erfolg bestätigen**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj`
Expected: PASS (4 Tests grün).

- [ ] **Step 7: Commit** (nur falls Git vorhanden — dieses Projekt ist KEIN Git-Repo; dann Schritt überspringen)

```bash
git add Models Services/FlightMath.cs Tests
git commit -m "feat: Flight-Modelle und FlightMath mit Tests"
```

---

## Task 2: FlightSession (State-Machine des laufenden Fluges)

**Files:**
- Create: `Services/FlightSession.cs`
- Modify: `Tests/Uhrzeitrechner.Tests.csproj` (Compile-Zeile für `FlightSession.cs` aktivieren)
- Test: `Tests/FlightSessionTests.cs`

**Interfaces:**
- Consumes: `Flight`, `Leg`, `FlightMath`
- Produces: `class FlightSession`
  - Konstruktor: `FlightSession(Func<DateTime>? nowUtc = null)` (Default `() => DateTime.UtcNow`)
  - `const int MaxLegs = 12;`
  - `Flight Flight { get; }` (aktueller Flug)
  - `string Registration { get; set; }` (schreibt in `Flight.Registration`)
  - Zustands-Flags (für Button-Enable): `bool CanOffBlock`, `bool CanStart`, `bool CanLanding`, `bool CanOnBlock`, `bool CanUndo`, `bool CanSave`
  - Aktionen: `void OffBlock()`, `void Start()`, `void Landing()`, `void OnBlock()`, `void Undo()`, `void Reset()`
  - `IReadOnlyList<Leg> Legs => Flight.Legs`

State-Regeln:
- `CanOffBlock`: `OffBlock == null`.
- `CanStart`: `OffBlock != null` && kein offenes Leg (alle Legs haben Landing) && `OnBlock == null` && `Legs.Count < MaxLegs`.
- `CanLanding`: es gibt ein offenes Leg (letztes Leg hat Takeoff, kein Landing).
- `CanOnBlock`: `OffBlock != null` && `OnBlock == null` && mind. ein abgeschlossenes Leg && kein offenes Leg.
- `CanUndo`: irgendein Zeitpunkt erfasst (`OffBlock != null` || `Legs.Count > 0`).
- `CanSave`: `OffBlock != null` && `OnBlock != null` && mind. ein abgeschlossenes Leg && `Registration` nicht leer.
- `Undo` entfernt in dieser Reihenfolge: `OnBlock` → letztes `Landing` → letztes `Leg` (Takeoff) → `OffBlock`.

- [ ] **Step 1: Compile-Zeile aktivieren**

In `Tests/Uhrzeitrechner.Tests.csproj` die Zeile
`<Compile Include="..\Services\FlightSession.cs" Link="Services\FlightSession.cs" />` einkommentieren.

- [ ] **Step 2: Failing tests schreiben**

`Tests/FlightSessionTests.cs`:
```csharp
using Uhrzeitrechner.Services;
using Xunit;

namespace Uhrzeitrechner.Tests;

public class FlightSessionTests
{
    static FlightSession NewSession(Queue<DateTime> times)
        => new(() => times.Dequeue());

    static DateTime Utc(int h, int m) => new(2026, 6, 27, h, m, 0, DateTimeKind.Utc);

    [Fact]
    public void InitialState_OnlyOffBlockAllowed()
    {
        var s = new FlightSession();
        Assert.True(s.CanOffBlock);
        Assert.False(s.CanStart);
        Assert.False(s.CanLanding);
        Assert.False(s.CanOnBlock);
        Assert.False(s.CanSave);
    }

    [Fact]
    public void FullFlow_TwoLegs_ComputesAndAllowsSave()
    {
        var times = new Queue<DateTime>(new[]
        {
            Utc(10, 0),  // OffBlock
            Utc(10, 10), // Start 1
            Utc(10, 40), // Landing 1
            Utc(11, 0),  // Start 2
            Utc(11, 20), // Landing 2
            Utc(11, 30), // OnBlock
        });
        var s = NewSession(times);
        s.Registration = "D-ABCD";

        s.OffBlock();
        Assert.False(s.CanOffBlock);
        Assert.True(s.CanStart);

        s.Start();
        Assert.True(s.CanLanding);
        Assert.False(s.CanStart);   // offenes Leg -> kein neuer Start
        Assert.False(s.CanOnBlock);

        s.Landing();
        Assert.True(s.CanStart);    // nächstes Paar möglich
        Assert.True(s.CanOnBlock);

        s.Start();
        s.Landing();
        s.OnBlock();

        Assert.Equal(2, s.Legs.Count);
        Assert.True(s.CanSave);
        Assert.Equal(TimeSpan.FromMinutes(90), FlightMath.BlockTime(s.Flight));
        Assert.Equal(TimeSpan.FromMinutes(50), FlightMath.FlightTime(s.Flight));
        Assert.Equal(new DateTime(2026, 6, 27), s.Flight.Date.Date);
    }

    [Fact]
    public void CannotSave_WithoutRegistration()
    {
        var times = new Queue<DateTime>(new[]
        { Utc(10,0), Utc(10,10), Utc(10,40), Utc(11,0) });
        var s = NewSession(times);
        s.OffBlock(); s.Start(); s.Landing(); s.OnBlock();
        Assert.False(s.CanSave); // kein Kennzeichen
        s.Registration = "D-XYZ";
        Assert.True(s.CanSave);
    }

    [Fact]
    public void Undo_RemovesLastCaptureInOrder()
    {
        var times = new Queue<DateTime>(new[]
        { Utc(10,0), Utc(10,10), Utc(10,40), Utc(11,0) });
        var s = NewSession(times);
        s.OffBlock(); s.Start(); s.Landing(); s.OnBlock();

        s.Undo(); // OnBlock weg
        Assert.Null(s.Flight.OnBlock);
        Assert.Single(s.Legs);

        s.Undo(); // Landing weg
        Assert.Null(s.Legs[0].Landing);
        Assert.True(s.CanLanding);

        s.Undo(); // Leg (Takeoff) weg
        Assert.Empty(s.Legs);

        s.Undo(); // OffBlock weg
        Assert.Null(s.Flight.OffBlock);
        Assert.True(s.CanOffBlock);
        Assert.False(s.CanUndo);
    }

    [Fact]
    public void MaxLegs_BlocksFurtherStarts()
    {
        var times = new Queue<DateTime>();
        var t = Utc(8, 0);
        times.Enqueue(t); // OffBlock
        for (int i = 0; i < FlightSession.MaxLegs; i++)
        {
            times.Enqueue(t.AddMinutes(1)); // Start
            times.Enqueue(t.AddMinutes(2)); // Landing
            t = t.AddMinutes(10);
        }
        var s = new FlightSession(() => times.Dequeue());
        s.OffBlock();
        for (int i = 0; i < FlightSession.MaxLegs; i++) { s.Start(); s.Landing(); }
        Assert.Equal(FlightSession.MaxLegs, s.Legs.Count);
        Assert.False(s.CanStart); // Maximum erreicht
        Assert.True(s.CanOnBlock);
    }
}
```

- [ ] **Step 3: Test ausführen, Fehlschlag bestätigen**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj`
Expected: FAIL (FlightSession existiert nicht).

- [ ] **Step 4: Implementierung schreiben**

`Services/FlightSession.cs`:
```csharp
using Uhrzeitrechner.Models;

namespace Uhrzeitrechner.Services;

public class FlightSession
{
    public const int MaxLegs = 12;

    private readonly Func<DateTime> _nowUtc;

    public FlightSession(Func<DateTime>? nowUtc = null)
        => _nowUtc = nowUtc ?? (() => DateTime.UtcNow);

    public Flight Flight { get; private set; } = new();

    public IReadOnlyList<Leg> Legs => Flight.Legs;

    public string Registration
    {
        get => Flight.Registration;
        set => Flight.Registration = value ?? string.Empty;
    }

    private Leg? OpenLeg
        => Flight.Legs.Count > 0 && Flight.Legs[^1].Landing is null
            ? Flight.Legs[^1] : null;

    private bool HasCompletedLeg
        => Flight.Legs.Any(l => l.Takeoff is not null && l.Landing is not null);

    public bool CanOffBlock => Flight.OffBlock is null;
    public bool CanStart => Flight.OffBlock is not null && Flight.OnBlock is null
                            && OpenLeg is null && Flight.Legs.Count < MaxLegs;
    public bool CanLanding => OpenLeg is not null;
    public bool CanOnBlock => Flight.OffBlock is not null && Flight.OnBlock is null
                              && OpenLeg is null && HasCompletedLeg;
    public bool CanUndo => Flight.OffBlock is not null || Flight.Legs.Count > 0;
    public bool CanSave => Flight.OffBlock is not null && Flight.OnBlock is not null
                           && HasCompletedLeg
                           && !string.IsNullOrWhiteSpace(Flight.Registration);

    public void OffBlock()
    {
        if (!CanOffBlock) return;
        var now = _nowUtc();
        Flight.OffBlock = now;
        Flight.Date = now.Date;
    }

    public void Start()
    {
        if (!CanStart) return;
        Flight.Legs.Add(new Leg { Takeoff = _nowUtc() });
    }

    public void Landing()
    {
        if (OpenLeg is not { } leg) return;
        leg.Landing = _nowUtc();
    }

    public void OnBlock()
    {
        if (!CanOnBlock) return;
        Flight.OnBlock = _nowUtc();
    }

    public void Undo()
    {
        if (Flight.OnBlock is not null) { Flight.OnBlock = null; return; }
        if (OpenLeg is { } open) { Flight.Legs.RemoveAt(Flight.Legs.Count - 1); return; }
        if (Flight.Legs.Count > 0)
        {
            Flight.Legs[^1].Landing = null; // letztes abgeschlossenes Leg -> Landing weg
            return;
        }
        if (Flight.OffBlock is not null) { Flight.OffBlock = null; }
    }

    public void Reset() => Flight = new();
}
```
Hinweis zur `Undo`-Reihenfolge: Bei genau einem offenen Leg (Takeoff ohne Landing) wird das ganze Leg entfernt; bei abgeschlossenen Legs wird zuerst das letzte Landing entfernt (Leg wird wieder „offen"), ein weiteres Undo entfernt dann den Takeoff. Das deckt der Test `Undo_RemovesLastCaptureInOrder` ab.

- [ ] **Step 5: Test ausführen, Erfolg bestätigen**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj`
Expected: PASS (alle FlightSession- und FlightMath-Tests grün).

- [ ] **Step 6: Commit** (nur falls Git vorhanden — sonst überspringen)

```bash
git add Services/FlightSession.cs Tests
git commit -m "feat: FlightSession State-Machine mit Tests"
```

---

## Task 3: FlightLogService (JSON-Persistenz)

**Files:**
- Create: `Services/FlightLogService.cs`
- Modify: `Tests/Uhrzeitrechner.Tests.csproj` (Compile-Zeile für `FlightLogService.cs` aktivieren)
- Test: `Tests/FlightLogServiceTests.cs`

**Interfaces:**
- Consumes: `Flight`
- Produces: `class FlightLogService`
  - Konstruktor: `FlightLogService(string filePath)`
  - `Task<List<Flight>> LoadAsync()` (leere Liste, wenn Datei fehlt)
  - `Task AddAsync(Flight flight)` (lädt, hängt an, speichert)
  - `Task DeleteAsync(Flight flight)` (entfernt per Referenz-Gleichheit aus geladener Liste — siehe Hinweis)
  - `Task SaveAllAsync(List<Flight> flights)`

Hinweis: Da JSON keine Referenzidentität kennt, arbeitet die UI mit der von `LoadAsync` gelieferten Liste und ruft nach Änderungen `SaveAllAsync` auf. `DeleteAsync` ist eine Komfortmethode: lädt die Liste, entfernt das erste Element mit gleichem `Date`+`Registration`+`OffBlock`, speichert.

- [ ] **Step 1: Compile-Zeile aktivieren**

In `Tests/Uhrzeitrechner.Tests.csproj` die Zeile
`<Compile Include="..\Services\FlightLogService.cs" Link="Services\FlightLogService.cs" />` einkommentieren.

- [ ] **Step 2: Failing tests schreiben**

`Tests/FlightLogServiceTests.cs`:
```csharp
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
}
```

- [ ] **Step 3: Test ausführen, Fehlschlag bestätigen**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj`
Expected: FAIL (FlightLogService existiert nicht).

- [ ] **Step 4: Implementierung schreiben**

`Services/FlightLogService.cs`:
```csharp
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
```

- [ ] **Step 5: Test ausführen, Erfolg bestätigen**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj`
Expected: PASS (alle Tests grün).

- [ ] **Step 6: Commit** (nur falls Git vorhanden — sonst überspringen)

```bash
git add Services/FlightLogService.cs Tests
git commit -m "feat: FlightLogService JSON-Persistenz mit Tests"
```

---

## Task 4: AppShell auf TabBar umstellen

**Files:**
- Modify: `AppShell.xaml`

**Interfaces:**
- Consumes: `MainPage` (bestehend), `FlightPage`, `LogbookPage` (in späteren Tasks erstellt)
- Produces: TabBar-Navigation mit Routen `MainPage`, `FlightPage`, `LogbookPage`; Route `FlightDetailPage` registriert für Detailnavigation.

Hinweis: Diese Task referenziert `FlightPage`/`LogbookPage`/`FlightDetailPage`, die in Task 5–7 erstellt werden. Daher baut die App erst nach Task 7 wieder vollständig. Wird der Plan task-weise mit Build-Gate ausgeführt, **AppShell-Umbau (Task 4) zusammen mit Task 5–7 als ein Build-Gate** behandeln — alternativ Task 4 ans Ende verschieben. Empfohlen: Task 4 nach Task 7 ausführen (Reihenfolge im Self-Review berücksichtigt).

- [ ] **Step 1: AppShell.xaml umschreiben**

`AppShell.xaml`:
```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Shell
    x:Class="Uhrzeitrechner.AppShell"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:local="clr-namespace:Uhrzeitrechner"
    Title="Uhrzeitrechner">

    <TabBar>
        <ShellContent
            Title="Stunden"
            ContentTemplate="{DataTemplate local:MainPage}"
            Route="MainPage" />
        <ShellContent
            Title="Flug"
            ContentTemplate="{DataTemplate local:FlightPage}"
            Route="FlightPage" />
        <ShellContent
            Title="Logbuch"
            ContentTemplate="{DataTemplate local:LogbookPage}"
            Route="LogbookPage" />
    </TabBar>

</Shell>
```

- [ ] **Step 2: Detail-Route registrieren**

In `AppShell.xaml.cs` im Konstruktor nach `InitializeComponent();` ergänzen:
```csharp
Routing.RegisterRoute(nameof(FlightDetailPage), typeof(FlightDetailPage));
```

- [ ] **Step 3: Build** (erst nach Task 5–7 erfolgreich)

Run: `dotnet build -f net10.0-windows10.0.19041.0`
Expected: Build succeeded.

- [ ] **Step 4: Commit** (nur falls Git vorhanden — sonst überspringen)

```bash
git add AppShell.xaml AppShell.xaml.cs
git commit -m "feat: TabBar-Navigation mit Stunden/Flug/Logbuch"
```

---

## Task 5: FlightPage (Tracker-UI)

**Files:**
- Create: `FlightPage.xaml`, `FlightPage.xaml.cs`

**Interfaces:**
- Consumes: `FlightSession`, `FlightMath`, `FlightLogService`, `Models.Flight`, `Models.Leg`
- Produces: `FlightPage : ContentPage` mit Route `FlightPage`.

UI-Aufbau (schlicht wie `MainPage`, `Padding="20"`, RowSpacing 15):
- Kopf: live UTC groß (`FontSize=28, Bold`), Lokalzeit klein darunter (Gray); 1-Sekunden-`IDispatcherTimer` wie in `MainPage`.
- Datum-Anzeige (read-only Label, gesetzt nach Off-Block) + `Entry` Kennzeichen (Placeholder „Kennzeichen", `TextChanged` → `_session.Registration` + `RefreshState`).
- 2×2 Button-Grid: Off-Block, Start, Landung, On-Block. `IsEnabled` an `Can*`-Flags gebunden (in `RefreshState` gesetzt).
- `CollectionView` der Legs (`ItemsSource` = `ObservableCollection<LegRow>`), Zeile: „Start n: HH:mm:ss / Landung n: HH:mm:ss" (offene Landung „—"). Zeit-Anzeige in UTC (`ToString("HH:mm:ss")`).
- „Rückgängig"-Button (`IsEnabled = CanUndo`).
- Summen unten: „Blockzeit" + `FormatDuration(BlockTime)`, „Flugzeit" + `FormatDuration(FlightTime)` (DodgerBlue, Bold).
- Aktionen unten: „Flug speichern" (`IsEnabled = CanSave`), „Zurücksetzen".

- [ ] **Step 1: FlightPage.xaml anlegen**

`FlightPage.xaml`:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Uhrzeitrechner.FlightPage"
             Title="Flug">

    <ScrollView>
        <VerticalStackLayout Padding="20" Spacing="15">

            <!-- Uhr -->
            <VerticalStackLayout>
                <Label Text="UTC" FontSize="12" TextColor="Gray" />
                <Label x:Name="UtcTimeLabel" Text="--:--:--" FontSize="28" FontAttributes="Bold" />
                <Label x:Name="LocalTimeLabel" Text="Lokal --:--:--" FontSize="12" TextColor="Gray" />
            </VerticalStackLayout>

            <BoxView HeightRequest="1" Color="LightGray" />

            <!-- Flugdaten -->
            <Grid ColumnDefinitions="Auto,*" ColumnSpacing="10" RowDefinitions="Auto,Auto" RowSpacing="8">
                <Label Grid.Row="0" Grid.Column="0" Text="Datum:" VerticalOptions="Center" TextColor="Gray" />
                <Label Grid.Row="0" Grid.Column="1" x:Name="DateLabel" Text="—" VerticalOptions="Center" FontAttributes="Bold" />
                <Label Grid.Row="1" Grid.Column="0" Text="Kennzeichen:" VerticalOptions="Center" TextColor="Gray" />
                <Entry Grid.Row="1" Grid.Column="1" x:Name="RegistrationEntry" Placeholder="z.B. D-ABCD" TextChanged="OnRegistrationChanged" />
            </Grid>

            <!-- Erfassungs-Buttons -->
            <Grid ColumnDefinitions="*,*" RowDefinitions="Auto,Auto" ColumnSpacing="10" RowSpacing="10">
                <Button Grid.Row="0" Grid.Column="0" x:Name="OffBlockButton" Text="Off-Block" Clicked="OnOffBlockClicked" />
                <Button Grid.Row="0" Grid.Column="1" x:Name="OnBlockButton" Text="On-Block" Clicked="OnOnBlockClicked" />
                <Button Grid.Row="1" Grid.Column="0" x:Name="StartButton" Text="Start" Clicked="OnStartClicked" />
                <Button Grid.Row="1" Grid.Column="1" x:Name="LandingButton" Text="Landung" Clicked="OnLandingClicked" />
            </Grid>

            <Button x:Name="UndoButton" Text="Letzte Erfassung rückgängig" Clicked="OnUndoClicked"
                    BackgroundColor="Transparent" TextColor="{StaticResource Primary}" />

            <BoxView HeightRequest="1" Color="LightGray" />

            <!-- Legs -->
            <CollectionView x:Name="LegsView">
                <CollectionView.ItemTemplate>
                    <DataTemplate>
                        <Grid Padding="0,6">
                            <Label Text="{Binding Display}" FontSize="16" />
                        </Grid>
                    </DataTemplate>
                </CollectionView.ItemTemplate>
            </CollectionView>

            <BoxView HeightRequest="1" Color="LightGray" />

            <!-- Summen -->
            <Grid ColumnDefinitions="*,Auto" RowDefinitions="Auto,Auto" RowSpacing="6">
                <Label Grid.Row="0" Grid.Column="0" Text="Blockzeit:" FontSize="18" FontAttributes="Bold" />
                <Label Grid.Row="0" Grid.Column="1" x:Name="BlockTimeLabel" Text="—" FontSize="18" FontAttributes="Bold" TextColor="DodgerBlue" />
                <Label Grid.Row="1" Grid.Column="0" Text="Flugzeit:" FontSize="18" FontAttributes="Bold" />
                <Label Grid.Row="1" Grid.Column="1" x:Name="FlightTimeLabel" Text="0:00" FontSize="18" FontAttributes="Bold" TextColor="DodgerBlue" />
            </Grid>

            <Button x:Name="SaveButton" Text="Flug speichern" Clicked="OnSaveClicked" />
            <Button x:Name="ResetButton" Text="Zurücksetzen" Clicked="OnResetClicked"
                    BackgroundColor="Transparent" TextColor="Red" />

        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

- [ ] **Step 2: FlightPage.xaml.cs anlegen**

`FlightPage.xaml.cs`:
```csharp
using System.Collections.ObjectModel;
using Uhrzeitrechner.Services;

namespace Uhrzeitrechner;

public partial class FlightPage : ContentPage
{
    private readonly FlightSession _session = new();
    private readonly FlightLogService _log =
        new(Path.Combine(FileSystem.AppDataDirectory, "flights.json"));
    private readonly ObservableCollection<LegRow> _legRows = new();
    private IDispatcherTimer? _clockTimer;

    public FlightPage()
    {
        InitializeComponent();
        LegsView.ItemsSource = _legRows;
        RefreshState();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateClock();
        _clockTimer = Dispatcher.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (s, e) => UpdateClock();
        _clockTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _clockTimer?.Stop();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        UtcTimeLabel.Text = now.ToUniversalTime().ToString("HH:mm:ss");
        LocalTimeLabel.Text = "Lokal " + now.ToString("HH:mm:ss");
    }

    private void OnRegistrationChanged(object? sender, TextChangedEventArgs e)
    {
        _session.Registration = e.NewTextValue ?? string.Empty;
        RefreshState();
    }

    private void OnOffBlockClicked(object? sender, EventArgs e) { _session.OffBlock(); RefreshState(); }
    private void OnStartClicked(object? sender, EventArgs e) { _session.Start(); RefreshState(); }
    private void OnLandingClicked(object? sender, EventArgs e) { _session.Landing(); RefreshState(); }
    private void OnOnBlockClicked(object? sender, EventArgs e) { _session.OnBlock(); RefreshState(); }
    private void OnUndoClicked(object? sender, EventArgs e) { _session.Undo(); RefreshState(); }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!_session.CanSave) return;
        await _log.AddAsync(_session.Flight);
        _session.Reset();
        RegistrationEntry.Text = string.Empty;
        RefreshState();
        await DisplayAlert("Gespeichert", "Flug wurde im Logbuch gespeichert.", "OK");
    }

    private async void OnResetClicked(object? sender, EventArgs e)
    {
        bool ok = await DisplayAlert("Zurücksetzen",
            "Aktuellen Flug verwerfen?", "Ja", "Abbrechen");
        if (!ok) return;
        _session.Reset();
        RegistrationEntry.Text = string.Empty;
        RefreshState();
    }

    private void RefreshState()
    {
        OffBlockButton.IsEnabled = _session.CanOffBlock;
        StartButton.IsEnabled = _session.CanStart;
        LandingButton.IsEnabled = _session.CanLanding;
        OnBlockButton.IsEnabled = _session.CanOnBlock;
        UndoButton.IsEnabled = _session.CanUndo;
        SaveButton.IsEnabled = _session.CanSave;

        DateLabel.Text = _session.Flight.OffBlock is null
            ? "—" : _session.Flight.Date.ToString("dd.MM.yyyy");

        _legRows.Clear();
        for (int i = 0; i < _session.Legs.Count; i++)
            _legRows.Add(new LegRow(i + 1, _session.Legs[i]));

        BlockTimeLabel.Text = FlightMath.FormatDuration(FlightMath.BlockTime(_session.Flight));
        FlightTimeLabel.Text = FlightMath.FormatDuration(FlightMath.FlightTime(_session.Flight));
    }

    public class LegRow
    {
        public string Display { get; }
        public LegRow(int index, Models.Leg leg)
        {
            string to = leg.Takeoff?.ToString("HH:mm:ss") ?? "—";
            string la = leg.Landing?.ToString("HH:mm:ss") ?? "—";
            Display = $"Start {index}: {to}   /   Landung {index}: {la}";
        }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build -f net10.0-windows10.0.19041.0`
Expected: Build succeeded (sofern AppShell-Task 4 bereits FlightPage referenziert — siehe Reihenfolge-Hinweis; ggf. baut die App erst nach Task 4+7).

- [ ] **Step 4: Manueller Test**

Run: `dotnet build -t:Run -f net10.0-windows10.0.19041.0`
Prüfen: UTC-Uhr läuft; Off-Block setzt Datum und aktiviert Start; Start→Landung-Paare erscheinen in der Liste; On-Block erst nach einer Landung aktiv; Flugzeit/Blockzeit korrekt; „Rückgängig" entfernt letzte Erfassung; „Flug speichern" erst mit Kennzeichen+vollständigem Flug aktiv.

- [ ] **Step 5: Commit** (nur falls Git vorhanden — sonst überspringen)

```bash
git add FlightPage.xaml FlightPage.xaml.cs
git commit -m "feat: FlightPage Flug-Tracker"
```

---

## Task 6: LogbookPage (Logbuch-Liste)

**Files:**
- Create: `LogbookPage.xaml`, `LogbookPage.xaml.cs`

**Interfaces:**
- Consumes: `FlightLogService`, `FlightMath`, `Models.Flight`
- Produces: `LogbookPage : ContentPage` mit Route `LogbookPage`. Navigiert zu `FlightDetailPage` und übergibt den ausgewählten `Flight` (per `ShellNavigationQueryParameters` mit Schlüssel `"flight"`).

UI: `CollectionView` mit `ObservableCollection<Flight>` (neueste oben). Zeile: Datum · Kennzeichen · „Block H:MM" · „Flug H:MM" + Lösch-Button (✕). Leerzustand: `EmptyView` mit Hinweistext. `OnAppearing` lädt neu.

- [ ] **Step 1: LogbookPage.xaml anlegen**

`LogbookPage.xaml`:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Uhrzeitrechner.LogbookPage"
             Title="Logbuch">

    <Grid Padding="20">
        <CollectionView x:Name="FlightsView" SelectionMode="Single"
                        SelectionChanged="OnFlightSelected">
            <CollectionView.EmptyView>
                <Label Text="Noch keine Flüge gespeichert"
                       HorizontalOptions="Center" VerticalOptions="Center"
                       TextColor="Gray" />
            </CollectionView.EmptyView>
            <CollectionView.ItemTemplate>
                <DataTemplate>
                    <Grid ColumnDefinitions="*,Auto" Padding="0,10" ColumnSpacing="10">
                        <VerticalStackLayout Grid.Column="0">
                            <Label FontAttributes="Bold" FontSize="16">
                                <Label.Text>
                                    <MultiBinding StringFormat="{}{0} · {1}">
                                        <Binding Path="DateText" />
                                        <Binding Path="Registration" />
                                    </MultiBinding>
                                </Label.Text>
                            </Label>
                            <Label Text="{Binding Summary}" FontSize="13" TextColor="Gray" />
                        </VerticalStackLayout>
                        <Button Grid.Column="1" Text="✕" FontSize="14"
                                WidthRequest="40" HeightRequest="40"
                                BackgroundColor="Transparent" TextColor="Red"
                                Clicked="OnDeleteClicked"
                                CommandParameter="{Binding Flight}" />
                    </Grid>
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>
    </Grid>
</ContentPage>
```

- [ ] **Step 2: LogbookPage.xaml.cs anlegen**

`LogbookPage.xaml.cs`:
```csharp
using System.Collections.ObjectModel;
using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;

namespace Uhrzeitrechner;

public partial class LogbookPage : ContentPage
{
    private readonly FlightLogService _log =
        new(Path.Combine(FileSystem.AppDataDirectory, "flights.json"));
    private readonly ObservableCollection<FlightRow> _rows = new();

    public LogbookPage()
    {
        InitializeComponent();
        FlightsView.ItemsSource = _rows;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var flights = await _log.LoadAsync();
        flights.Sort((a, b) => DateTime.Compare(
            b.OffBlock ?? b.Date, a.OffBlock ?? a.Date)); // neueste oben
        _rows.Clear();
        foreach (var f in flights)
            _rows.Add(new FlightRow(f));
    }

    private async void OnFlightSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not FlightRow row) return;
        FlightsView.SelectedItem = null; // Auswahl zurücksetzen
        await Shell.Current.GoToAsync(nameof(FlightDetailPage),
            new Dictionary<string, object> { ["flight"] = row.Flight });
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Flight flight }) return;
        bool ok = await DisplayAlert("Löschen",
            $"Flug {flight.Registration} löschen?", "Ja", "Abbrechen");
        if (!ok) return;
        await _log.DeleteAsync(flight);
        await ReloadAsync();
    }

    public class FlightRow
    {
        public Flight Flight { get; }
        public string DateText => Flight.Date.ToString("dd.MM.yyyy");
        public string Registration => Flight.Registration;
        public string Summary =>
            $"Block {FlightMath.FormatDuration(FlightMath.BlockTime(Flight))} · " +
            $"Flug {FlightMath.FormatDuration(FlightMath.FlightTime(Flight))}";
        public FlightRow(Flight flight) => Flight = flight;
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build -f net10.0-windows10.0.19041.0`
Expected: Build succeeded (nach Task 4 + 7).

- [ ] **Step 4: Commit** (nur falls Git vorhanden — sonst überspringen)

```bash
git add LogbookPage.xaml LogbookPage.xaml.cs
git commit -m "feat: LogbookPage Logbuch-Liste"
```

---

## Task 7: FlightDetailPage (Detailansicht)

**Files:**
- Create: `FlightDetailPage.xaml`, `FlightDetailPage.xaml.cs`

**Interfaces:**
- Consumes: `Models.Flight`, `Models.Leg`, `FlightMath`
- Produces: `FlightDetailPage : ContentPage`, empfängt `Flight` über `IQueryAttributable` (Schlüssel `"flight"`). Route registriert in `AppShell` (Task 4, Step 2).

UI: Nur-Anzeige. Kopf: Datum + Kennzeichen. Liste: Off-Block (UTC), je Leg „Start n / Landung n", On-Block. Unten: Blockzeit, Flugzeit.

- [ ] **Step 1: FlightDetailPage.xaml anlegen**

`FlightDetailPage.xaml`:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Uhrzeitrechner.FlightDetailPage"
             Title="Flugdetails">

    <ScrollView>
        <VerticalStackLayout Padding="20" Spacing="12">
            <Label x:Name="HeaderLabel" FontSize="22" FontAttributes="Bold" />

            <BoxView HeightRequest="1" Color="LightGray" />

            <Grid ColumnDefinitions="Auto,*" ColumnSpacing="10">
                <Label Grid.Column="0" Text="Off-Block:" TextColor="Gray" />
                <Label Grid.Column="1" x:Name="OffBlockLabel" FontAttributes="Bold" />
            </Grid>

            <VerticalStackLayout x:Name="LegsStack" Spacing="6" />

            <Grid ColumnDefinitions="Auto,*" ColumnSpacing="10">
                <Label Grid.Column="0" Text="On-Block:" TextColor="Gray" />
                <Label Grid.Column="1" x:Name="OnBlockLabel" FontAttributes="Bold" />
            </Grid>

            <BoxView HeightRequest="1" Color="LightGray" />

            <Grid ColumnDefinitions="*,Auto" RowDefinitions="Auto,Auto" RowSpacing="6">
                <Label Grid.Row="0" Grid.Column="0" Text="Blockzeit:" FontSize="18" FontAttributes="Bold" />
                <Label Grid.Row="0" Grid.Column="1" x:Name="BlockTimeLabel" FontSize="18" FontAttributes="Bold" TextColor="DodgerBlue" />
                <Label Grid.Row="1" Grid.Column="0" Text="Flugzeit:" FontSize="18" FontAttributes="Bold" />
                <Label Grid.Row="1" Grid.Column="1" x:Name="FlightTimeLabel" FontSize="18" FontAttributes="Bold" TextColor="DodgerBlue" />
            </Grid>
        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

- [ ] **Step 2: FlightDetailPage.xaml.cs anlegen**

`FlightDetailPage.xaml.cs`:
```csharp
using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;

namespace Uhrzeitrechner;

public partial class FlightDetailPage : ContentPage, IQueryAttributable
{
    public FlightDetailPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("flight", out var value) && value is Flight flight)
            Render(flight);
    }

    private void Render(Flight flight)
    {
        HeaderLabel.Text = $"{flight.Date:dd.MM.yyyy} · {flight.Registration}";
        OffBlockLabel.Text = flight.OffBlock is { } off ? off.ToString("HH:mm:ss") + " UTC" : "—";
        OnBlockLabel.Text = flight.OnBlock is { } on ? on.ToString("HH:mm:ss") + " UTC" : "—";

        LegsStack.Children.Clear();
        for (int i = 0; i < flight.Legs.Count; i++)
        {
            var leg = flight.Legs[i];
            string to = leg.Takeoff?.ToString("HH:mm:ss") ?? "—";
            string la = leg.Landing?.ToString("HH:mm:ss") ?? "—";
            LegsStack.Children.Add(new Label
            {
                Text = $"Start {i + 1}: {to}   /   Landung {i + 1}: {la}",
                FontSize = 16
            });
        }

        BlockTimeLabel.Text = FlightMath.FormatDuration(FlightMath.BlockTime(flight));
        FlightTimeLabel.Text = FlightMath.FormatDuration(FlightMath.FlightTime(flight));
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build -f net10.0-windows10.0.19041.0`
Expected: Build succeeded.

- [ ] **Step 4: Manueller End-to-End-Test**

Run: `dotnet build -t:Run -f net10.0-windows10.0.19041.0`
Prüfen: Flug erfassen → speichern → Tab „Logbuch" zeigt den Flug → Tippen öffnet Details mit allen Zeiten → Löschen entfernt ihn.

- [ ] **Step 5: Commit** (nur falls Git vorhanden — sonst überspringen)

```bash
git add FlightDetailPage.xaml FlightDetailPage.xaml.cs
git commit -m "feat: FlightDetailPage Detailansicht"
```

---

## Ausführungsreihenfolge (wichtig)

Wegen der gegenseitigen Referenzen baut die MAUI-App erst, wenn alle Seiten existieren. Empfohlene Reihenfolge:

1. Task 1 (Modelle/FlightMath + Tests) — `dotnet test` grün
2. Task 2 (FlightSession + Tests) — `dotnet test` grün
3. Task 3 (FlightLogService + Tests) — `dotnet test` grün
4. Task 5 (FlightPage)
5. Task 6 (LogbookPage)
6. Task 7 (FlightDetailPage)
7. Task 4 (AppShell TabBar + Detail-Route) — danach `dotnet build` grün, manueller End-to-End-Test

Tasks 1–3 sind unabhängig testbar (reine Logik). Tasks 4–7 bilden ein gemeinsames Build-Gate.

## Hinweise

- Das Projektverzeichnis ist **kein Git-Repository**; alle „Commit"-Schritte entfallen, sofern nicht vorab `git init` ausgeführt wird.
- `Tests/Uhrzeitrechner.Tests.csproj` ist ein eigenständiges Projekt; es wird nicht von der MAUI-`.csproj` referenziert und stört deren Build nicht. Ausführen gezielt mit `dotnet test Tests/Uhrzeitrechner.Tests.csproj`.
