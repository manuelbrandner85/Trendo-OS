# Bericht T-0612: kie.ai in jedem Studio

**Zweig:** `claude/T-0612-kie-ai` (Worktree `studios-aufraeumen`), auf `claude/T-0611-kreativ-ki-2`. Nicht auf `main`.

## Inhalt

- **Kern** `src/Trendo.Core/Kie/Kie.cs`: KieClient (Kontostand, Auftrag, Abfrage, Upload Base64/Mehrteil), Fehlercodes trotz HTTP 200, Wiederholen bei kurzen Störungen, kuratierte Modellliste, Regeln, Richtpreise, Dateiart- und Adressprüfung, Herkunftsnachweis.
- **Modelle** (nur mit belegter Anfrageform): Nano Banana, Nano Banana Bearbeiten, Recraft Hintergrund entfernen, Topaz Hochskalieren, Kling 3.0 Video, Kling Avatar, Suno V6, ElevenLabs Sprachausgabe. Bewusst nicht: Midjourney, Chat-Modelle (Claude nur über die Anmeldung der Person).
- **App** `MainWindow.Kie.cs`: Knopf „kie.ai“ in jeder Studio-Leiste, Klappfeld mit den passenden Arten je Studio, Schlüssel je Person im verschlüsselten Speicher (Prüfung vor dem Speichern, Trennen mit Rückfrage), Kosten vor dem Start, Start erst nach Bestätigung (Doppelklick zählt nicht), bei unbekanntem Preis zusätzliches Häkchen, Ergebnis sofort lokal mit Nachweis, „Ergebnis holen“ ohne neue Kosten nach Abbruch, Modellliste mit der Übersicht von kie.ai abgleichen.
- **Claude Code** `MainWindow.KieClaude.cs`: Werkzeuge kie_status, kie_erzeugen, kie_ergebnis über Trendos eigene Brücke (statt des fremden felores-Pakets aus der Freigabe-Beschreibung): Claude sieht den Schlüssel nie; die Rückfrage im Chat nennt Kosten, Beschreibung, Liedtext und die vollen Pfade der hochgeladenen Dateien; lange Aufträge laufen in Trendo weiter.

## Recherche-Grundlage

Offizielle Doku (docs.kie.ai) blockiert automatische Abrufe. Anfrageformen aus den Kopien der offiziellen Beschreibungen im MIT-lizenzierten Projekt felores/kie-ai-mcp-server (Stand 04.10.2026) gelesen, nichts übernommen.

## Unabhängige Prüfung

Kein Schlüssel-Abfluss, kein Start ohne Ja. Behoben: Fehlsperren der Nachahmungs-Regel bei normalen deutschen Sätzen, Längengrenze 1500 für Liedtexte, fehlende Preise vor dem Auftrag, Rückfrage ohne Pfade/Liedtext, nicht gefangene Fehler beim Speichern des Schlüssels, Abbruch eines bezahlten Auftrags bei kurzem Netzfehler, Doppelklick-Start, Neuaufbau beim Tippen, gleiche Dateinamen, Download ohne Inhalts- und Adressprüfung, Trennen ohne Rückfrage, minutenlang offene Claude-Aufrufe.

## Testergebnis

Volltest, Exitcode 0: Api 16; Core 2235 bestanden, 5 übersprungen; App 1223 bestanden, 4 übersprungen; Paket-Tests 11 OK. Auszug in `reports/volltest-T-0612/`. Vorher ein Lauf mit 2 Fehlern (Netz-Paket im Kern – QA-019; Klickprüfung erkannte das Klappfeld nicht) – behoben.

## Nicht geprüft / offen

- Kein Auftrag mit echtem kie.ai-Konto (braucht den Schlüssel der Person).
- S2 eigene Stimme (Suno-Verifikationssatz): Schnittstelle nicht belegt – nicht gebaut.
- Veo 3.1 und Soundeffekte: Quellen widersprüchlich bzw. Google-Vorschau abgeschaltet – nicht gebaut.
- Richtpreise nur für Nano Banana, Recraft, Kling 3.0, ElevenLabs; sonst „unbekannt“ mit Zusatzbestätigung.

## Status

Auf dem Zweig, nicht auf `main`. Keine Abnahme durch eine Person.
