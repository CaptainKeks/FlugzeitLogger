# Swipe-Navigation mit CarouselView Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zuverlässiges Wischen zwischen den vier Hauptseiten auf Android und iOS über eine eingebaute `CarouselView` plus eigene untere Tab-Leiste.

**Architecture:** Shell bleibt als Host für genau eine Seite (`MainTabsPage`) und die Detail-Route. `MainTabsPage` hostet eine `CarouselView` (natives Swipe-Paging) mit den vier Seiteninhalten als `ContentView`s plus eine antippbare Tab-Leiste. Die bisherige `OnAppearing`-Logik der Seiten wird über ein `ITabView`-Interface (`OnSelected`/`OnDeselected`) beim Positionswechsel ausgelöst. Zustand ist extern gehalten (Datei bzw. ein `StundenStore`-Singleton), damit er ein evtl. Recyceln der Views übersteht.

**Tech Stack:** .NET MAUI (net10.0-android/-ios/-maccatalyst/-windows), C#, System.Text.Json, xUnit. Nur eingebaute MAUI-Komponenten.

## Global Constraints

- Keine neuen NuGet-Pakete (nur `Microsoft.Maui.Controls`).
- Cross-Platform-Swipe muss auf Android UND iOS funktionieren → `CarouselView` (nicht `TabbedPage`).
- `DisplayAlertAsync` ist eine `Page`-Methode; in `ContentView` über `Shell.Current.DisplayAlertAsync(...)` aufrufen. (`DisplayAlert` ist in .NET 10 obsolet — nicht verwenden.)
- ContentViews liegen im Namespace `Uhrzeitrechner.Views`, Host/Shell in `Uhrzeitrechner`.
- Startposition der CarouselView: Flug (Index 1). Reihenfolge `[Stunden(0), Flug(1), Logbuch(2), Einstellungen(3)]`.
- Klassen, die im Test-Projekt getestet werden, bekommen einen `public`-Konstruktor und werden per `<Compile Include>` ins Test-csproj gelinkt. MAUI-abhängige Typen (Views, Pages) werden NICHT ins Test-Projekt gelinkt.
- Nullable aktiviert.
- Composite-Key für Flug-Identität bleibt `Date` + `Registration` + `OffBlock` (unverändert).

## Test- und Build-Befehle

- Tests: `dotnet test Tests/Uhrzeitrechner.Tests.csproj`
- Einzelne Testklasse: `dotnet test Tests/Uhrzeitrechner.Tests.csproj --filter "FullyQualifiedName~<Klassenname>"`
- App-Build: `dotnet build Uhrzeitrechner.csproj -f net10.0-windows10.0.19041.0`

---

### Task 1: `TimeEntry`-Modell + `StundenStore`-Singleton (TDD)

**Files:**
- Create: `Models/TimeEntry.cs`
- Create: `Services/StundenStore.cs`
- Modify: `Tests/Uhrzeitrechner.Tests.csproj`
- Test: `Tests/StundenStoreTests.cs` (neu)

**Interfaces:**
- Produces:
  - `class TimeEntry` mit `TimeSpan Time { get; }`, `string Display { get; }`, Ctor `TimeEntry(TimeSpan time)`.
  - `class StundenStore` mit `public StundenStore()`, `static StundenStore Instance { get; }`, `ObservableCollection<TimeEntry> Entries { get; }`, `TimeSpan Total { get; }`, `static string FormatTotal(TimeSpan total)`, `void Add(TimeSpan time)`, `void Remove(TimeEntry entry)`, `void Clear()`.

- [ ] **Step 1: Modell `TimeEntry` anlegen**

`Models/TimeEntry.cs`:

```csharp
namespace Uhrzeitrechner.Models;

public class TimeEntry
{
    public TimeSpan Time { get; }
    public string Display => $"{(int)Time.TotalHours}:{Time.Minutes:D2} Std";

    public TimeEntry(TimeSpan time) => Time = time;
}
```

- [ ] **Step 2: Test-Projekt um die neuen Dateien erweitern**

In `Tests/Uhrzeitrechner.Tests.csproj` im zweiten `<ItemGroup>` (nach der `FlightSessionStore.cs`-Zeile) ergänzen:

```xml
    <Compile Include="..\Models\TimeEntry.cs" Link="Models\TimeEntry.cs" />
    <Compile Include="..\Services\StundenStore.cs" Link="Services\StundenStore.cs" />
```

- [ ] **Step 3: Fehlertests schreiben**

`Tests/StundenStoreTests.cs`:

```csharp
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
```

- [ ] **Step 4: Tests ausführen, Fehlschlag bestätigen**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj --filter "FullyQualifiedName~StundenStoreTests"`
Expected: FAIL — Kompilierfehler „'StundenStore' could not be found".

- [ ] **Step 5: `StundenStore` implementieren**

`Services/StundenStore.cs`:

```csharp
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
```

- [ ] **Step 6: Tests ausführen, Erfolg bestätigen**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj --filter "FullyQualifiedName~StundenStoreTests"`
Expected: PASS (6 Tests).

- [ ] **Step 7: Commit**

```bash
git add Models/TimeEntry.cs Services/StundenStore.cs Tests/StundenStoreTests.cs Tests/Uhrzeitrechner.Tests.csproj
git commit -m "feat: TimeEntry-Modell und StundenStore-Singleton mit Tests"
```

---

### Task 2: `ITabView`-Interface + `StundenView` (aus MainPage)

**Files:**
- Create: `Views/ITabView.cs`
- Create: `Views/StundenView.xaml`
- Create: `Views/StundenView.xaml.cs`

**Interfaces:**
- Consumes: `StundenStore` (Task 1).
- Produces:
  - `interface ITabView { void OnSelected(); void OnDeselected(); }` im Namespace `Uhrzeitrechner.Views`.
  - `class StundenView : ContentView, ITabView` im Namespace `Uhrzeitrechner.Views`.

UI-Aufgabe — Verifikation per App-Build. (Die alten Seiten bleiben vorerst bestehen; alles kompiliert weiter.)

- [ ] **Step 1: `ITabView` anlegen**

`Views/ITabView.cs`:

```csharp
namespace Uhrzeitrechner.Views;

public interface ITabView
{
    void OnSelected();
    void OnDeselected();
}
```

- [ ] **Step 2: `StundenView.xaml` anlegen**

`Views/StundenView.xaml` (Inhalt von `MainPage.xaml` ohne Root-`ContentPage`/`Title`/`SwipeGestureRecognizer`):

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Uhrzeitrechner.Views.StundenView">

    <Grid RowDefinitions="Auto,Auto,*,Auto" Padding="20" RowSpacing="15">

        <!-- Eingabe -->
        <VerticalStackLayout Grid.Row="0" Spacing="20">

            <!-- Aktuelle Zeiten -->
            <Grid ColumnDefinitions="*,*" ColumnSpacing="10">
                <HorizontalStackLayout Grid.Column="0" Spacing="8" VerticalOptions="Center">
                    <Label Text="UTC" FontSize="Medium" TextColor="Gray" VerticalOptions="Center" />
                    <Label x:Name="UtcTimeLabel" Text="--:--:--" FontSize="Medium" FontAttributes="Bold" VerticalOptions="Center" />
                </HorizontalStackLayout>
                <HorizontalStackLayout Grid.Column="1" Spacing="8" VerticalOptions="Center">
                    <Label Text="Lokal" FontSize="Medium" TextColor="Gray" VerticalOptions="Center" />
                    <Label x:Name="LocalTimeLabel" Text="--:--:--" FontSize="Medium" FontAttributes="Bold" VerticalOptions="Center" />
                </HorizontalStackLayout>
            </Grid>

            <Label Text="Zeit hinzufügen" FontSize="20" FontAttributes="Bold" />
            <Grid ColumnDefinitions="*,Auto,*,Auto" ColumnSpacing="8">
                <Entry x:Name="HoursEntry"
               Grid.Column="0"
               Placeholder="Std"
               FontSize="Medium"
               Keyboard="Numeric"
               MaxLength="5" />
                <Label Grid.Column="1" Text=":" VerticalOptions="Center" FontSize="20" />
                <Entry x:Name="MinutesEntry"
               Grid.Column="2"
               Placeholder="Min"
               FontSize="Medium"
               Keyboard="Numeric"
               MaxLength="3" />
                <Button Grid.Column="3"
                Text="+"
                FontSize="Medium"
                FontAttributes="Bold"
                WidthRequest="55"
                HeightRequest="55"
                Clicked="OnAddClicked" />
            </Grid>
        </VerticalStackLayout>

        <!-- Trenner -->
        <BoxView Grid.Row="1" HeightRequest="1" Color="LightGray" />

        <!-- Liste der Einträge -->
        <CollectionView Grid.Row="2" x:Name="EntriesView">
            <CollectionView.ItemTemplate>
                <DataTemplate>
                    <Grid ColumnDefinitions="*,Auto" Padding="5,2">
                        <Label Grid.Column="0"
                               Text="{Binding Display}"
                               FontSize="Medium"
                               VerticalOptions="Center" />
                        <Button Grid.Column="1"
                                Text="✕"
                                FontSize="Medium"
                                WidthRequest="40"
                                HeightRequest="40"
                                Padding="0"
                                BackgroundColor="Transparent"
                                TextColor="Red"
                                Clicked="OnRemoveClicked"
                                CommandParameter="{Binding .}" />
                    </Grid>
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>

        <!-- Summe -->
        <VerticalStackLayout Grid.Row="3" Spacing="10">
            <BoxView HeightRequest="1" Color="LightGray" />
            <Grid ColumnDefinitions="*,Auto">
                <Label Grid.Column="0" Text="Summe:" FontSize="Medium" FontAttributes="Bold" />
                <Label Grid.Column="1" x:Name="TotalLabel" Text="0:00"
                       FontSize="Medium" FontAttributes="Bold" TextColor="DodgerBlue" />
            </Grid>
            <Button Text="Alles löschen" FontSize="Medium" Clicked="OnClearClicked" />
        </VerticalStackLayout>

    </Grid>
</ContentView>
```

- [ ] **Step 3: `StundenView.xaml.cs` anlegen**

`Views/StundenView.xaml.cs`:

```csharp
using Uhrzeitrechner.Services;

namespace Uhrzeitrechner.Views;

public partial class StundenView : ContentView, ITabView
{
    private readonly StundenStore _store = StundenStore.Instance;
    private IDispatcherTimer? _clockTimer;

    public StundenView()
    {
        InitializeComponent();
        EntriesView.ItemsSource = _store.Entries;
        UpdateTotal();
    }

    public void OnSelected()
    {
        UpdateClock();
        _clockTimer = Dispatcher.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (s, e) => UpdateClock();
        _clockTimer.Start();
    }

    public void OnDeselected() => _clockTimer?.Stop();

    private void UpdateClock()
    {
        var now = DateTime.Now;
        LocalTimeLabel.Text = now.ToString("HH:mm:ss");
        UtcTimeLabel.Text = now.ToUniversalTime().ToString("HH:mm:ss");
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        int.TryParse(HoursEntry.Text, out int hours);
        int.TryParse(MinutesEntry.Text, out int minutes);

        if (hours == 0 && minutes == 0)
            return;

        _store.Add(new TimeSpan(hours, minutes, 0));

        HoursEntry.Text = "";
        MinutesEntry.Text = "";
        HoursEntry.Focus();

        UpdateTotal();
    }

    private void OnRemoveClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Models.TimeEntry entry })
        {
            _store.Remove(entry);
            UpdateTotal();
        }
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        _store.Clear();
        UpdateTotal();
    }

    private void UpdateTotal() => TotalLabel.Text = StundenStore.FormatTotal(_store.Total);
}
```

- [ ] **Step 4: App-Build prüfen**

Run: `dotnet build Uhrzeitrechner.csproj -f net10.0-windows10.0.19041.0`
Expected: Build erfolgreich.

- [ ] **Step 5: Commit**

```bash
git add Views/ITabView.cs Views/StundenView.xaml Views/StundenView.xaml.cs
git commit -m "feat: ITabView und StundenView (ContentView aus MainPage)"
```

---

### Task 3: `FlugView` (aus FlightPage)

**Files:**
- Create: `Views/FlugView.xaml`
- Create: `Views/FlugView.xaml.cs`

**Interfaces:**
- Consumes: `ITabView` (Task 2); `FlightSession`, `FlightLogService`, `FlightSessionStore`, `AppPaths`, `FlightMath` (bestehend).
- Produces: `class FlugView : ContentView, ITabView` im Namespace `Uhrzeitrechner.Views`.

- [ ] **Step 1: `FlugView.xaml` anlegen**

`Views/FlugView.xaml` (Inhalt von `FlightPage.xaml` ohne Root-`ContentPage`/`Title`/`SwipeGestureRecognizer`-Block):

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Uhrzeitrechner.Views.FlugView">

    <ScrollView>
        <VerticalStackLayout Padding="20" Spacing="15">

            <!-- Uhr -->
            <Grid ColumnDefinitions="*,*" ColumnSpacing="10">
                <HorizontalStackLayout Grid.Column="0" Spacing="8" VerticalOptions="Center">
                    <Label Text="UTC" FontSize="Medium" TextColor="Gray" VerticalOptions="Center" />
                    <Label x:Name="UtcTimeLabel" Text="--:--:--" FontSize="Medium" FontAttributes="Bold" VerticalOptions="Center" />
                </HorizontalStackLayout>
                <HorizontalStackLayout Grid.Column="1" Spacing="8" VerticalOptions="Center">
                    <Label Text="Lokal" FontSize="Medium" TextColor="Gray" VerticalOptions="Center" />
                    <Label x:Name="LocalTimeLabel" Text="--:--:--" FontSize="Medium" FontAttributes="Bold" VerticalOptions="Center" />
                </HorizontalStackLayout>
            </Grid>

            <BoxView HeightRequest="1" Color="LightGray" />

            <!-- Flugdaten -->
            <Grid ColumnDefinitions="Auto,*" ColumnSpacing="10" RowDefinitions="Auto,Auto" RowSpacing="8">
                <Label Grid.Row="0" Grid.Column="0" FontSize="Medium" Text="Datum:" VerticalOptions="Center" TextColor="Gray" />
                <Label Grid.Row="0" Grid.Column="1" FontSize="Medium" x:Name="DateLabel" Text="—" VerticalOptions="Center" FontAttributes="Bold" />
                <Label Grid.Row="1" Grid.Column="0" FontSize="Medium" Text="Flug:" VerticalOptions="Center" TextColor="Gray" />
                <Entry Grid.Row="1" Grid.Column="1" FontSize="Medium" x:Name="RegistrationEntry" Placeholder="z.B. D-MSSE" TextChanged="OnRegistrationChanged" Completed="OnRegistrationCompleted" />
            </Grid>

            <!-- Erfassungs-Buttons -->
            <Grid ColumnDefinitions="*,*" RowDefinitions="Auto,Auto" ColumnSpacing="10" RowSpacing="10">
                <Button Grid.Row="0" Grid.Column="0" x:Name="OffBlockButton" Text="Off-Block" Clicked="OnOffBlockClicked" FontAttributes="Bold" FontSize="Medium" HeightRequest="55" />
                <Button Grid.Row="1" Grid.Column="0" x:Name="OnBlockButton" Text="On-Block" Clicked="OnOnBlockClicked" FontAttributes="Bold" FontSize="Medium" HeightRequest="55" />
                <Button Grid.Row="0" Grid.Column="1" x:Name="StartButton" Text="Start" Clicked="OnStartClicked" FontAttributes="Bold" FontSize="Medium" HeightRequest="55" />
                <Button Grid.Row="1" Grid.Column="1" x:Name="LandingButton" Text="Landung" Clicked="OnLandingClicked" FontAttributes="Bold" FontSize="Medium" HeightRequest="55" />
            </Grid>

            <Button x:Name="UndoButton" Text="Letzte Erfassung rückgängig" Clicked="OnUndoClicked"
                    BackgroundColor="Transparent" FontSize="Small" TextColor="Orange"
                    BorderColor="Orange" BorderWidth="1" />

            <BoxView HeightRequest="1" Color="LightGray" />

            <!-- Ergebnisse -->
            <Grid ColumnDefinitions="*,*" RowDefinitions="Auto,Auto,Auto,Auto" RowSpacing="10" ColumnSpacing="10">

                <Grid Grid.Row="0" Grid.Column="0" ColumnDefinitions="Auto,*" ColumnSpacing="6">
                    <Label Grid.Column="0" Text="Off-Block" FontSize="Small" TextColor="Gray" VerticalOptions="Center" />
                    <Label Grid.Column="1" x:Name="OffBlockResultLabel" Text="—" FontSize="Medium" FontAttributes="Bold" TextColor="DodgerBlue" VerticalOptions="Center" HorizontalTextAlignment="End" />
                </Grid>
                <Grid Grid.Row="0" Grid.Column="1" ColumnDefinitions="Auto,*" ColumnSpacing="6">
                    <Label Grid.Column="0" Text="Erster Start" FontSize="Small" TextColor="Gray" VerticalOptions="Center" />
                    <Label Grid.Column="1" x:Name="FirstTakeoffLabel" Text="—" FontSize="Medium" FontAttributes="Bold" TextColor="DodgerBlue" VerticalOptions="Center" HorizontalTextAlignment="End" />
                </Grid>

                <Grid Grid.Row="1" Grid.Column="0" ColumnDefinitions="Auto,*" ColumnSpacing="6">
                    <Label Grid.Column="0" Text="On-Block" FontSize="Small" TextColor="Gray" VerticalOptions="Center" />
                    <Label Grid.Column="1" x:Name="OnBlockResultLabel" Text="—" FontSize="Medium" FontAttributes="Bold" TextColor="DodgerBlue" VerticalOptions="Center" HorizontalTextAlignment="End" />
                </Grid>
                <Grid Grid.Row="1" Grid.Column="1" ColumnDefinitions="Auto,*" ColumnSpacing="6">
                    <Label Grid.Column="0" Text="Letzte Ldg." FontSize="Small" TextColor="Gray" VerticalOptions="Center" />
                    <Label Grid.Column="1" x:Name="LastLandingLabel" Text="—" FontSize="Medium" FontAttributes="Bold" TextColor="DodgerBlue" VerticalOptions="Center" HorizontalTextAlignment="End" />
                </Grid>

                <Grid Grid.Row="2" Grid.Column="0" ColumnDefinitions="Auto,*" ColumnSpacing="6">
                    <Label Grid.Column="0" Text="Blockzeit" FontSize="Small" TextColor="Gray" VerticalOptions="Center" />
                    <Label Grid.Column="1" x:Name="BlockTimeLabel" Text="—" FontSize="Medium" FontAttributes="Bold" TextColor="DodgerBlue" VerticalOptions="Center" HorizontalTextAlignment="End" />
                </Grid>
                <Grid Grid.Row="2" Grid.Column="1" ColumnDefinitions="Auto,*" ColumnSpacing="6">
                    <Label Grid.Column="0" Text="Flugzeit" FontSize="Small" TextColor="Gray" VerticalOptions="Center" />
                    <Label Grid.Column="1" x:Name="FlightTimeLabel" Text="0:00" FontSize="Medium" FontAttributes="Bold" TextColor="DodgerBlue" VerticalOptions="Center" HorizontalTextAlignment="End" />
                </Grid>

                <Grid Grid.Row="3" Grid.Column="0" ColumnDefinitions="Auto,*" ColumnSpacing="6">
                    <Label Grid.Column="0" Text="Landungen" FontSize="Small" TextColor="Gray" VerticalOptions="Center" />
                    <Label Grid.Column="1" x:Name="LandingCountLabel" Text="0" FontSize="Medium" FontAttributes="Bold" TextColor="DodgerBlue" VerticalOptions="Center" HorizontalTextAlignment="End" />
                </Grid>

            </Grid>

            <Grid ColumnDefinitions="*,*" ColumnSpacing="10">
                <Button Grid.Column="0" x:Name="ResetButton" Text="Zurücksetzen" FontSize="Small" Clicked="OnResetClicked"
                        BackgroundColor="Transparent" TextColor="Red"
                        BorderColor="Red" BorderWidth="1" />
                <Button Grid.Column="1" x:Name="SaveButton" Text="Flug speichern" FontSize="Small" Clicked="OnSaveClicked" />
            </Grid>

            <BoxView HeightRequest="1" Color="LightGray" />

            <!-- Legs -->
            <CollectionView x:Name="LegsView">
                <CollectionView.ItemTemplate>
                    <DataTemplate>
                        <Grid Padding="0,6">
                            <Label Text="{Binding Display}" FontSize="Small" />
                        </Grid>
                    </DataTemplate>
                </CollectionView.ItemTemplate>
            </CollectionView>

        </VerticalStackLayout>
    </ScrollView>
</ContentView>
```

- [ ] **Step 2: `FlugView.xaml.cs` anlegen**

`Views/FlugView.xaml.cs` (aus `FlightPage.xaml.cs`: Klasse → `FlugView : ContentView, ITabView`; `OnAppearing`→`OnSelected` ohne `base`-Aufruf; `OnDisappearing`→`OnDeselected` plus best-effort Persist; `OnSwipeLeft/Right` entfernt; `DisplayAlertAsync`→`Shell.Current.DisplayAlertAsync`):

```csharp
using System.Collections.ObjectModel;
using Uhrzeitrechner.Services;

namespace Uhrzeitrechner.Views;

public partial class FlugView : ContentView, ITabView
{
    private readonly FlightSession _session = new();
    private readonly FlightLogService _log = new(AppPaths.FlightLogPath);
    private readonly FlightSessionStore _store = new(AppPaths.SessionPath);
    private bool _restored;
    private readonly ObservableCollection<LegRow> _legRows = new();
    private IDispatcherTimer? _clockTimer;

    public FlugView()
    {
        InitializeComponent();
        LegsView.ItemsSource = _legRows;
        RefreshState();
    }

    public async void OnSelected()
    {
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

    public void OnDeselected()
    {
        _clockTimer?.Stop();
        _ = PersistAsync();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        UtcTimeLabel.Text = now.ToUniversalTime().ToString("HH:mm:ss");
        LocalTimeLabel.Text = now.ToString("HH:mm:ss");
        DateLabel.Text = now.ToString("dd.MM.yyyy");
    }

    private void OnRegistrationChanged(object? sender, TextChangedEventArgs e)
    {
        _session.Registration = e.NewTextValue ?? string.Empty;
        RefreshState();
    }

    private async void OnRegistrationCompleted(object? sender, EventArgs e) { RegistrationEntry.Unfocus(); await PersistAsync(); }

    private async void OnOffBlockClicked(object? sender, EventArgs e) { _session.OffBlock(); RefreshState(); await PersistAsync(); }
    private async void OnStartClicked(object? sender, EventArgs e) { _session.Start(); RefreshState(); await PersistAsync(); }
    private async void OnLandingClicked(object? sender, EventArgs e) { _session.Landing(); RefreshState(); await PersistAsync(); }
    private async void OnOnBlockClicked(object? sender, EventArgs e) { _session.OnBlock(); RefreshState(); await PersistAsync(); }
    private async void OnUndoClicked(object? sender, EventArgs e) { _session.Undo(); RefreshState(); await PersistAsync(); }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!_session.CanSave) return;
        await _log.AddAsync(_session.Flight);
        _session.Reset();
        await _store.ClearAsync();
        RegistrationEntry.Text = string.Empty;
        RefreshState();
        await Shell.Current.DisplayAlertAsync("Gespeichert", "Flug wurde im Logbuch gespeichert.", "OK");
    }

    private async void OnResetClicked(object? sender, EventArgs e)
    {
        bool ok = await Shell.Current.DisplayAlertAsync("Zurücksetzen",
            "Aktuellen Flug verwerfen?", "Ja", "Abbrechen");
        if (!ok) return;
        _session.Reset();
        await _store.ClearAsync();
        RegistrationEntry.Text = string.Empty;
        RefreshState();
    }

    private async Task PersistAsync()
    {
        try { await _store.SaveAsync(_session.Flight); }
        catch { /* best-effort: Persistenz darf die UI nicht stören */ }
    }

    private void RefreshState()
    {
        OffBlockButton.IsEnabled = _session.CanOffBlock;
        StartButton.IsEnabled = _session.CanStart;
        LandingButton.IsEnabled = _session.CanLanding;
        OnBlockButton.IsEnabled = _session.CanOnBlock;
        UndoButton.IsEnabled = _session.CanUndo;
        SaveButton.IsEnabled = _session.CanSave;

        _legRows.Clear();
        for (int i = 0; i < _session.Legs.Count; i++)
            _legRows.Add(new LegRow(i + 1, _session.Legs[i]));

        OffBlockResultLabel.Text = _session.Flight.OffBlock?.ToString("HH:mm") ?? "—";
        OnBlockResultLabel.Text = _session.Flight.OnBlock?.ToString("HH:mm") ?? "—";

        FirstTakeoffLabel.Text = _session.Legs.Count > 0
            ? _session.Legs[0].Takeoff?.ToString("HH:mm") ?? "—"
            : "—";

        var lastLanding = _session.Legs.LastOrDefault(l => l.Landing is not null)?.Landing;
        LastLandingLabel.Text = lastLanding?.ToString("HH:mm") ?? "—";

        LandingCountLabel.Text = _session.Legs.Count(l => l.Landing is not null).ToString();

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

- [ ] **Step 3: App-Build prüfen**

Run: `dotnet build Uhrzeitrechner.csproj -f net10.0-windows10.0.19041.0`
Expected: Build erfolgreich.

- [ ] **Step 4: Commit**

```bash
git add Views/FlugView.xaml Views/FlugView.xaml.cs
git commit -m "feat: FlugView (ContentView aus FlightPage) mit ITabView-Lifecycle"
```

---

### Task 4: `LogbuchView` (aus LogbookPage)

**Files:**
- Create: `Views/LogbuchView.xaml`
- Create: `Views/LogbuchView.xaml.cs`

**Interfaces:**
- Consumes: `ITabView` (Task 2); `FlightLogService`, `AppPaths`, `FlightMath`, `Flight` (bestehend); Route `nameof(FlightDetailPage)` (bestehend).
- Produces: `class LogbuchView : ContentView, ITabView` im Namespace `Uhrzeitrechner.Views`.

- [ ] **Step 1: `LogbuchView.xaml` anlegen**

`Views/LogbuchView.xaml` (aus `LogbookPage.xaml` ohne Root-`ContentPage`/`Title`/`SwipeGestureRecognizer`):

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Uhrzeitrechner.Views.LogbuchView">

    <Grid Padding="20">
        <CollectionView x:Name="FlightsView" SelectionMode="Single"
                        SelectionChanged="OnFlightSelected">
            <CollectionView.EmptyView>
                <Label Text="Noch keine Flüge gespeichert"
                       HorizontalOptions="Center" VerticalOptions="Center"
                       FontSize="18" TextColor="Gray" />
            </CollectionView.EmptyView>
            <CollectionView.ItemTemplate>
                <DataTemplate>
                    <Grid ColumnDefinitions="*,Auto" Padding="0,10" ColumnSpacing="10">
                        <VerticalStackLayout Grid.Column="0">
                            <Label Text="{Binding DateTimeText}" FontAttributes="Bold" FontSize="20" />
                            <Label Text="{Binding Registration}" FontAttributes="Bold" FontSize="20" TextColor="DodgerBlue" />
                            <Label Text="{Binding Summary}" FontSize="18" TextColor="Gray" />
                        </VerticalStackLayout>
                        <Button Grid.Column="1" Text="✕" FontSize="20"
                                WidthRequest="48" HeightRequest="48"
                                BackgroundColor="Transparent" TextColor="Red"
                                Clicked="OnDeleteClicked"
                                CommandParameter="{Binding Flight}" />
                    </Grid>
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>
    </Grid>
</ContentView>
```

- [ ] **Step 2: `LogbuchView.xaml.cs` anlegen**

`Views/LogbuchView.xaml.cs` (aus `LogbookPage.xaml.cs`: Klasse → `LogbuchView : ContentView, ITabView`; `OnAppearing`→`OnSelected`; `OnSwipeRight` entfernt; `DisplayAlertAsync`→`Shell.Current.DisplayAlertAsync`):

```csharp
using System.Collections.ObjectModel;
using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;

namespace Uhrzeitrechner.Views;

public partial class LogbuchView : ContentView, ITabView
{
    private readonly FlightLogService _log = new(AppPaths.FlightLogPath);
    private readonly ObservableCollection<FlightRow> _rows = new();

    public LogbuchView()
    {
        InitializeComponent();
        FlightsView.ItemsSource = _rows;
    }

    public async void OnSelected() => await ReloadAsync();

    public void OnDeselected() { }

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
        bool ok = await Shell.Current.DisplayAlertAsync("Löschen",
            $"Flug {flight.Registration} löschen?", "Ja", "Abbrechen");
        if (!ok) return;
        await _log.DeleteAsync(flight);
        await ReloadAsync();
    }

    public class FlightRow
    {
        public Flight Flight { get; }
        public string DateText => Flight.Date.ToString("dd.MM.yyyy");
        public string DateTimeText => Flight.OffBlock is { } off
            ? off.ToString("dd.MM.yyyy HH:mm")
            : Flight.Date.ToString("dd.MM.yyyy");
        public string Registration => Flight.Registration;
        public string Summary =>
            $"Block {FlightMath.FormatDuration(FlightMath.BlockTime(Flight))} · " +
            $"Flug {FlightMath.FormatDuration(FlightMath.FlightTime(Flight))}";
        public FlightRow(Flight flight) => Flight = flight;
    }
}
```

- [ ] **Step 3: App-Build prüfen**

Run: `dotnet build Uhrzeitrechner.csproj -f net10.0-windows10.0.19041.0`
Expected: Build erfolgreich.

- [ ] **Step 4: Commit**

```bash
git add Views/LogbuchView.xaml Views/LogbuchView.xaml.cs
git commit -m "feat: LogbuchView (ContentView aus LogbookPage)"
```

---

### Task 5: `EinstellungenView` (aus SettingsPage)

**Files:**
- Create: `Views/EinstellungenView.xaml`
- Create: `Views/EinstellungenView.xaml.cs`

**Interfaces:**
- Consumes: `ITabView` (Task 2); `FlightLogService`, `AppPaths`, `Flight` (bestehend); eingebaute `Share`/`FilePicker`.
- Produces: `class EinstellungenView : ContentView, ITabView` im Namespace `Uhrzeitrechner.Views`.

- [ ] **Step 1: `EinstellungenView.xaml` anlegen**

`Views/EinstellungenView.xaml` (aus `SettingsPage.xaml` ohne Root-`ContentPage`/`Title`):

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Uhrzeitrechner.Views.EinstellungenView">

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
</ContentView>
```

- [ ] **Step 2: `EinstellungenView.xaml.cs` anlegen**

`Views/EinstellungenView.xaml.cs` (aus `SettingsPage.xaml.cs`: Klasse → `EinstellungenView : ContentView, ITabView`; `OnAppearing`→`OnSelected`; alle `DisplayAlertAsync`→`Shell.Current.DisplayAlertAsync`):

```csharp
using System.Text.Json;
using Uhrzeitrechner.Models;
using Uhrzeitrechner.Services;

namespace Uhrzeitrechner.Views;

public partial class EinstellungenView : ContentView, ITabView
{
    private readonly FlightLogService _log = new(AppPaths.FlightLogPath);

    public EinstellungenView()
    {
        InitializeComponent();
    }

    public async void OnSelected()
    {
        PathLabel.Text = _log.FilePath;
        var flights = await _log.LoadAsync();
        CountLabel.Text = $"{flights.Count} Flüge im Logbuch";
    }

    public void OnDeselected() { }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (!File.Exists(_log.FilePath))
        {
            await Shell.Current.DisplayAlertAsync("Export", "Es sind noch keine Flüge gespeichert.", "OK");
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
            await Shell.Current.DisplayAlertAsync("Fehler", $"Export fehlgeschlagen: {ex.Message}", "OK");
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
                await Shell.Current.DisplayAlertAsync("Import", "Datei enthält keine Flüge.", "OK");
                return;
            }

            int added = await _log.MergeAsync(incoming);
            int skipped = incoming.Count - added;
            await Shell.Current.DisplayAlertAsync("Import",
                $"{added} Flüge importiert, {skipped} übersprungen.", "OK");

            var flights = await _log.LoadAsync();
            CountLabel.Text = $"{flights.Count} Flüge im Logbuch";
        }
        catch (JsonException)
        {
            await Shell.Current.DisplayAlertAsync("Fehler", "Die Datei ist keine gültige Logbuch-Datei.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Fehler", $"Import fehlgeschlagen: {ex.Message}", "OK");
        }
    }
}
```

- [ ] **Step 3: App-Build prüfen**

Run: `dotnet build Uhrzeitrechner.csproj -f net10.0-windows10.0.19041.0`
Expected: Build erfolgreich.

- [ ] **Step 4: Commit**

```bash
git add Views/EinstellungenView.xaml Views/EinstellungenView.xaml.cs
git commit -m "feat: EinstellungenView (ContentView aus SettingsPage)"
```

---

### Task 6: `MainTabsPage` (Host mit CarouselView + Tab-Leiste + Lifecycle)

**Files:**
- Create: `MainTabsPage.xaml`
- Create: `MainTabsPage.xaml.cs`

**Interfaces:**
- Consumes: `StundenView`, `FlugView`, `LogbuchView`, `EinstellungenView`, `ITabView` (Tasks 2–5).
- Produces: `class MainTabsPage : ContentPage` im Namespace `Uhrzeitrechner`.

UI-Aufgabe — Verifikation per App-Build (live geschaltet wird sie erst in Task 7).

- [ ] **Step 1: `MainTabsPage.xaml` anlegen**

`MainTabsPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Uhrzeitrechner.MainTabsPage"
             Shell.NavBarIsVisible="False">

    <Grid RowDefinitions="*,Auto">

        <CarouselView x:Name="Pager" Grid.Row="0"
                      Loop="False"
                      IsBounceEnabled="False"
                      HorizontalScrollBarVisibility="Never">
            <CarouselView.ItemTemplate>
                <DataTemplate>
                    <ContentView Content="{Binding .}" />
                </DataTemplate>
            </CarouselView.ItemTemplate>
        </CarouselView>

        <Grid Grid.Row="1" ColumnDefinitions="*,*,*,*" HeightRequest="56"
              BackgroundColor="#F0F0F0">
            <Button x:Name="TabStunden" Grid.Column="0" Text="Stunden"
                    FontSize="Caption" BackgroundColor="Transparent" TextColor="Gray"
                    Clicked="OnTabStunden" />
            <Button x:Name="TabFlug" Grid.Column="1" Text="Flug"
                    FontSize="Caption" BackgroundColor="Transparent" TextColor="Gray"
                    Clicked="OnTabFlug" />
            <Button x:Name="TabLogbuch" Grid.Column="2" Text="Logbuch"
                    FontSize="Caption" BackgroundColor="Transparent" TextColor="Gray"
                    Clicked="OnTabLogbuch" />
            <Button x:Name="TabEinstellungen" Grid.Column="3" Text="Einstellungen"
                    FontSize="Caption" BackgroundColor="Transparent" TextColor="Gray"
                    Clicked="OnTabEinstellungen" />
        </Grid>

    </Grid>
</ContentPage>
```

- [ ] **Step 2: `MainTabsPage.xaml.cs` anlegen**

`MainTabsPage.xaml.cs`. Die View-Instanzen werden einmalig erzeugt und als `ItemsSource` gehalten (Zustand bleibt erhalten). `Activate`/`Deactivate` steuern den Lifecycle idempotent über `_activeIndex`:

```csharp
using Uhrzeitrechner.Views;

namespace Uhrzeitrechner;

public partial class MainTabsPage : ContentPage
{
    private const int StartIndex = 1; // Flug

    private readonly List<View> _views;
    private readonly ITabView[] _tabs;
    private readonly Button[] _tabButtons;
    private int _activeIndex = -1;
    private bool _started;

    public MainTabsPage()
    {
        InitializeComponent();

        var stunden = new StundenView();
        var flug = new FlugView();
        var logbuch = new LogbuchView();
        var einstellungen = new EinstellungenView();

        _views = new List<View> { stunden, flug, logbuch, einstellungen };
        _tabs = new ITabView[] { stunden, flug, logbuch, einstellungen };
        _tabButtons = new[] { TabStunden, TabFlug, TabLogbuch, TabEinstellungen };

        Pager.ItemsSource = _views;
        UpdateHighlight(StartIndex);

        Pager.PositionChanged += OnPositionChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_started)
        {
            _started = true;
            Pager.Position = StartIndex;
            Activate(StartIndex);
        }
        else
        {
            Activate(Pager.Position);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Deactivate();
    }

    private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
        => Activate(e.CurrentPosition);

    private void OnTabStunden(object? sender, EventArgs e) => Pager.Position = 0;
    private void OnTabFlug(object? sender, EventArgs e) => Pager.Position = 1;
    private void OnTabLogbuch(object? sender, EventArgs e) => Pager.Position = 2;
    private void OnTabEinstellungen(object? sender, EventArgs e) => Pager.Position = 3;

    private void Activate(int index)
    {
        if (index < 0 || index >= _tabs.Length) return;
        if (_activeIndex == index) return;
        if (_activeIndex >= 0) _tabs[_activeIndex].OnDeselected();
        _activeIndex = index;
        _tabs[index].OnSelected();
        UpdateHighlight(index);
    }

    private void Deactivate()
    {
        if (_activeIndex < 0) return;
        _tabs[_activeIndex].OnDeselected();
        _activeIndex = -1;
    }

    private void UpdateHighlight(int index)
    {
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            _tabButtons[i].TextColor = i == index ? Colors.DodgerBlue : Colors.Gray;
            _tabButtons[i].FontAttributes = i == index ? FontAttributes.Bold : FontAttributes.None;
        }
    }
}
```

- [ ] **Step 3: App-Build prüfen**

Run: `dotnet build Uhrzeitrechner.csproj -f net10.0-windows10.0.19041.0`
Expected: Build erfolgreich.

- [ ] **Step 4: Commit**

```bash
git add MainTabsPage.xaml MainTabsPage.xaml.cs
git commit -m "feat: MainTabsPage hostet CarouselView + Tab-Leiste mit Lifecycle"
```

---

### Task 7: AppShell auf `MainTabsPage` umstellen, alte Seiten entfernen

**Files:**
- Modify: `AppShell.xaml`
- Modify: `AppShell.xaml.cs`
- Delete: `MainPage.xaml`, `MainPage.xaml.cs`, `FlightPage.xaml`, `FlightPage.xaml.cs`, `LogbookPage.xaml`, `LogbookPage.xaml.cs`, `SettingsPage.xaml`, `SettingsPage.xaml.cs`

**Interfaces:**
- Consumes: `MainTabsPage` (Task 6), `FlightDetailPage` (bestehend).

UI-Aufgabe — Verifikation per App-Build plus manuellem Test.

- [ ] **Step 1: `AppShell.xaml` auf eine ShellContent umstellen**

`AppShell.xaml` komplett ersetzen durch:

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Shell
    x:Class="Uhrzeitrechner.AppShell"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:local="clr-namespace:Uhrzeitrechner"
    Title="Flugzeit Logger">

    <ShellContent
        ContentTemplate="{DataTemplate local:MainTabsPage}"
        Route="MainTabsPage" />

</Shell>
```

- [ ] **Step 2: Start-Navigation aus `AppShell.xaml.cs` entfernen**

`AppShell.xaml.cs` komplett ersetzen durch (Detail-Route bleibt registriert; der `GoToAsync("//FlightPage")`-Start entfällt, da es nur noch eine ShellContent gibt und `MainTabsPage` selbst die Startposition setzt):

```csharp
namespace Uhrzeitrechner
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(FlightDetailPage), typeof(FlightDetailPage));
        }
    }
}
```

- [ ] **Step 3: Alte Seiten löschen**

```bash
git rm MainPage.xaml MainPage.xaml.cs FlightPage.xaml FlightPage.xaml.cs LogbookPage.xaml LogbookPage.xaml.cs SettingsPage.xaml SettingsPage.xaml.cs
```

- [ ] **Step 4: App-Build prüfen**

Run: `dotnet build Uhrzeitrechner.csproj -f net10.0-windows10.0.19041.0`
Expected: Build erfolgreich, keine Verweise mehr auf die gelöschten Seiten.

- [ ] **Step 5: Unit-Tests prüfen (Regressionssicherung)**

Run: `dotnet test Tests/Uhrzeitrechner.Tests.csproj`
Expected: PASS (alle bisherigen Tests + die 6 neuen StundenStore-Tests).

- [ ] **Step 6: Manueller Test (vom Nutzer auszuführen, Android/iOS)**

1. App startet auf der Flug-Seite (Index 1).
2. Durch alle vier Seiten wischen (links/rechts), flüssiges Paging.
3. Tab-Leiste antippen wechselt zur richtigen Seite; aktiver Tab ist hervorgehoben.
4. Flug erfassen, zu einer anderen Seite wischen und zurück → Flug-Session erhalten; Uhr läuft.
5. Stunden eintragen, wegwischen und zurück → Liste erhalten.
6. Logbuch → Flug antippen → Detailseite öffnet (mit Zurück-Button); zurück → Position erhalten.
7. Einstellungen → Export/Import funktioniert wie zuvor.

- [ ] **Step 7: Commit**

```bash
git add AppShell.xaml AppShell.xaml.cs
git commit -m "feat: Navigation auf MainTabsPage/CarouselView umgestellt, alte Seiten entfernt"
```

---

## Self-Review

**Spec-Abdeckung:**
- Navigationsstruktur (Shell → eine ShellContent, Detail-Route bleibt, NavBar aus) → Tasks 6, 7. ✓
- `MainTabsPage` mit CarouselView + Tab-Leiste, Startposition Flug → Task 6. ✓
- `ITabView`-Lifecycle (OnSelected/OnDeselected, PositionChanged, OnAppearing/OnDisappearing) → Tasks 2, 6. ✓
- Vier ContentViews mit gemappter Lifecycle-Logik → Tasks 2–5. ✓
- `DisplayAlertAsync` → `Shell.Current.DisplayAlertAsync` → Tasks 3, 4, 5. ✓
- Zustands-Robustheit (`StundenStore`-Singleton, `TimeEntry` ausgelagert) → Tasks 1, 2. ✓
- Alte Seiten entfernt, FlightDetailPage unverändert → Task 7. ✓
- Tests (StundenStore-Aggregation; bestehende Service-Tests gültig) → Tasks 1, 7. ✓
- Keine neuen NuGet-Pakete → nur eingebaute Komponenten. ✓

**Platzhalter:** keine.

**Typ-Konsistenz:** `ITabView.OnSelected/OnDeselected`, `StundenStore.Instance/Entries/Total/FormatTotal/Add/Remove/Clear`, `TimeEntry`, View-Klassennamen (`StundenView`/`FlugView`/`LogbuchView`/`EinstellungenView`) und `MainTabsPage` werden überall konsistent verwendet. ✓

**Hinweis zum Risiko CarouselView-Hosting:** Das Template `<ContentView Content="{Binding .}" />` hostet die im Code-Behind gehaltenen View-Instanzen direkt; ihr Zustand bleibt erhalten. Falls sich dieses Hosting auf einer Plattform als unzuverlässig erweist, ist die externe Zustandshaltung (StundenStore + datei-gestützte Views) das Sicherheitsnetz — ein evtl. Neuaufbau einer View bleibt dann funktional folgenlos. Wird beim manuellen Test in Task 7 verifiziert.
