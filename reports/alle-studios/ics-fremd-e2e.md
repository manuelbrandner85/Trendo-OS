# Fremde Kalenderdateien mit ical.net (Kalender) – 08.11.2026

Alle Studios, Runde 6 (SYS-09/P16). Eingesetzt wird die Bibliothek **Ical.Net 5.2.4** (MIT, mit NodaTime 3.2.2 unter Apache-2.0). Sie kommt als NuGet-Paket mit ins Setup. Es gibt kein Fremdprogramm.

## Warum
Trendos eigener Leser kennt nur „jeden Tag/Woche/Monat/Jahr, ohne Ende“. Bei fremden Dateien ging deshalb viel verloren:
- **Regeln mit Ende** (`COUNT`, `UNTIL`), **Intervalle** und **Wochentage**: Aus „10× jeden zweiten Dienstag“ wurde ein endloser Wochentermin. Aus „Mo und Mi“ wurde nur Montag.
- **Ausnahmen** (`EXDATE`) und **verschobene Einzeltermine** (`RECURRENCE-ID`): Sie wurden übersehen.
- **Zeitzonen** (`TZID`): Termine wurden als Ortszeit gelesen. Ein Termin um 9:00 in New York stand also um 9:00 statt um 15:00 bzw. 16:00 im Kalender.

## Wie es jetzt läuft
- Trendos eigene Dateien und einfache Dateien liest weiter der bisherige Leser. Er bleibt verlustfrei, das zeigen die alten Tests.
- Enthält eine Datei Regeln, Ausnahmen, Einzeländerungen oder Zeitzonen, übernimmt **ical.net**:
  - Einfache Reihen bleiben Reihen.
  - Alle anderen Reihen werden im Fenster *ein Jahr zurück bis zwei Jahre voraus* zu Einzelterminen aufgelöst. Ihre UID lautet „…#JJJJMMTTTHHMM“.
  - Es gilt eine Höchstgrenze von 5000 Terminen pro Datei.
  - Wird eine geänderte Reihe erneut eingelesen, ersetzt sie ihre alten Einzeltermine.
  - Alles wird in einem einzigen Speichervorgang geschrieben.
- **Oberfläche:** „ICS einlesen …“ meldet, was übernommen wurde. Bei einer kaputten oder fehlenden Datei erscheint eine verständliche Meldung. Bisher konnte eine kaputte Datei einen Absturz auslösen.

## Echter Lauf: Vergleich mit einer unabhängigen Umsetzung
- **Referenz:** Python **icalendar 7.3.0** + **recurring-ical-events 3.8.2**, eingerichtet in einem eigenen Prüfordner. Sie dienen nur als Prüfwerkzeug und werden nicht mitgeliefert.
- **Ablauf:** Beide lösen dieselben Dateien im Fenster 08.11.2025–08.11.2028 in die Ortszeit dieses PCs auf. Danach wird Termin für Termin verglichen (Beginn und Titel).

| Datei | Inhalt | Termine | Ergebnis |
|---|---|---|---|
| `GermanHolidays.ics` (echte Datei von thunderbird.net, SHA-256 daaa6200…ae44) | 58 Feiertage, ganztägig | 41 im Fenster | gleich |
| `outlook.ics` (Aufbau wie Outlook 16) | eigene `VTIMEZONE` „W. Europe Standard Time“, Mo+Mi bis `UNTIL`, zwei `EXDATE`, ein verschobener Termin | 40 | gleich |
| `google.ics` (Aufbau wie Google Kalender) | „jeder 2. Donnerstag“ in America/New_York ×12, UTC-Termin, werktags täglich in Europe/Berlin ohne Ende | 540 | gleich |
| `apple.ics` (Aufbau wie macOS-Kalender) | Geburtstag jährlich (bleibt Reihe), „Monatsletzter“ ×8 | 11 | gleich |

**Gegenprobe:** Ein absichtlich verfälschter Referenzeintrag macht den Test rot. Die Meldung nennt den Unterschied genau („Nur Referenz … / Nur Trendo …“).

Stichproben aus der Referenz:
- Der 18.11.2026 ist auf 11:00 verschoben.
- Am 23.12. und 28.12. findet kein Termin statt.
- Der letzte Termin ist am 31.03.2027 um 08:30. Das `UNTIL` 07:30 UTC liegt in der Sommerzeit.
- Der Monatscall USA liegt alle 12 Mal um 16:00 Ortszeit, also 10:00 in New York. Keiner der Termine fällt in die zwei Wochen im März, in denen die USA schon Sommerzeit haben und Deutschland noch nicht.

## Tests
- **`IcsForeignTests`** (10 Tests, grün):
  - Weiche zwischen den beiden Lesern, auch für gefaltete Zeilen
  - COUNT + INTERVAL + BYDAY
  - EXDATE und RECURRENCE-ID
  - Zeitzone New York
  - UNTIL und Zeitfenster
  - ganztägig, Sonderzeichen, Erinnerung, DURATION
  - einfache Reihe bleibt Reihe
  - erneutes Einlesen ersetzt die alten Einzeltermine
  - kaputte Datei
  - Höchstgrenze bei minütlicher Regel
- **`IcsForeignReferenzTests`** (1 Echtlauf, grün): Er findet den Prüfordner automatisch (`EchtWerkzeuge.IcsReferenz`). Fehlt der Ordner, wird der Test übersprungen.
- **`IcsForeignUi`** (1 Oberflächentest, grün).
- Die bisherigen Kalendertests (`PimTests`, `Sys02Tests`, Kalenderraster, PIM-Oberfläche) bleiben grün.

## Grenzen / nicht geprüft (NOT RUN)
- Echte Exporte aus einem Outlook-, Google- oder Apple-Konto. Die Dateien hier sind nach deren Aufbau geschrieben. Echt heruntergeladen ist nur die Feiertagsdatei von Thunderbird.
- Termine außerhalb des Fensters (älter als ein Jahr oder mehr als zwei Jahre voraus) werden bei aufgelösten Reihen nicht übernommen.
- Der Export schreibt weiter Trendos einfaches Format. Aufgelöste Reihen gehen also als Einzeltermine hinaus, nicht als Regel.
- Kalender-Abos über das Netz (CalDAV, webcal://) gibt es nicht.
