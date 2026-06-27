# Swipe-Navigation mit CarouselView — Design

**Datum:** 2026-06-27
**Status:** Genehmigt

## Ziel

Zuverlässiges Wischen zwischen den vier Hauptseiten auf **Android und iOS**.
Die bisherige Lösung (`SwipeGestureRecognizer` auf scrollbarem Inhalt +
`Shell.Current.GoToAsync("//Route")`) ist unzuverlässig (Gesten-Konflikt mit
dem Scrollen, kein Paging-Gefühl, hartes Umsetzen) und fehlt auf der neuen
Einstellungen-Seite ganz.

Lösung: eingebaute **`CarouselView`** als Swipe-Paging-Komponente (nativ und
konsistent auf Android+iOS), kombiniert mit einer eigenen unteren Tab-Leiste
zum Antippen.

**Nicht im Umfang:** `TabbedPage` (swipt nur auf Android nativ, nicht iOS);
Drittanbieter-Tab-Bibliotheken (vermeiden zusätzliche Abhängigkeit);
Detail-Navigation (`FlightDetailPage` bleibt unverändert per Push-Route).

## Kontext (Ist-Zustand)

- `AppShell` definiert eine `<TabBar>` mit vier `<ShellContent>`: `MainPage`
  (Stunden), `FlightPage` (Flug), `LogbookPage` (Logbuch), `SettingsPage`
  (Einstellungen). Start via `GoToAsync("//FlightPage")`.
- Jede Seite ist eine `ContentPage` mit `OnAppearing`-Logik: Uhr-Timer
  (Stunden, Flug), einmaliger Session-Restore (Flug), Logbuch-Reload (Logbuch),
  Pfad/Anzahl laden (Einstellungen).
- Swipe-Navigation via `SwipeGestureRecognizer` auf dem Inhalts-Layout +
  `OnSwipeLeft/Right` → `GoToAsync("//Route")`. `SettingsPage` hat keinen Swipe.
- `FlightDetailPage` wird per `Shell.Current.GoToAsync(nameof(FlightDetailPage),
  dict)` mit `IQueryAttributable` geöffnet.
- Abhängigkeiten: nur `Microsoft.Maui.Controls`. **Keine neuen NuGet-Pakete.**

## Architektur

### Navigationsstruktur

- `Shell` bleibt erhalten, dient aber nur noch als Host für **eine** Seite und
  für die Detail-Route.
- `AppShell.xaml`: `<TabBar>` mit vier `<ShellContent>` wird durch ein einzelnes
  `<ShellContent ContentTemplate="{DataTemplate local:MainTabsPage}"
  Route="MainTabsPage" />` ersetzt.
- `AppShell.xaml.cs`: `Routing.RegisterRoute(nameof(FlightDetailPage), …)` bleibt;
  der Start-`GoToAsync("//FlightPage")` entfällt (Shell startet automatisch auf
  der einzigen ShellContent; die Startposition setzt `MainTabsPage`).
- `MainTabsPage` blendet die Shell-Navigationsleiste aus
  (`Shell.NavBarIsVisible="False"`). `FlightDetailPage` behält ihre Leiste samt
  Zurück-Button.

### `MainTabsPage` (Host)

- `ContentPage` mit `Grid RowDefinitions="*,Auto"`.
- **Zeile 0:** `CarouselView x:Name="Pager"` mit `Loop="False"` und
  horizontalem Paging.
  - Die vier Inhalts-Views werden **einmalig** im Code-Behind erzeugt und in
    einer Liste (`IList<View>`) gehalten, an die `Pager.ItemsSource` gebunden ist.
    Das `ItemTemplate` hostet die jeweilige Instanz
    (`<ContentView Content="{Binding .}" />`). Dadurch bleiben die View-
    Instanzen und ihr Zustand über das Wischen hinweg erhalten.
- **Zeile 1:** Tab-Leiste aus vier Buttons (Stunden/Flug/Logbuch/Einstellungen).
  Tippen → `Pager.Position = i`. `Pager.PositionChanged` aktualisiert die
  optische Hervorhebung des aktiven Tabs.
- **Startposition:** Flug (Index 1).
- Reihenfolge: `[Stunden(0), Flug(1), Logbuch(2), Einstellungen(3)]`.

### Lifecycle

- Interface `ITabView { void OnSelected(); void OnDeselected(); }`, implementiert
  von allen vier ContentViews.
- `Pager.PositionChanged`: `OnDeselected()` auf der zuvor aktiven View,
  `OnSelected()` auf der neuen aktiven View.
- `MainTabsPage.OnAppearing`: Startposition setzen (falls erstes Mal) und
  `OnSelected()` auf der aktuell aktiven View aufrufen.
- `MainTabsPage.OnDisappearing`: `OnDeselected()` auf der aktuell aktiven View
  (z. B. wenn `FlightDetailPage` darüberliegt → Timer stoppen).

### Die vier ContentViews

Inhalt und Code wandern aus den bisherigen ContentPages in ContentViews; die
`OnAppearing`-Logik wird zu `OnSelected`, das Aufräumen (Timer stoppen,
persistieren) zu `OnDeselected`.

| Neu | Aus | OnSelected | OnDeselected |
|-----|-----|-----------|--------------|
| `StundenView` | `MainPage` | Uhr-Timer starten | Uhr-Timer stoppen |
| `FlugView` | `FlightPage` | Session-Restore (einmalig, Flag) + Uhr-Timer + `RefreshState` | Timer stoppen + `PersistAsync` |
| `LogbuchView` | `LogbookPage` | `ReloadAsync` | — |
| `EinstellungenView` | `SettingsPage` | Pfad + Fluganzahl aktualisieren | — |

- Alle bestehenden Handler (Buttons, Entry, Export/Import, Delete, Detail-Öffnen)
  bleiben funktional gleich.
- `DisplayAlertAsync` ist eine `Page`-Methode und in `ContentView` nicht
  verfügbar → Aufruf über `Shell.Current.DisplayAlertAsync(…)` (Shell leitet von
  Page ab und ist hier nicht null). Betroffen: FlugView (Speichern/Zurücksetzen),
  EinstellungenView (Export/Import-Meldungen).
- `Share` und `FilePicker` benötigen keine Page-Referenz — unverändert.

### Zustands-Robustheit

- Neue Singleton-Klasse `Services/StundenStore` hält die
  `ObservableCollection<TimeEntry>` und die Summen-Logik. `StundenView` nutzt sie
  als `ItemsSource`. So überlebt die Stunden-Liste auch ein evtl. Recyceln der
  View durch die CarouselView.
- `TimeEntry` wird aus `MainPage` nach `Models/TimeEntry.cs` gezogen
  (eigener Typ, vom Singleton nutzbar).
- Flug-Session, Logbuch und Einstellungen sind bereits datei-gestützt und damit
  ohnehin gegen Recyceln robust.

## Datenfluss

```
Wischen / Tab-Tipp ──► CarouselView.Position ──► PositionChanged
   ──► alte View.OnDeselected() (Timer stop / persist)
   ──► neue View.OnSelected()   (Timer start / reload / restore)

FlugView-Aktionen ──► FlightSession ──► FlightSessionStore (session.json)
LogbuchView Detail ──► Shell.Current.GoToAsync(nameof(FlightDetailPage), {flight})
StundenView ──► StundenStore (Singleton, In-Memory)
```

## Fehlerbehandlung

- Lifecycle-Aufrufe sind idempotent abgesichert (Timer doppelt-stop/-start
  unkritisch; Restore-Flag verhindert Mehrfach-Restore).
- Persistenz der Flug-Session bleibt best-effort (try/catch, UI nie blockiert).
- Export/Import-Fehlerpfade unverändert (Nutzer-Hinweis, kein Datenverlust).

## Betroffene/neue Dateien

| Datei | Art |
|-------|-----|
| `MainTabsPage.xaml` / `MainTabsPage.xaml.cs` | neu (Host) |
| `Views/ITabView.cs` | neu (Interface) |
| `Views/StundenView.xaml` / `.xaml.cs` | neu (aus MainPage) |
| `Views/FlugView.xaml` / `.xaml.cs` | neu (aus FlightPage) |
| `Views/LogbuchView.xaml` / `.xaml.cs` | neu (aus LogbookPage) |
| `Views/EinstellungenView.xaml` / `.xaml.cs` | neu (aus SettingsPage) |
| `Services/StundenStore.cs` | neu (Singleton) |
| `Models/TimeEntry.cs` | neu (aus MainPage gezogen) |
| `AppShell.xaml` / `AppShell.xaml.cs` | ändern (TabBar → eine ShellContent) |
| `MainPage.*`, `FlightPage.*`, `LogbookPage.*`, `SettingsPage.*` | entfernen |
| `FlightDetailPage.*` | unverändert |
| `Tests/StundenStoreTests.cs` | neu (Summen-Aggregation) |

## Tests

- Bestehende Service-Tests bleiben gültig (UI-Refactor berührt sie nicht).
- Neu: `StundenStore` — Aggregation der Summe (z. B. 90 Min → 1:30; >24h korrekt;
  Hinzufügen/Entfernen/Leeren).
- UI/Swipe wird per App-Build (`dotnet build … -f net10.0-windows10.0.19041.0`)
  und manuellem Test auf Android/iOS verifiziert:
  1. Wischen durch alle vier Seiten (beide Richtungen), flüssig.
  2. Tab-Tippen wechselt zur richtigen Seite, Hervorhebung stimmt.
  3. Flug-Session bleibt beim Hin-/Herwischen erhalten; Uhr läuft nur auf
     sichtbarer Seite.
  4. Stunden-Liste bleibt beim Wischen erhalten.
  5. Logbuch-Detail öffnen und zurück funktioniert; danach Position erhalten.
