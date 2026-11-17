# Bericht T-0724 – Abnahme: Echtläufe, Installation, Update, Deinstallation

**Branch:** claude/T-0724-abnahme-laeufe (von claude/T-0685-b1-integration), eigene Kopie unter %TEMP%\trendo-auftrag-T-0724
**Status:** Läufe durchgeführt; Setup.exe-Weg BLOCKIERT (siehe unten). Kein Volltest (macht die Hauptsitzung).

## Geänderte/neue Dateien
- `tools/echtlauf/Trendo.Echtlauf/Program.cs` – neuer Fachlauf `unreal` (Projekt über GameDesign.BuildUnreal, Build.bat, Unreal-Tests, Startlauf; Zeitgrenzen 30/15/15 min)
- `tools/echtlauf/echtlauf.py` – Unreal von „blockiert“ auf `helfer-langsam` umgestellt (läuft mit `--langsam`)
- `scripts/setup-rundlauf-portabel.ps1` (neu) – abgeschotteter Rundlauf mit dem portablen Paket, Vorher/Nachher-Vergleich des echten Systems
- `tests/Trendo.App.Tests/LeistungT0724UiTests.cs` (neu) – Öffnen von 100 000 Zellen und 40 000 Dreiecken über die Oberfläche, misst nur (kein Zeitziel)
- `reports/echtlauf/2026-11-16/*` (aktualisiert), `reports/T-0724/**` (neu), `tasks/T-0724-*`

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler
- `python tools/echtlauf/echtlauf.py --langsam` → 16× grün/teilweise, Claude ROT (Abo-Limit erreicht); danach `--nur claude` → ECHT-GRÜN. Gesamt: 15 ECHT-GRÜN, 2 ECHT-GRÜN (teilweise: Chrome ohne pdfinfo, Tesseract ohne pdftoppm), 0 ROT, 0 BLOCKIERT
- Unreal (Teil der Station): Kompilieren Exit 0 „Result: Succeeded“ (85,9 s), Unreal-Tests 3/3, Startlauf TRENDO-OK
- `powershell -ExecutionPolicy Bypass -File scripts/setup-rundlauf-portabel.ps1 -Beleg reports/T-0724/setup` → Exit 0, ALLE BESTANDEN (20 PASS, 0 FAIL)
- `Trendo.App.Tests.exe -class Trendo.App.Tests.LeistungT0724Ui` → 3 Läufe, je Total 2, Failed 0
Zahlen und Tabellen: `reports/T-0724/README.md`.

## Was echt geprüft ist
Installation (portables Paket), Start mit Selbsttest, Update über Velopacks Update.exe mit Datenerhalt, Entfernen, Unberührtheit von Registry/Startmenü/Desktop/echtem Datenordner; Unreal-C++-Spiel aus Trendo kompiliert, getestet, gestartet; alle Werkzeuge der Station mit echtem Werkzeug; Startzeiten des installierten Pakets.

## Was NICHT geprüft wurde
- **Setup.exe (BLOCKIERT):** schreibt immer HKCU\…\Uninstall und startet die App danach mit dem echten Datenordner – nicht abschottbar ohne Eingriff ins echte System. Damit ungeprüft: Registry-Eintrag, Install-/Deinstallations-Hooks (Vollpaket merken, Datenfrage), Startwächter nach Setup, Rückfall (in scripts/setup-rundlauf.ps1 vorgesehen). Voraussetzung: eigener Windows-Testbenutzer oder VM.
- Deinstallation beim portablen Paket = Ordner löschen (es gibt dort keinen Deinstallations-Eintrag).
- Unreal: Open World, MetaHuman, Startlauf mit Grafik/Bildschirmfoto, BuildCookRun.
- Leistung im sichtbaren Fenster (gemessen headless); kein Vergleich mit Zielwerten (keine erfunden).
- Codesignatur, QA-017, QA-019: unverändert offen, nicht als bestanden markiert. requirements.yaml nicht geändert (SYS-13 umfasst 25 Funktionen; dieser Lauf belegt nur einen Teil).

## Abnahmekriterien (Brief)
1. Leere Vorlage → echtes Ergebnis: TEILWEISE (Unreal-Spiel aus Trendo-Vorlage bis zum laufenden Spiel; nicht über die sichtbare Oberfläche geklickt)
2. Fremddatei → Export im Fremdprogramm: TEILWEISE (über die Station: LibreOffice, Blender, Godot, Unity usw. ECHT-GRÜN; in diesem Auftrag nichts Neues)
3. Prozessabbruch ≤ 30 s: NICHT GETESTET (nicht Teil dieses Auftrags)
4. Claude/KI-Fachaufgabe im Dokument: NICHT GETESTET (Station prüft nur Claude-Antwort und lokale KI, nicht im Dokument)
5. Klick bis echte Datei: TEILWEISE (Setup-Rundlauf bis zur echten Installation/Update; große Dateien über die echte Oberfläche geöffnet)
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: NICHT GETESTET (nicht Teil dieses Auftrags)

## Offene Punkte / Risiken
- Setup.exe-Rundlauf nur in VM/Testbenutzer möglich.
- Kaltstart nach frischem Auspacken 11,5 s bis zum ersten Bild (warm 1,5–2 s) – Ursache nicht gemessen (vermutlich Virenscanner/Dateicache beim ersten Laden).
- 3D-Szene mit 40 000 Dreiecken: 2,2–3,0 s bis die Ansicht steht, davon 0,5–0,9 s Import – Rest Szenenaufbau, nicht weiter untersucht.
- Ollama-Antwort schwankt (frühere Station: „32“ statt 42) – Modellqualität, kein Fehler der Station.
