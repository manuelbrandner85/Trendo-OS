# Vorbild-Analyse Schutz: Windows-Sicherheit, Dateiversionsverlauf/Time Machine, TCPView, Bitwarden-Bericht (Clean-Room)

- Programme: „Schutz & Datenschutz“, „Freigaben“, „Sicherung“, „Sicherheits-Werkzeuge & Beweise“, „Selbstheilung“
- Vorbilder: Windows-Sicherheit (Übersicht mit Ampel), Windows-Dateiversionsverlauf und macOS Time Machine, Sysinternals TCPView, Bitwarden „Tresor-Bericht“, Windows-Problembehandlung – Funktionsumfang aus öffentlicher Doku
- Werkzeug: Funktionsumfang aus öffentlicher Doku und Bedienkonzept; kein fremder Code, keine fremden Grafiken oder Texte. Trendo-Stand aus Code und Tests, Stand 05.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

### Schutz & Datenschutz
- ✓ Schutz-Wert von 100 mit Hinweisen, was fehlt (wie die Übersicht der Windows-Sicherheit)
- ✓ Prüfung „Liegen Passwörter offen herum?“ (wie der Tresor-Bericht von Passwort-Managern)
- ✓ Geheimnisse nur im Windows-Schlüsselspeicher, nie in Dateien

### Freigaben
- ✓ Was auf deine Entscheidung wartet (Installationen, sensible Aktionen) – nichts passiert ohne Ja; jede Entscheidung im Protokoll

### Sicherung
- ✓ „Jetzt sichern“, Zeitleiste der Sicherungen, „Alles wiederherstellen“ (wie Time Machine)
- ◐ Einzelne Datei aus einer älteren Sicherung zurückholen – nur ganze Wiederherstellung

### Sicherheits-Werkzeuge & Beweise
- ✓ Datei-Prüfsumme, „wer lauscht auf diesem PC“ (offene Verbindungen wie TCPView), Beweis-Fälle nur lokal und höchstens 120 Stunden (feste Projektregel, Warnung 24 h vorher)

### Selbstheilung
- ✓ Trendo prüft sich beim Start und repariert sichere Fälle ohne Nachfrage (wie die Windows-Problembehandlung, aber nur für Trendo selbst)

## Bedienkonzept (Profi-Bauplan A1–A3)
Gruppe „Datenschutz & Sicherheit“ in den Einstellungen, je Seite ein klarer Hauptknopf.

## Nächste Schritte
1. Einzelne Dateien aus einer Sicherung zurückholen
