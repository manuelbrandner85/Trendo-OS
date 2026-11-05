# Vorbild-Analyse Erweitern: VS Code Erweiterungen, GitHub Desktop, Pinokio, Obsidian-Plugins (Clean-Room)

- Programme: „Erweiterungen“, „Open Source“, „Sauberer Nachbau“
- Vorbilder: VS Code Marketplace (Erweiterungen mit An/Aus und Detailseite), Obsidian Community Plugins, Pinokio (Projekte mit einem Klick starten), GitHub Desktop (Projekte holen) – Funktionsumfang aus öffentlicher Doku
- Werkzeug: Funktionsumfang aus öffentlicher Doku und Bedienkonzept; kein fremder Code, keine fremden Grafiken oder Texte. Trendo-Stand aus Code und Tests, Stand 05.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

### Erweiterungen
- ✓ Wie in VS Code: links die Liste (installiert, mit An/Aus), rechts die Detailseite
- ✓ Erweiterungen sind nur Daten (Vorlagen, Schrittlisten, Links, Textbausteine) – kein fremder Code läuft
- ✓ Katalog lokal oder von GitHub; erst fragen (Name, Version, Herkunft, Prüfsumme), dann laden, Prüfsumme kontrollieren, mit Freigabe installieren; Entfernen nach Rückfrage
- ✓ Jede Weiterleitung wird einzeln geprüft (nur GitHub über HTTPS)

### Open Source (GitHub)
- ✓ Projekte suchen; Treffer zeigt sofort „Windows-Programm“ oder „nur Quellcode“
- ✓ Auswählen = laden, einrichten und starten (wie Pinokio); Beschreibung aus der README, auf Wunsch von der KI auf Deutsch erklärt
- ✓ Quellcode-Projekte mit offizieller Laufzeit einrichten; Alternativen (Forks mit Windows-Programm, PyPI/npm-Paket)
- ✓ Updates für installierte GitHub-Programme – die alte Version bleibt, bis die neue fertig ist
- ✓ Optionaler GitHub-Schlüssel nur für die Suche, im Windows-Schlüsselspeicher

### Sauberer Nachbau
- ✓ Projekt-Arbeitsfläche mit Regeln und den drei Schritten Beschreibung → Umsetzung → Prüfung (Clean-Room)
- ✓ Automatischer Ähnlichkeitsvergleich mit dem Original (Wortfolgen-Abgleich), Treffer über der Schwelle werden gemeldet; Tagebuch je Rolle

## Bedienkonzept (Profi-Bauplan A1–A3)
Liste links, Detail rechts – wie VS Code; ein Hauptknopf je Eintrag.

## Nächste Schritte
1. Ähnlichkeitsvergleich auch für Texte und Grafiken, nicht nur Code
