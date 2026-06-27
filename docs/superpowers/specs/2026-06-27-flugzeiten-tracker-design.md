# Flugzeiten-Tracker – Design

**Datum:** 2026-06-27
**Status:** Genehmigt (zur Implementierung)

## Ziel

Die bestehende MAUI-App (Uhrzeitrechner mit Stundenrechner) wird um einen
Flugzeiten-Tracker erweitert. Damit lassen sich Flüge mit Off-Block-, Start-,
Landungs- und On-Block-Zeiten erfassen. Mehrere Start/Landung-Paare pro Flug
sind möglich (max. 10–12). Blockzeit und Flugzeit werden automatisch berechnet.
Abgeschlossene Flüge werden in einem Logbuch gespeichert.

## Architektur

**Ansatz: Code-Behind wie bisher (kein MVVM).** Die neuen Seiten folgen exakt
dem Muster von `MainPage` (Event-Handler, `ObservableCollection`, keine
ViewModels/Commands), damit der Code einheitlich und schlicht bleibt.

Speicherung als eine JSON-Datei im App-Datenverzeichnis über `System.Text.Json`.
Eine eigene Service-Klasse kapselt das Laden/Speichern/Löschen, getrennt von der
UI.

## Navigation & Seitenstruktur

`AppShell` wird auf eine untere **TabBar** mit drei Tabs umgestellt:

- **Stunden** – die bestehende `MainPage` (unverändert)
- **Flug** – neue `FlightPage` (aktiver Flug-Tracker)
- **Logbuch** – neue `LogbookPage` (Liste gespeicherter Flüge)

`FlightDetailPage` ist eine Unterseite, die aus dem Logbuch heraus geöffnet wird
(Shell-Navigation per Route).

## Datenmodell

```csharp
public class Flight
{
    public DateTime Date { get; set; }          // automatisch = Datum bei Off-Block
    public string Registration { get; set; }     // manuell eingegeben (Kennzeichen)
    public DateTime? OffBlock { get; set; }      // UTC
    public DateTime? OnBlock { get; set; }       // UTC
    public List<Leg> Legs { get; set; } = new(); // Start/Landung-Paare

    // Berechnet (nicht gespeichert):
    // BlockTime = OnBlock - OffBlock
    // FlightTime = Summe aller (Leg.Landing - Leg.Takeoff)
}

public class Leg
{
    public DateTime? Takeoff { get; set; }   // UTC
    public DateTime? Landing { get; set; }   // UTC
}
```

- Alle erfassten Zeiten sind **UTC**.
- **Datum** wird automatisch beim Off-Block auf das aktuelle Datum gesetzt.
- **Kennzeichen** wird vom Nutzer eingegeben.
- Block-/Flugzeit werden **berechnet**, nicht persistiert.

## FlightPage (Tab „Flug")

### Kopfbereich
- Aktuelle **UTC-Zeit** groß, live laufend (1-Sekunden-Timer wie im
  Stundenrechner), darunter Lokalzeit klein.
- Eingabefeld **Kennzeichen** (manuell). Datum wird automatisch gesetzt und nur
  angezeigt.

### Erfassungs-Buttons (State Machine)
Jeder Button erfasst beim Tippen die aktuelle UTC-Zeit als Snapshot
(**nicht editierbar**). Es ist immer nur der sinnvolle nächste Schritt aktiv:

1. **Off-Block** – am Anfang aktiv; setzt OffBlock + Datum. Danach deaktiviert.
2. **Start** – nach Off-Block aktiv; legt neues Leg mit Takeoff an.
3. **Landung** – nach einem Start (offenes Leg) aktiv; setzt Landing im offenen Leg.
4. Nach einer Landung sind **Start** (nächstes Paar) **und** **On-Block** aktiv.
5. **On-Block** – nach mind. einer abgeschlossenen Landung aktiv; setzt OnBlock,
   beendet den Flug. Danach sind die Erfassungs-Buttons deaktiviert.

Begrenzung: max. 12 Legs. Ist das Maximum erreicht, wird **Start** deaktiviert.

### Fehlbedienung
- **„Letzte Erfassung rückgängig"**-Button: entfernt den zuletzt erfassten
  Zeitpunkt (On-Block → letzte Landung → letzter Start → Off-Block) und stellt
  den vorherigen Button-Zustand wieder her. Da Zeiten nicht editierbar sind, ist
  dies die Korrekturmöglichkeit bei Fehlklicks.

### Liste der Start/Landung-Paare
- Untereinander, z. B. „Start 1: 12:34:05 / Landung 1: 12:51:20".
- Offenes Leg (Start ohne Landung) zeigt Landung als „—".

### Live-Berechnung (unten)
- **Blockzeit** = OnBlock − OffBlock. Solange OnBlock noch nicht erfasst ist,
  wird „—" angezeigt (keine laufende Hochzählung), analog zur Flugzeit, die nur
  abgeschlossene Legs zählt.
- **Flugzeit** = Summe aller (Landing − Takeoff) abgeschlossener Legs.
- Format wie Stundenrechner: `H:MM` (auch > 24 h korrekt).

### Aktionen
- **„Flug speichern"**: nur aktiv, wenn Flug vollständig (Off-Block, mind. ein
  abgeschlossenes Leg, On-Block) und Kennzeichen ausgefüllt. Speichert ins
  Logbuch, setzt danach für einen neuen Flug zurück.
- **„Zurücksetzen"**: verwirft den aktuellen Flug ohne zu speichern.

## LogbookPage (Tab „Logbuch")

- `CollectionView` aller gespeicherten Flüge, **neueste oben**.
- Pro Eintrag eine kompakte Zeile: **Datum · Kennzeichen · Blockzeit · Flugzeit**.
- Tippen öffnet `FlightDetailPage`.
- Löschen eines Flugs per Button/Swipe.
- Leerzustand: Hinweistext „Noch keine Flüge gespeichert".

## FlightDetailPage

- Nur-Anzeige aller Daten eines Flugs: Datum, Kennzeichen, Off-Block, alle
  Start/Landung-Paare (durchnummeriert), On-Block, Blockzeit, Flugzeit.

## FlightLogService

Kapselt die Persistenz, getrennt von der UI:

- `Task<List<Flight>> LoadAsync()`
- `Task SaveAsync(Flight flight)` (hängt an und speichert die Gesamtliste)
- `Task DeleteAsync(Flight flight)`
- Speicherort: `Path.Combine(FileSystem.AppDataDirectory, "flights.json")`
- Serialisierung: `System.Text.Json`.

## Design / Stil

Durchgängig schlicht wie der Stundenrechner:
- Gleiche Schriftgrößen und Gewichtungen (Labels grau/klein, Werte fett).
- Akzentfarben `#512BD4` (Primary) und DodgerBlue für Summen/Hervorhebungen.
- `BoxView`-Trenner (1 px, LightGray) zwischen Bereichen.
- Gleiche Button-Optik wie bestehend.
- `Padding="20"`, konsistente Spacings (10–20).

## Nicht im Scope (YAGNI)

- Bearbeiten erfasster Zeiten (nur Rückgängig vorgesehen).
- Start-/Zielort, Bemerkungen, Flugzeugmuster.
- Export/Teilen des Logbuchs.
- Umschaltbare Zeitzonen (fix UTC, Lokalzeit nur als Anzeige).
- MVVM / Datenbank (SQLite).

## Hinweis

Das Projektverzeichnis ist kein Git-Repository, daher wird die Spec nicht
committet, sondern nur als Datei abgelegt.
