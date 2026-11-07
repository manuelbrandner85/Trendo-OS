# S4-06 Plugin-Host (abgeschottet) – Lauf 07.11.2026

Die Freigabe des Nutzers kam am 07.11.2026 im Chat: zuerst „Ja, S4-06 angehen“, für den Test mit dem echten Plugin dann „Ja, abgeschottet laden“.

## Was gebaut ist (drei Sperren vor jedem Laden)

1. **Statische Prüfung** (`PluginInspect`):
   - Liest nur Bytes, führt nichts aus.
   - Ermittelt Architektur, Einstiegspunkt (VST3 `GetPluginFactory`, CLAP `clap_entry`), Signatur vorhanden ja/nein und SHA-256.
   - Wertet die Importtabelle aus und gibt Hinweise auf Netz, Programmstart, Eingriffe in andere Prozesse, Dienste, Registrierung und Nachladen.
2. **Freigabe je Datei** (`PluginHost.Approvals`):
   - Nur nach Angabe der Herkunft, mit Eintrag im Audit-Log.
   - Gilt nur für genau diese Prüfsumme. Ändert sich die Datei, ist die Freigabe ungültig.
   - Sie lässt sich zurücknehmen.
3. **Abschottung** (`Sys/Sandbox`, ohne Adminrechte):
   - Eigener Prozess mit **niedriger Integritätsstufe**: keine Schreibrechte auf die Dateien des Nutzers, kein Zugriff auf Programme mit normaler Stufe.
   - **Job-Objekt**: genau ein Prozess, Speichergrenze, kein Fehlerdialog, wird beim Ende mit beendet. Zwischenablage, Systemeinstellungen, Anzeige und Abmelden sind gesperrt.
   - **Zeitgrenze**. Minimale Umgebung, nur der eigene Arbeitsordner ist beschreibbar, nur die beiden Ausgabedateien werden vererbt.
4. **Host:**
   - Pedalboard 0.9.26 (Spotify, GPL-3.0) als eigenes Programm (`Assets/ki/trendo_plugin.py`), gestartet mit `python -I`. Neue Laufzeit `KiRuntime.PluginHost` in der KI-Bibliothek.
   - Er lädt die **innere Programmdatei** des VST3-Bündels. Den Bündel-Ordner lehnt Pedalboard unter Windows ab (gemessen).
5. **Audioeditor → „Plugins (VST3, abgeschottet)“:**
   - Prüfen → Herkunft angeben und freigeben → Parameter lesen → auf Auswahl oder ganze Spur anwenden.
   - Das Ergebnis ist der Schritt „Plugin: …“ und lässt sich rückgängig machen.
   - Bei einem Absturz entsteht ein Absturzbericht unter „Plugins“; das Projekt bleibt unverändert.

## Positiv (S4-06-P): echtes Plugin

Plugin: **Airwindows Consolidated** (VST3, MIT/GPL-3) aus dem offiziellen GitHub-Release `baconpaul/airwin2rack` (DAWPlugin).
- Zip-SHA-256 `a21a0df8…f292`, Programmdatei `f3e64a02…36bf`.
- Statische Prüfung (`pruefung-vst3.md`):
  - x64, Einstiegspunkt vorhanden, keine Netz-Bibliotheken.
  - Hinweise: ShellExecute, Fenster-Haken, Tastenzustände, Nachladen – bei JUCE-Plugins üblich.
  - Die CLAP-Fassung ist ebenfalls ladbar geprüft, der Host lädt CLAP aber noch nicht.

| Schritt (abgeschottet) | Ergebnis |
|---|---|
| Parameter lesen | ok – Effekt, 9 Parameter (replace, brightness, detune, bigness, dry_wet, bypass, input_level, output_level …) |
| Ton (2 s, 440 Hz, Stereo) mit Voreinstellung | ok in 0,4 s · Spitze −10,46 → −8,69 dB · Veränderung 117 % · Speicher 512 MB |
| Ton mit `dry_wet=0` | ok · Spitze −10,46 → −10,46 dB · Veränderung **0** → Parameter wirken |

Hörproben: `ton-nach-plugin.mp3`, `ton-dry_wet-0.mp3`. Protokoll: `host-lauf.txt`.

## Negativ (S4-06-N): Absturz isoliert

- **Echtes abstürzendes Plugin:**
  - Eigener Code `testdatei-absturz.c`, mit MSVC gebaut. Es meldet eine VST3-Fabrik, deren erster Aufruf auf Adresse 0 schreibt.
  - Ergebnis: „abgestürzt (Zugriffsverletzung, Code 0xC0000005) – nur der abgeschottete Prozess ist betroffen“. Es gibt keine Ausgabedatei, der Bericht steht in `absturzbericht-plugin.md`.
- **Absturz im Host-Prozess** (Selbsttest `absturztest`, `faulthandler._read_null`): ebenfalls 0xC0000005, isoliert (`absturzbericht.md`).
- **Ohne Freigabe:** Es wird kein Prozess gestartet, sowohl im Komponenten- als auch im App-Test.
- **Abschottung mit echten Windows-Prozessen** (`abschottung-lauf.txt`):
  - Schreiben außerhalb des Arbeitsordners: `UnauthorizedAccessException`, obwohl der Ordner für den Nutzer normal beschreibbar ist.
  - Zeitgrenze nach 3,0 s.
  - Weitere Programme: „Nicht genügend Quoten“.
  - .NET-Notabbruch wird erkannt.
- **Kaputte oder nicht passende Dateien:** 32 Bit, ohne Einstiegspunkt, ohne Programmdatei, defekter PE-Kopf → erklärt „nicht ladbar“.

**Befund und Korrektur:** Im Gesamttestlauf erbte ein gleichzeitig gestarteter anderer Prozess die Ausgabedateien und hielt sie offen. Deshalb sind die Handles jetzt nur unmittelbar um den Start vererbbar (unter Sperre).

**Tests:** PluginInspect 3, Sandbox 3, PluginHost 4 (einer echt), PluginScan, App `AudioPluginHost_Ui` und `AudioPlugins_Ui` – alle bestanden (`dotnet-test.log`).

## Ehrliche Grenzen

- **Lesen** fremder Dateien ist für das Plugin weiter möglich. Die niedrige Integritätsstufe verhindert nur Schreiben und Steuern.
- **Netz** ist nicht gesperrt. Dafür bräuchte es eine Firewall-Regel, und das wäre eine Systemänderung, die in diesem Projekt verboten ist. Die statische Prüfung zeigt Netz-Bibliotheken vor der Freigabe an.
- **CLAP** wird geprüft, aber noch nicht geladen. Pedalboard kann nur VST3.
- Verarbeitet wird **offline**, nicht in Echtzeit. Plugin-Oberflächen werden nicht angezeigt.
- Die Abschottung gibt es **nur unter Windows**. Unter Linux lädt Trendo keine Plugins und sagt das.
- Die Einrichtung des Plugin-Hosts über die KI-Bibliothek habe ich auf diesem PC nicht selbst angeklickt; Installationen aus Claude heraus landen im App-Container.
- Getestet wurde nur mit einem echten Plugin (Airwindows) und nur mit Preset-freien Parametern. Das Laden von `.vstpreset` ist nicht echt geprüft.
