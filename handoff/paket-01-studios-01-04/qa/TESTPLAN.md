# Abnahmevertrag Paket 01

`qa/TESTFAELLE.csv`: exakt 156 **noch nicht ausgeführte** Fälle – je 78 explizit genehmigten Verbesserungen ein Positiv- und ein Negativtest; zusätzlich die engine-spezifischen Unity-Gates aus `tech/UNITY_ENGINE_ADAPTER.md`.

Pro Fall muss Claude erst **ausführbare Tests** passend zur tatsächlich implementierten Fachlogik schreiben. Eine CSV ist kein automatisierter Test. Vorhandene Tests aus dem tatsächlichen Trendo-Repo wiederverwenden; neue Tests ohne Schein-PASS erstellen.

## Testebenen
1. Unit/API/Model: Editor-Modelle, Serialisierung, Grenzen, Recovery und Nebenwirkungsverbote.
2. Avalonia UI: Klick, Drag/drop, Hotkeys, Undo/Redo, Dateidiff, Keyboard/Accessibility.
3. Engine-Slice (lokal): Unity EditMode+PlayMode / Unreal Automation / Godot CLI, Blender CLI; `NOT_RUN` wenn nicht vorhanden.
4. Render-/Audio-/Video-Slice: tatsächlicher Preview, kodierter Export, Analyse mit FFprobe, Framevergleiche, Color/Timecode, Audio LUFS/TruePeak.
5. Sicherheit: Consent, ApprovalGate, keine unautorisierte Installation/Netzwerk/Veröffentlichung, Rechte, Lizenz, Projektisolation.
6. Performance: freier VRAM, Laufzeit, GPU-Budget, ggf. CPU-Fallback, reproduzierbare Messung.

## Standard-Trendo-Tests
Vor jedem Push/Git-Update `dotnet build Trendo.sln -c Release` und `dotnet test Trendo.sln -c Release`, Pakettests laut `CLAUDE.md`. Prüfskripte und Rohlogs `reports/` mit SHA-256; echte Zielrechner-Messungen nötig. `APPROVED_SPEC` niemals in `TESTED` umwandeln ohne tatsächliche Resultate.

## Wiederholbarkeit
Jedes Testartefakt enthält: Eingabedatei, Programmversion, Schalter, Seed, Testsnapshot, Betriebssystem, Gerät/GPU, Rohlog, Output-Datei, Hash, Soll-/Ist-Vergleich, Reproduktionsanleitung.
