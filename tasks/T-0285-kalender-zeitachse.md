# T-0285 – Kalender: Zeitachse mit Ziehen, Bearbeiten-Feld, Rückgängig

- Freigabe: Manuel im Chat am 31.10.2026 („Erst Programme umbauen“).
- Vorher: Woche und Tag waren Listen je Tag, Termine nur über ein Formular anlegbar, Ändern nur durch Löschen und neu Eintragen.
- Jetzt (`CalTimeGrid`):
  - Woche und Tag als Zeitachse mit Stundenraster, Wochenende abgesetzt, heute hervorgehoben, rote Linie für „jetzt“; Überschneidungen nebeneinander; ganztägige Termine in der Kopfzeile.
  - In freie Zeit ziehen legt einen Termin an (15-Minuten-Raster), Termin ziehen verschiebt ihn – auch auf andere Tage –, Unterkante ziehen ändert die Dauer.
  - Monat: Termin auf einen anderen Tag ziehen.
  - Klick wählt einen Termin, rechts das Bearbeiten-Feld (Titel, Datum, Beginn, Ende, ganztägig, Ort, Erinnerung, Wiederholung, Notizen, Löschen). Ohne Auswahl steht dort wie bisher „Termin eintragen“.
  - Rückgängig (Knopf und Strg+Z) für Anlegen, Verschieben, Dauer, Bearbeiten und Löschen.
- Kern: `Calendar.Get`, `Calendar.Snap` (15 min), `Calendar.Shift` (Dauer bleibt; bei Wiederholung die ganze Reihe), `Calendar.SetEnd` (mindestens 15 min).
- **Fehler behoben (7 Stellen):** Knöpfe mit `Background = … : null` waren nur auf der Schrift anklickbar (Kalender-Navigation ◀ Heute ▶ Tag/Woche/Monat, Systemreiter, KI-Rollen, Vorlagen, Entfernen-Knöpfe, 3D-Druck „Verbinden“) – jetzt durchsichtiger Hintergrund, gleiche Optik, ganze Fläche klickbar. Gefunden, weil der neue Test echt klickt.
- Tests: neu `CalendarGrid_Ui` (Mausziehen anlegen/verschieben/Dauer, Bearbeiten, Monat ziehen, Löschen, Rückgängig per Knopf und Strg+Z; Kern Raster/Verschieben/Ende); bestehende Kalender-/Büro-Tests grün.
