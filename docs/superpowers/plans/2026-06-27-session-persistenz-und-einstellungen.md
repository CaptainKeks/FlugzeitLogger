# Session-Persistenz & Einstellungen (Export/Import) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die laufende Flugsession übersteht App-Neustarts, und eine neue Einstellungen-Seite erlaubt Export/Import des Logbuchs.

**Architecture:** Auto-Save der laufenden Session in `session.json` nach jeder Aktion (neuer `FlightSessionStore`-Service), Wiederherstellung beim Öffnen der Flug-Seite. Eine neue `SettingsPage` (4. Tab) exportiert `flights.json` per System-Teilen-Menü und importiert per Dateiauswahl mit Zusammenführung (Dubletten übersprungen). Dateipfade werden in `AppPaths` zentralisiert.

**Tech Stack:** .NET MAUI (net10.0-android / -ios / -maccatalyst / -windows), C#, System.Text.Json, xUnit. Eingebaute MAUI-APIs `Share` und `FilePicker` — keine neuen NuGet-Pakete.

## Global Constraints

- Keine neuen NuGet-Pakete (nur `Microsoft.Maui.Controls`).
- `AppPaths` nutzt `FileSystem.AppDataDirectory` (MAUI-Runtime) und darf NICHT ins Test-Projekt gelinkt werden. Services, die getestet werden, bekommen den Pfad per Konstruktor übergeben.
- Composite-Key für Flug-Identität (überall identisch): `Date` + `Registration` + `OffBlock`.
- UI-Methode für Dialoge in diesem Projekt heißt `DisplayAlertAsync` (nicht `DisplayAlert`).
- Nullable ist aktiviert (`<Nullable>enable</Nullable>`).
- JSON-Serialisierung mit `JsonSerializerOptions { WriteIndented = true }` (wie in `FlightLogService`).

## Test- und Build-Befehle

- Tests: `dotnet test Tests/Uhrzeitrechner.Tests.csproj`
- Einzelne Testklasse: `dotnet test Tests/Uhrzeitrechner.Tests.csproj --filter "FullyQualifiedName~<Klassenname>"`
- App-Build (UI-Aufgaben, Windows-Target): `dotnet build Uhrzeitrechner.csproj -f net10.0-windows10.0.19041.0`

---

### Task 1: Pfade zentralisieren (`AppPaths`)

**Files:**
- Create: `Services/AppPaths.cs`
- Modify: `FlightPage.xaml.cs:9-10`
- Modify: `LogbookPage.xaml.cs:9-10`

**Interfaces:**
- Produces: `static class AppPaths` mit `static string FlightLogPath` und `static string SessionPath`.

Reiner Refactor ohne Verhaltensänderung. Kein Unit-Test (Klasse hängt von MAUI-`FileSystem` ab) — Verifikation per App-Build.

- [ ] **Step 1: `AppPaths` anlegen**

`Services/AppPaths.cs`:

```csharp
namespace Uhrzeitrechner.Services;

public static class AppPaths
{
    public static string FlightLogPath =>
        Path.Combine(FileSystem.AppDataDirectory, "flights.json");

    public static string SessionPath =>
        Path.Combine(FileSystem.AppDataDirectory, "session.json");
}
```

- [ ] **Step 2: `FlightPage` auf `AppPaths` umstellen**

In `FlightPage.xaml.cs` die Zeilen 9-10 ersetzen:

```csharp
    private readonly FlightLogService _log = new(AppPaths.FlightLogPath);
```

(Der `using Uhrzeitrechner.Services;` ist bereits vorhanden.)

- [ ] **Step 3: `LogbookPage` auf `AppPaths` umstellen**

In `LogbookPage.xaml.cs` die Zeilen 9-10 ersetzen:

```csharp
    private readonly FlightLogService _log = new(AppPaths.FlightLogPath);
```

- [ ] **Step 4: App-Build prüfen**

Run: `dotnet build Uhrzeitrechner.csproj -f net10.0-windows10.0.19041.0`
Expected: Build erfolgreich, keine Fehler.

- [ ] **Step 5: Commit**

```bash
git add Services/AppPaths.cs FlightPage.xaml.cs LogbookPage.xaml.cs
git commit -m "refactor: Dateipfade in AppPaths zentralisieren"
```

---

### Task 2: `FlightSession.Restore`

**Files:**
- Modify: `Services/FlightSession.cs` (Methode nach `Reset()` ergänzen)
- Test: `Tests/FlightSessionRestoreTests.cs` (neu)

**Interfaces:**
- Consumes: `FlightSession`, `Flight`, `Leg`.
- Produces: `public void FlightSession.Restore(Flight flight)` — setzt die aktuelle `Flight` auf die übergebene Instanz (bei `null` eine neue leere `Flight`).

- [ ] **Step 1: Fehlertest schreiben**

`Tests/FlightSessionRestoreTests.cs`:

```csharp
using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;
using Xunit;

namespace Uhrzeitrechner.Tests;

public class FlightSessionRestoreTests
{
    static DateTime Utc(int h, int m) => new(2026, 6, 27, h, m, 0, DateTimeKind.Utc);

    [Fact]
    public void Restore_SetsCurrentFlight()
    {
        var s = new FlightSession();
        var flight = new Flight
        {
            Registration = "D-TEST",
            Date = new DateTime(2026, 6, 27),
            OffBlock = Utc(9, 0),
            Legs = { new Leg { Takeoff = Utc(9, 10), Landing = Utc(9, 40) } },
        };

        s.Restore(flight);

        Assert.Equal("D-TEST", s.Registration);
        Assert.Equal(Utc(9, 0), s.Flight.OffBlock);
        Assert.Single(s.Legs);
        Assert.False(s.CanOffBlock); // OffBlock bereits gesetzt
    }

    [Fact]
    public void Restore_WithNull_ResetsToEmpty()
    {
        var s = new FlightSession();
        s.Restore(null!);

        Assert.True(s.CanOffBlock);
        Assert.Empty(s.Legs);
        Assert.Equal(string.Empty, s.Registration);
    }
}
```

- [ ] **Step 2: Test ausführen, Fehlschlag bestätigen**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj --filter "FullyQualifiedName~FlightSessionRestoreTests"`
Expected: FAIL — Kompilierfehler „'FlightSession' does not contain a definition for 'Restore'".

- [ ] **Step 3: `Restore` implementieren**

In `Services/FlightSession.cs` direkt nach der Methode `Reset()` (Zeile 80) einfügen:

```csharp
    public void Restore(Flight flight) => Flight = flight ?? new();
```

- [ ] **Step 4: Test ausführen, Erfolg bestätigen**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj --filter "FullyQualifiedName~FlightSessionRestoreTests"`
Expected: PASS (2 Tests).

- [ ] **Step 5: Commit**

```bash
git add Services/FlightSession.cs Tests/FlightSessionRestoreTests.cs
git commit -m "feat: FlightSession.Restore zum Wiederherstellen einer Session"
```

---

### Task 3: `FlightSessionStore`-Service

**Files:**
- Create: `Services/FlightSessionStore.cs`
- Modify: `Tests/Uhrzeitrechner.Tests.csproj` (Compile-Link ergänzen)
- Test: `Tests/FlightSessionStoreTests.cs` (neu)

**Interfaces:**
- Consumes: `Flight`.
- Produces: `class FlightSessionStore` mit
  - `FlightSessionStore(string filePath)`
  - `Task SaveAsync(Flight flight)`
  - `Task<Flight?> LoadAsync()` — `null` wenn Datei fehlt oder beschädigt
  - `Task ClearAsync()`

- [ ] **Step 1: Service-Datei anlegen**

`Services/FlightSessionStore.cs`:

```csharp
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
```

- [ ] **Step 2: Test-Projekt um den Service erweitern**

In `Tests/Uhrzeitrechner.Tests.csproj` innerhalb des zweiten `<ItemGroup>` (nach Zeile 21, der `FlightLogService.cs`-Zeile) ergänzen:

```xml
    <Compile Include="..\Services\FlightSessionStore.cs" Link="Services\FlightSessionStore.cs" />
```

- [ ] **Step 3: Fehlertests schreiben**

`Tests/FlightSessionStoreTests.cs`:

```csharp
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
```

- [ ] **Step 4: Tests ausführen, Erfolg bestätigen**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj --filter "FullyQualifiedName~FlightSessionStoreTests"`
Expected: PASS (4 Tests).

- [ ] **Step 5: Commit**

```bash
git add Services/FlightSessionStore.cs Tests/FlightSessionStoreTests.cs Tests/Uhrzeitrechner.Tests.csproj
git commit -m "feat: FlightSessionStore zum Persistieren der laufenden Session"
```

---

### Task 4: Session-Persistenz in `FlightPage` verdrahten

**Files:**
- Modify: `FlightPage.xaml.cs`

**Interfaces:**
- Consumes: `FlightSessionStore` (Task 3), `FlightSession.Restore` (Task 2), `AppPaths.SessionPath` (Task 1).

UI-Verdrahtung, kein Unit-Test — Verifikation per App-Build plus manueller Test.

- [ ] **Step 1: Store-Feld und Restore-Flag ergänzen**

In `FlightPage.xaml.cs` nach dem `_log`-Feld (Zeile 10) ergänzen:

```csharp
    private readonly FlightSessionStore _store = new(AppPaths.SessionPath);
    private bool _restored;
```

- [ ] **Step 2: Persist-Helfer hinzufügen**

In `FlightPage.xaml.cs` direkt vor `RefreshState()` (Zeile 82) einfügen:

```csharp
    private async Task PersistAsync()
    {
        try { await _store.SaveAsync(_session.Flight); }
        catch { /* best-effort: Persistenz darf die UI nicht stören */ }
    }
```

- [ ] **Step 3: `OnAppearing` um einmalige Wiederherstellung erweitern**

Die vorhandene Methode `OnAppearing` (Zeilen 21-29) komplett ersetzen durch:

```csharp
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_restored)
        {
            _restored = true;
            var saved = await _store.LoadAsync();
            if (saved is not null)
            {
                _session.Restore(saved);
                RegistrationEntry.Text = _session.Registration;
                RefreshState();
            }
        }

        UpdateClock();
        _clockTimer = Dispatcher.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (s, e) => UpdateClock();
        _clockTimer.Start();
    }
```

- [ ] **Step 4: Aktions-Handler auf Persistenz umstellen**

Die fünf Handler (Zeilen 53-57) ersetzen durch:

```csharp
    private async void OnOffBlockClicked(object? sender, EventArgs e) { _session.OffBlock(); RefreshState(); await PersistAsync(); }
    private async void OnStartClicked(object? sender, EventArgs e) { _session.Start(); RefreshState(); await PersistAsync(); }
    private async void OnLandingClicked(object? sender, EventArgs e) { _session.Landing(); RefreshState(); await PersistAsync(); }
    private async void OnOnBlockClicked(object? sender, EventArgs e) { _session.OnBlock(); RefreshState(); await PersistAsync(); }
    private async void OnUndoClicked(object? sender, EventArgs e) { _session.Undo(); RefreshState(); await PersistAsync(); }
```

- [ ] **Step 5: Kennzeichen bei `Completed` persistieren**

Die Methode `OnRegistrationCompleted` (Zeile 51) ersetzen durch:

```csharp
    private async void OnRegistrationCompleted(object? sender, EventArgs e) { RegistrationEntry.Unfocus(); await PersistAsync(); }
```

- [ ] **Step 6: Beim Speichern die Session-Datei löschen**

Die Methode `OnSaveClicked` (Zeilen 62-70) ersetzen durch:

```csharp
    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!_session.CanSave) return;
        await _log.AddAsync(_session.Flight);
        _session.Reset();
        await _store.ClearAsync();
        RegistrationEntry.Text = string.Empty;
        RefreshState();
        await DisplayAlertAsync("Gespeichert", "Flug wurde im Logbuch gespeichert.", "OK");
    }
```

- [ ] **Step 7: Beim Zurücksetzen die Session-Datei löschen**

Die Methode `OnResetClicked` (Zeilen 72-80) ersetzen durch:

```csharp
    private async void OnResetClicked(object? sender, EventArgs e)
    {
        bool ok = await DisplayAlertAsync("Zurücksetzen",
            "Aktuellen Flug verwerfen?", "Ja", "Abbrechen");
        if (!ok) return;
        _session.Reset();
        await _store.ClearAsync();
        RegistrationEntry.Text = string.Empty;
        RefreshState();
    }
```

- [ ] **Step 8: App-Build prüfen**

Run: `dotnet build Uhrzeitrechner.csproj -f net10.0-windows10.0.19041.0`
Expected: Build erfolgreich.

- [ ] **Step 9: Manueller Test (vom Nutzer auszuführen)**

1. App starten, Flug-Tab öffnen, Kennzeichen eingeben, „Off-Block" und „Start" drücken.
2. App vollständig beenden.
3. App erneut öffnen, Flug-Tab → Off-Block-Zeit, Start und Kennzeichen müssen wieder da sein.
4. „Flug speichern" oder „Zurücksetzen" → nach erneutem Start ist die Session leer.

- [ ] **Step 10: Commit**

```bash
git add FlightPage.xaml.cs
git commit -m "feat: laufende Flugsession übersteht App-Neustart"
```

---

### Task 5: `FlightLogService` um `FilePath` und `MergeAsync` erweitern

**Files:**
- Modify: `Services/FlightLogService.cs`
- Test: `Tests/FlightLogServiceTests.cs` (Tests ergänzen)

**Interfaces:**
- Consumes: `Flight`, vorhandene `LoadAsync`/`SaveAllAsync`.
- Produces:
  - `public string FlightLogService.FilePath { get; }`
  - `public Task<int> FlightLogService.MergeAsync(IEnumerable<Flight> incoming)` — ergänzt nur Flüge, deren Composite-Key (`Date` + `Registration` + `OffBlock`) noch nicht existiert; gibt die Anzahl der hinzugefügten zurück.

- [ ] **Step 1: Fehlertests schreiben**

In `Tests/FlightLogServiceTests.cs` vor der schließenden Klammer der Klasse (vor Zeile 67) einfügen:

```csharp
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
```

- [ ] **Step 2: Tests ausführen, Fehlschlag bestätigen**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj --filter "FullyQualifiedName~FlightLogServiceTests"`
Expected: FAIL — Kompilierfehler „'FlightLogService' does not contain a definition for 'FilePath'/'MergeAsync'".

- [ ] **Step 3: `FilePath` und `MergeAsync` implementieren**

In `Services/FlightLogService.cs` nach dem Konstruktor (Zeile 11) den `FilePath`-Getter ergänzen:

```csharp
    public string FilePath => _filePath;
```

Und vor der schließenden Klammer der Klasse (nach `DeleteAsync`, Zeile 47) `MergeAsync` ergänzen:

```csharp
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
```

- [ ] **Step 4: Tests ausführen, Erfolg bestätigen**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj --filter "FullyQualifiedName~FlightLogServiceTests"`
Expected: PASS (alle Tests der Klasse, inkl. der drei neuen).

- [ ] **Step 5: Commit**

```bash
git add Services/FlightLogService.cs Tests/FlightLogServiceTests.cs
git commit -m "feat: FlightLogService.FilePath und MergeAsync für Import"
```

---

### Task 6: Einstellungen-Seite mit Export/Import

**Files:**
- Create: `SettingsPage.xaml`
- Create: `SettingsPage.xaml.cs`
- Modify: `AppShell.xaml` (Tab ergänzen)

**Interfaces:**
- Consumes: `FlightLogService.FilePath` und `FlightLogService.MergeAsync` (Task 5), `AppPaths.FlightLogPath` (Task 1), eingebaute `Share`/`FilePicker`.

UI-Aufgabe — Verifikation per App-Build plus manueller Test.

- [ ] **Step 1: XAML der Einstellungen-Seite anlegen**

`SettingsPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Uhrzeitrechner.SettingsPage"
             Title="Einstellungen">

    <ScrollView>
        <VerticalStackLayout Padding="20" Spacing="15">

            <Label Text="Logbuch" FontSize="Large" FontAttributes="Bold" />

            <Label Text="Speicherort" FontSize="Small" TextColor="Gray" />
            <Label x:Name="PathLabel" Text="—" FontSize="Small" />

            <Label x:Name="CountLabel" Text="—" FontSize="Medium" />

            <BoxView HeightRequest="1" Color="LightGray" />

            <Button x:Name="ExportButton" Text="Logbuch exportieren"
                    Clicked="OnExportClicked" FontAttributes="Bold" HeightRequest="55" />

            <Button x:Name="ImportButton" Text="Logbuch importieren"
                    Clicked="OnImportClicked" FontAttributes="Bold" HeightRequest="55" />

            <Label Text="Export öffnet das Teilen-Menü. Import fügt neue Flüge hinzu; bereits vorhandene werden übersprungen."
                   FontSize="Small" TextColor="Gray" />

        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

- [ ] **Step 2: Code-Behind der Einstellungen-Seite anlegen**

`SettingsPage.xaml.cs`:

```csharp
using System.Text.Json;
using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;

namespace Uhrzeitrechner;

public partial class SettingsPage : ContentPage
{
    private readonly FlightLogService _log = new(AppPaths.FlightLogPath);

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        PathLabel.Text = _log.FilePath;
        var flights = await _log.LoadAsync();
        CountLabel.Text = $"{flights.Count} Flüge im Logbuch";
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (!File.Exists(_log.FilePath))
        {
            await DisplayAlertAsync("Export", "Es sind noch keine Flüge gespeichert.", "OK");
            return;
        }
        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Logbuch exportieren",
                File = new ShareFile(_log.FilePath),
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Fehler", $"Export fehlgeschlagen: {ex.Message}", "OK");
        }
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Logbuch-Datei wählen",
            });
            if (result is null) return; // abgebrochen

            await using var stream = await result.OpenReadAsync();
            var incoming = await JsonSerializer.DeserializeAsync<List<Flight>>(stream);
            if (incoming is null)
            {
                await DisplayAlertAsync("Import", "Datei enthält keine Flüge.", "OK");
                return;
            }

            int added = await _log.MergeAsync(incoming);
            int skipped = incoming.Count - added;
            await DisplayAlertAsync("Import",
                $"{added} Flüge importiert, {skipped} übersprungen.", "OK");

            var flights = await _log.LoadAsync();
            CountLabel.Text = $"{flights.Count} Flüge im Logbuch";
        }
        catch (JsonException)
        {
            await DisplayAlertAsync("Fehler", "Die Datei ist keine gültige Logbuch-Datei.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Fehler", $"Import fehlgeschlagen: {ex.Message}", "OK");
        }
    }
}
```

- [ ] **Step 3: Tab in `AppShell.xaml` ergänzen**

In `AppShell.xaml` nach dem `LogbookPage`-`ShellContent` (Zeile 21) und vor `</TabBar>` einfügen:

```xml
        <ShellContent
            Title="Einstellungen"
            ContentTemplate="{DataTemplate local:SettingsPage}"
            Route="SettingsPage" />
```

- [ ] **Step 4: App-Build prüfen**

Run: `dotnet build Uhrzeitrechner.csproj -f net10.0-windows10.0.19041.0`
Expected: Build erfolgreich.

- [ ] **Step 5: Manueller Test (vom Nutzer auszuführen)**

1. App starten, Tab „Einstellungen" → Speicherort und Fluganzahl werden angezeigt.
2. „Logbuch exportieren" → System-Teilen-Menü erscheint, Datei lässt sich speichern/teilen.
3. „Logbuch importieren" → exportierte Datei wählen → Hinweis „0 importiert, N übersprungen" (alle bereits vorhanden).
4. Eine Datei mit zusätzlichem Flug importieren → nur neue Flüge werden ergänzt, Logbuch-Tab zeigt sie.

- [ ] **Step 6: Commit**

```bash
git add SettingsPage.xaml SettingsPage.xaml.cs AppShell.xaml
git commit -m "feat: Einstellungen-Seite mit Export/Import des Logbuchs"
```

---

## Self-Review

**Spec-Abdeckung:**
- Session-Persistenz (Spec Teil 1) → Tasks 2, 3, 4. ✓
- Einstellungen-Seite mit Export/Import/Anzeige (Spec Teil 2) → Tasks 5, 6. ✓
- Zusammenführen mit Dubletten-Überspringung → Task 5 (`MergeAsync`). ✓
- Pfad-Zentralisierung in `AppPaths` (Spec Aufräumer) → Task 1. ✓
- Tests (Store-Roundtrip, Restore, MergeAsync) → Tasks 2, 3, 5. ✓
- Keine neuen NuGet-Pakete → nur eingebaute APIs verwendet. ✓

**Platzhalter:** keine.

**Typ-Konsistenz:** `FlightSessionStore.SaveAsync/LoadAsync/ClearAsync`, `FlightSession.Restore`, `FlightLogService.FilePath/MergeAsync`, `AppPaths.FlightLogPath/SessionPath` werden überall identisch verwendet. ✓
