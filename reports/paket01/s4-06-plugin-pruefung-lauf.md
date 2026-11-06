# Beleg S4-06 – Plugin-Prüfung ohne Laden (06.11.2026)

- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~PluginScanTests` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     1, übersprungen:     0, gesamt:     1, Dauer: 40 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.AudioPlugins_Ui` → Failed 0 (Bericht „Plugin-Prüfung.md“, Hinweis auf die Sperre fürs Laden)
- Geprüft mit nachgebauten Plugin-Ordnern: VST3-Bündel mit moduleinfo.json (x64), 32-Bit-VST3, VST3 mit kaputter Beschreibung, CLAP (x64) und eine CLAP-Datei ohne Programmkopf. Architektur wird aus dem PE-Kopf gelesen; es wird kein Plugin-Code ausgeführt.
- Auf diesem Rechner gibt es keine Ordner „Common Files\VST3“ oder „Common Files\CLAP“ – der echte Scan meldet „Keine VST3- oder CLAP-Plugins gefunden“.

## Status der Testfälle
- S4-06-P (Host lädt Testplugin, verarbeitet Ton, schließt): **BLOCKIERT** – ein Plugin-Host führt fremden Code aus. Vorher nötig: REA-Clean-Room-Bewertung, abgeschottete Laufumgebung (eigener Prozess, kein Zugriff auf Nutzerdaten) und Freigabe.
- S4-06-N (abstürzendes Plugin isoliert): **BLOCKIERT** aus demselben Grund; der Scan selbst stürzt bei kaputten Plugins nicht ab (Test mit defekter Beschreibung und Datei ohne Programmkopf).

## Nicht geprüft
- Laden, Presets, Klangverarbeitung von Plugins
- Echte Hersteller-Plugins (keine installiert)
