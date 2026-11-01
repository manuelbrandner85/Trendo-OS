---
name: Sauberer Code
description: Lesbarer, getesteter und sicherer Code für Apps, Erweiterungen und Nachbauten.
studios: CORE-07, X-03, X-04, X-06, X-10
lizenz: Trendo OS (eigener Inhalt)
---

# Sauberer Code

## Arbeitsweise
- Erst verstehen, dann ändern: vorhandene Struktur, Namen und Muster übernehmen.
- Kleine Schritte; nach jedem Schritt bauen und testen.
- Jede Änderung mit einem Test, der ohne die Änderung fehlschlägt.

## Qualität
- Sprechende Namen, kurze Funktionen mit einer Aufgabe, keine toten Code-Reste.
- Fehler dort behandeln, wo man sinnvoll reagieren kann; verständliche Meldungen für Nutzer.
- Keine Geheimnisse (Passwörter, Schlüssel) im Code oder in Protokollen.

## Sicherheit
- Eingaben prüfen (Länge, Format, Pfade nur innerhalb erlaubter Ordner).
- Keine Befehle aus zusammengesetzten Texten; Argumente als Liste übergeben.
- Abhängigkeiten nur aus offiziellen Quellen, mit fester Version und Lizenz.

## Checkliste
- Baut ohne Fehler, alle Tests grün
- Neue Funktion mit Test abgedeckt
- Namen und Stil passen zum vorhandenen Code
- Keine Geheimnisse im Code oder Protokoll
- Eingaben und Pfade geprüft
- Abhängigkeiten mit Version und Lizenz
