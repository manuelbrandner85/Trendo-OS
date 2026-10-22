# T-0042 Anschluss

**Ziel:** Blockierte Prompt-Aktionen an vorhandene Dienste hängen, soweit das ohne neue Wirkung geht. Zwei Fehler beheben: das Baukasten-Formular und die offene Studiofläche übernehmen die neue Datei, `IWebsitePageAdapter` ist registriert.
**Branch:** cursor/v42-adapter-c7fc
**Grundlage:** `cursor/v42-uebergabe-fa7f` (3677ad8).

## Dateien

- `src/Trendo.Core/PromptAction/ConnectedAdapters.cs`, `PromptServices.cs`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs`, `SystemPromptAdapter.cs`, `WebsiteStudio.cs`, `CapabilityCatalog.cs`, `Model.cs`, `FieldKit.cs`, `RunStore.cs`
- `src/Trendo.Core/ProjectStore.cs`, `src/Trendo.Core/W3/Research.cs`
- `src/Trendo.App/MainWindow.PromptAction.cs`, `MainWindow.Research.cs`, `MainWindow.W4Builder.cs`, `MainWindow.W5Ext.cs`, `ReadOnlyWeb.cs`
- `tools/Trendo.Render/Program.cs`
- `tests/Trendo.Core.Tests/ConnectedPromptTests.cs`, `PromptActionTests.cs`
- `tests/Trendo.App.Tests/PromptBarTests.cs`
- `docs/improvement-42/*`

## Testergebnis

`dotnet test Trendo.sln -c Release`:

- Api: 15 bestanden, 0 übersprungen, 0 Fehler
- Core: 598 bestanden, 10 übersprungen, 0 Fehler
- App: 213 bestanden, 0 übersprungen, 0 Fehler

Prompt-Filter `PromptActionTests|ConnectedPromptTests`: 103 bestanden, 0 Fehler.

Headless-Bild `baukasten-vorschau.png`: Leiste „QA bestanden“, Formular-Titel „Hofseite“, Text „Willkommen im Hof“. Es wurde nichts veröffentlicht.

## Was jetzt echt ist

- Kamera: nur Namen aus sysfs. Keine Aufnahme, kein Mikrofon.
- Übersetzung: nur Deutsch oder Englisch, nur wenn die angeschlossene lokale KI antwortet. Language Studio 2.0 bleibt abgelehnt.
- Recherche: Lesemodus über den eingesetzten Leser. Offline und abgelehnte Adressen holen nichts. Der GET liegt in der App (`ReadOnlyWeb`), nicht im Kern.
- KI-Frage und Team: nur die angeschlossene lokale KI. Ohne Antwort keine Datei.
- Kostenlose KI: nur Loopback, und nur wenn die App die Prüfung einsetzt. Online-Konten werden nicht gefragt.
- Desktop, Darstellung, Geheimnis, Verbund: nur anzeigen. Nichts ändern, kein Wert, nichts koppeln.
- Geräte: nur die gelesene Liste. Nichts starten.
- Handy: nur `adb devices -l`, wenn adb schon da ist. Kein Verbinden, kein Spiegel, kein Foto, keine Installation.
- Prüfung und Reparatur: Protokoll von `SelfHealer` mit `Run(false)`. Explorer bleibt zu. Verbesserung 35 bleibt offen.
- Seite: `PageWriteAdapter` schreibt HTML und `seite.json`. Kein 3D, kein Pixel-Streaming, nichts veröffentlicht.

## Was nur nach dem Knopf „Einmal freigeben“ läuft

Der Satz „Freigabe: erteilt“ im Text reicht nicht.

- `datei.laden`: http(s) im Lesemodus, höchstens 2 MB, Datei ins Projekt.
- `datei.teilen`: ein Ordner im Projekt, höchstens 24 Stunden, nur lesen.
- `programm.installieren`: nur ein Katalogprogramm, nur mit vorhandener Lizenzdatei, danach muss das Programm erkennbar da sein. Sonst kein Erfolg.
- `programm.starten`: nur ein Programm aus der festen Liste und eine Datei im Projekt.

Eine Ablehnung, bevor etwas nach außen lief, gibt den Idempotenzschlüssel frei. Ein angefangener Versuch ohne Beleg bleibt gesperrt.

## Was blockiert bleibt

- `SPEC-09` Scan und `SPEC-12` Forensik: auch „Los“ schreibt keine Datei.
- Kamera öffnen, Mikrofon, Roboter, CNC, Handy verbinden, Koppeln, Explorer, Reparieren, Theme ändern, Desktop ändern, Geheimnis setzen oder löschen.
- Versand, Zahlung, Veröffentlichen, Klonen, Blender, Unreal, Maschinenlauf, Boot.
- Kostenpflichtige KI wird nicht vorausgesetzt und nicht aufgerufen.
- Kein Studio ist `ready`. Verbesserung 42 ist nicht vollständig implementiert.

## Nicht geprüft

- Kein Windows-PC, kein winget, kein angeschlossenes Handy, keine echte Kamera, kein QA-017, kein QA-019 als bestanden markiert, keine Codesignatur. Die zehn übersprungenen Core-Tests sind die Windows-Prüfungen. Kein Merge.
