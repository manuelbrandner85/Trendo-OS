# Bericht T-0614: Etappe „Kreativ & KI“ – Teil 3

**Zweig:** `claude/T-0614-kreativ-ki-3`, Basis `origin/main` cce5c6261. Nicht auf `main`.

## Inhalt

- **Videos mit KI** (STUDIO-VIDEOAI-01, `video-ki-programm`): Szenenplan aus Text (eine Szene je Zeile oder Absatz; Dauer „5 s“, Bewegung „Zoom rein/raus, Schwenk nach links/rechts, ruhig“ und „Text: …“ werden erkannt), Längenprüfung bis 60 s inklusive Überblendungen, Standbilder mit der lokalen Bild-KI auf demselben Weg wie „Bilder mit KI“ (SDXL/SD Turbo, Herkunftsnachweis je Bild) oder eigenes Bild (wird in den Dokumentordner kopiert), Video mit FFmpeg (zoompan-Kamerafahrt auf doppelter Auflösung, xfade-Überblendung, Einblendung als ruhiges PNG-Overlay, `-n` und `-nostdin`, Zeitgrenze), Herkunftsnachweis des Videos (Quelle je Szene, Prüfsummen). Formate 16:9, 9:16, 1:1. Echte KI-Clips (Kling) über die vorhandene kie.ai-Leiste; `KieModels.ForStudio` ergänzt.
- **KI-Agenten bauen** (STUDIO-AGENTS-01, `agenten-bauen-programm`): Name, Aufgabe, Anweisungen, Werkzeuge aus fester Liste (Lesen, Suchen, Schreiben im Projekt, Internet, Befehle – Befehle nur mit Warnung, mindestens ein Werkzeug Pflicht), Beispiele, Grenzen. Prüfung (Kurzname, Pflichtfelder, Länge, Schlüssel/Passwörter wie sk-…, AKIA…, ghp_…, private Schlüssel). Export als Claude-Code-Subagent (YAML-Kopf name/description/tools, Rundlauf getestet), Einlesen vorhandener Agent-Dateien (fremde Werkzeuge werden gemeldet), Speichern nach Ordnerwahl unter `.claude/agents/<name>.md` ohne Überschreiben, Übernahme als eigene Rolle ins KI-Team (`AgentCenter.AddCustomRole`) und Export als Trendo-Rolle (JSON im Format `AgentRole`), Ausprobieren über `ClaudeFromStudioBar` – bei fremden Dateien erst nach zweitem Klick.
- **KI-Modelle vergleichen** (STUDIO-MODELS-01, `modelle-vergleichen-programm`): Quellen aus den eingerichteten KI-Quellen (`DefaultKiBackends`, also KI auf diesem PC, Claude über den eigenen Zugang, Internet-KIs mit eigenem Schlüssel), Frage über `IntelligenceCore.AskAsync` je Quelle, Dauer, Wörter, Wörter/s, Blindbewertung (Mischen mit festem Startwert, 1–5 Sterne, Aufdecken erst nach allen Sternen), Ergebnistabelle über alle Fragen (Schnitt, Siege, Dauer), Export Markdown/CSV. Fehlende Quelle → Hinweis, kein Absturz. Test-Übersteuerung über das vorhandene `KiBackendsOverride`.
- **Eigene KI trainieren** (STUDIO-TRAIN-01, `ki-trainieren-programm`): multinomialer Naive Bayes mit Laplace-Glättung, deutsche Wortzerlegung (Kleinschreibung, Umlaute, Bindestrich trennt, Füllwörter), Beispieltabelle, CSV-Import, feste Aufteilung je Kategorie mit Startwert, Genauigkeit, Verwechslungstabelle, Präzision/Trefferquote/F1, Warnung bei weniger als 5 Beispielen je Kategorie, Ausprobieren mit Sicherheit in %, Modell als JSON speichern/laden, Auswertung als Markdown. Ändern sich die Beispiele, gilt die alte Prüfung sichtbar nicht mehr.
- Je Studio Vorlagen „Leer“ und Beispiel mit neutralen Daten, Menüband, Exporte, Hilfe, Statuszeile, Suchwörter; Prompt-Adapter (Szenenplan, Agent-Datei, Fragebogen, trainiertes Modell) in `PromptActions.CreateDefault`; Katalog, Symbole, Profile mit Lieferformat, Teams, Wissen, Vorschläge, `requirements.yaml`, Programmkatalog und Fähigkeitsmatrix aktualisiert.

## Unabhängige Prüfung (Subagent, nur lesend)

Behoben:
- hoch: Bildordner hing an der nicht gespeicherten Dokument-Kennung – nach Wiederöffnen fehlten die Bilder. Jetzt feste Kennung `dir` im Dokument (nur 32 Hex-Zeichen). Dasselbe Muster besteht noch in Teil 2 (`ImgDir`, Bilder mit KI) – nicht angefasst.
- hoch: Präparierte Modelldatei (null-Einträge, überlaufende Zahlen) konnte die Oberfläche abstürzen lassen – jetzt InvalidDataException, Oberfläche fängt ab.
- mittel: Fremde Agent-Datei ging beim Ausprobieren direkt an Claude – jetzt Warnung und erst zweiter Klick.
- mittel: Bewegungswörter mitten im Wort („ruhiges“), Zahlen wie „1980s“ als Dauer – jetzt nur ganze Wörter.
- mittel: Beschädigte Listeneinträge verschoben Stellen (Sterne in falscher Runde) – Stellen bleiben jetzt erhalten.
- mittel: Umplanen während des Malens – Knöpfe gesperrt, Bild nur in die passende Szene, Quellen vor dem Bauen festgehalten.
- niedrig: Herkunftsnachweis des Videos wird nicht überschrieben; YAML-Wörter (yes/no …) als Name gesperrt, Steuerzeichen in der Aufgabe; leerer YAML-Kopf; Quellensuche parallel zur Frage; FFmpeg mit `-nostdin` und 15-Minuten-Grenze; „Passwort: niemals weitergeben“ gilt nicht mehr als Schlüssel.
Nicht umgesetzt: Zwischenspeicher der Trainings-Auswertung (Leistung bei sehr vielen Beispielen), Hinweis bei doppelt übernommener Rolle.

## Testergebnis (gezielt, kein Volltest)

- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- `dotnet test tests/Trendo.Core.Tests -c Release`: 2463 bestanden, 5 übersprungen, 0 Fehler (darin `KreativKits3Tests` 10 Tests inkl. echtem FFmpeg-Lauf, `PromptActionTests` inkl. Teil-3-Adaptertest, QA-015 mit 71 Modulen; Matrix mit `TRENDO_UPDATE_MATRIX=1` neu geschrieben).
- App-Testklassen einzeln (`Trendo.App.Tests.exe -class …`), jeweils 0 Fehler: KreativStudios3_Ui 5, KreativStudios2_Ui 5, PromptBarTests 6, StudioFolds_Ui 3, StudioIconTests 4, StudioBilder 1, ProgramCatalog_Ui 1, SimpleMode_Ui 5, Einfachheit_Ui 2, DeadButtonTests 73, StudioClickTests 3, ClaudeEverywhere_Ui 2.
- Programmkatalog mit `tools/Trendo.Render` neu erzeugt (113 Programme), nur `katalog.json`, `KATALOG.md` und die vier neuen Ordner behalten.
- Api-Tests, Paket-Tests und Volltest nicht gelaufen (macht die Hauptsitzung).

## Nicht geprüft

- Bild-KI mit echter Grafikkarte über die Oberfläche (im Test ersetzt); FFmpeg echt nur im Kern-Test (`VideoKi_Ffmpeg`, lief hier mit FFmpeg 9.0.2), in der Oberfläche ersetzt.
- Ausprobieren mit echtem Claude und Vergleich mit echten lokalen Modellen.
- Agent-Datei in einer echten Claude-Code-Sitzung.

## Status

Auf dem Zweig, nicht auf `main`. Keine Abnahme durch eine Person.
