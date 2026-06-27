# Session-Persistenz & Einstellungen (Export/Import) — Design

**Datum:** 2026-06-27
**Status:** Genehmigt

## Ziel

Zwei Erweiterungen für den Flugzeit-Logger (.NET MAUI, Ziel: Android + iOS):

1. **Laufende Flugsession übersteht App-Neustart.** Der noch nicht ins Logbuch
   gespeicherte Flug (Off-Block, Starts/Landungen, Kennzeichen) bleibt erhalten,
   wenn die App beendet und neu geöffnet wird.
2. **Einstellungen-Seite mit Export/Import des Logbuchs.** Der Nutzer kann die
   Logbuch-Datei sichern/weitergeben (Export) und wieder einspielen (Import).

**Nicht im Umfang:** Persistenz der Stunden-Liste (`MainPage`); freie Wahl eines
dauerhaften Ziel-Ordners (auf iOS/Android unzuverlässig). Backup erfolgt über
Export/Import per System-Dialog.

## Kontext (Ist-Zustand)

- Abgeschlossene Flüge werden bereits dauerhaft in `flights.json`
  (`FileSystem.AppDataDirectory`) über `FlightLogService` gespeichert.
- Die laufende Session lebt nur im Speicher (`FlightSession` in der `FlightPage`-
  Instanz) und geht beim Beenden verloren.
- Der Logbuch-Pfad ist aktuell an zwei Stellen hartkodiert
  (`FlightPage.xaml.cs`, `LogbookPage.xaml.cs`).
- Abhängigkeiten: nur `Microsoft.Maui.Controls`. `Share` und `FilePicker` sind in
  MAUI eingebaut — **keine neuen NuGet-Pakete nötig**.

## Teil 1 — Session-Persistenz

### Ansatz

Auto-Save in eigene Datei `session.json` (interner App-Ordner). Nach **jeder**
mutierenden Aktion wird die laufende Session geschrieben. Grund: Android/iOS
können Apps hart beenden, ohne dass „beim Schließen speichern"-Code noch läuft —
deshalb nicht erst beim Beenden, sondern nach jeder Aktion speichern.

### Komponenten

**Neuer Service `Services/FlightSessionStore.cs`** (analog zu `FlightLogService`):
- `FlightSessionStore(string filePath)`
- `Task SaveAsync(Flight flight)` — serialisiert die Session nach `session.json`.
- `Task<Flight?> LoadAsync()` — liest die Session, `null` wenn keine Datei.
- `Task ClearAsync()` — löscht `session.json` (idempotent, kein Fehler wenn
  Datei fehlt).

**`Services/FlightSession.cs`** — neue Methode:
- `public void Restore(Flight flight)` — übernimmt eine geladene Session als
  aktuelle `Flight`.

**`FlightPage.xaml.cs`** — Verdrahtung:
- Hält eine `FlightSessionStore`-Instanz (Pfad aus `AppPaths.SessionPath`).
- **Wiederherstellung:** Beim ersten `OnAppearing` (Guard `_restored`) wird eine
  vorhandene Session geladen und per `Restore` übernommen, danach `RefreshState`.
  Folge-Aufrufe von `OnAppearing` (Tab-Wechsel) stellen nicht erneut wieder her.
- **Speichern:** Eine Hilfsmethode `PersistAsync()` schreibt die aktuelle Session.
  Aufruf nach jeder Aktion: Off-Block, Start, Landung, On-Block, Undo. Kennzeichen
  wird bei `Completed` (Unfocus) gespeichert, nicht pro Tastendruck.
- **Löschen:** Bei „Flug speichern" (nach erfolgreichem `AddAsync`) und bei
  „Zurücksetzen" wird `ClearAsync()` aufgerufen.
- **Fehlerbehandlung:** `PersistAsync` ist best-effort — Schreibfehler werden
  abgefangen (Debug-Log), die UI funktioniert weiter.

## Teil 2 — Einstellungen-Seite (Export/Import)

### Komponenten

**Neue Seite `SettingsPage.xaml` / `SettingsPage.xaml.cs`**, als 4. Tab
„Einstellungen" in `AppShell.xaml`.

Inhalt:
- **Speicherort-Anzeige:** schreibgeschütztes Label mit dem internen Pfad der
  `flights.json` plus Anzahl der Flüge im Logbuch (Transparenz, wo die Daten
  liegen).
- **Export-Button:** `Share.Default.RequestAsync(new ShareFileRequest(...))` mit
  `flights.json`. Öffnet das System-Teilen-Menü → speichern in Dateien/Cloud oder
  versenden. Fehler → `DisplayAlert`.
- **Import-Button:** `FilePicker.Default.PickAsync()` → Datei einlesen →
  `List<Flight>` deserialisieren → **zusammenführen** mit dem Logbuch. Ergebnis
  als Hinweis („X Flüge importiert, Y übersprungen"). Ungültige/nicht
  deserialisierbare Datei → Fehlermeldung, Logbuch bleibt unverändert.

**`Services/FlightLogService.cs`** — Erweiterungen:
- `public string FilePath { get; }` — Pfad für die Anzeige.
- `public async Task<int> MergeAsync(IEnumerable<Flight> incoming)` — fügt nur
  Flüge hinzu, die nicht bereits existieren (Schlüssel: `Date` + `Registration` +
  `OffBlock`, identisch zur Logik in `DeleteAsync`). Rückgabe: Anzahl tatsächlich
  hinzugefügter Flüge. Speichert anschließend.

**Neue Datei `Services/AppPaths.cs`** — zentrale Pfade:
- `static string FlightLogPath` → `Path.Combine(FileSystem.AppDataDirectory, "flights.json")`
- `static string SessionPath` → `Path.Combine(FileSystem.AppDataDirectory, "session.json")`
- `FlightPage` und `LogbookPage` nutzen künftig `AppPaths.FlightLogPath` statt der
  hartkodierten Strings.

## Datenfluss

```
Aktion auf FlightPage ──► FlightSession (Speicher) ──► FlightSessionStore.SaveAsync ──► session.json
App-Start / FlightPage erstes Anzeigen ──► FlightSessionStore.LoadAsync ──► FlightSession.Restore ──► UI
"Flug speichern" ──► FlightLogService.AddAsync ──► flights.json ; FlightSessionStore.ClearAsync
Export ──► Share(flights.json) ──► System-Teilen-Menü
Import ──► FilePicker ──► Deserialize ──► FlightLogService.MergeAsync ──► flights.json
```

## Fehlerbehandlung

- **Session-Speichern:** best-effort, Ausnahmen abfangen, UI nicht blockieren.
- **Session-Laden:** beschädigte `session.json` → wie „keine Session" behandeln
  (`null`), App startet sauber.
- **Import:** Deserialisierungs-/Lesefehler → Nutzer-Hinweis, kein Datenverlust.
- **Export:** Ausnahme (z. B. Abbruch) → Nutzer-Hinweis bzw. stilles Ignorieren
  bei Abbruch.

## Tests (xUnit, im vorhandenen Tests-Projekt)

- `FlightSessionStore`: Save→Load Roundtrip; `ClearAsync` entfernt Datei;
  `LoadAsync` ohne Datei liefert `null`; beschädigte Datei liefert `null`.
- `FlightSession.Restore` übernimmt die übergebene Session korrekt.
- `FlightLogService.MergeAsync`: ergänzt nur neue Flüge, überspringt Dubletten,
  liefert korrekte Anzahl.

## Betroffene/neue Dateien

| Datei | Art |
|-------|-----|
| `Services/AppPaths.cs` | neu |
| `Services/FlightSessionStore.cs` | neu |
| `SettingsPage.xaml` / `SettingsPage.xaml.cs` | neu |
| `Services/FlightSession.cs` | `Restore` ergänzen |
| `Services/FlightLogService.cs` | `FilePath`, `MergeAsync` ergänzen |
| `FlightPage.xaml.cs` | Persistenz verdrahten, `AppPaths` nutzen |
| `LogbookPage.xaml.cs` | `AppPaths` nutzen |
| `AppShell.xaml` | Einstellungen-Tab |
| `Tests/*` | neue Tests |
