# Bericht T-0620: Restpunkte aus T-0615/T-0616

**Zweig:** `claude/T-0620-reste` von `main` (d9f9a394d).

## Inhalt

- Von selbst angelegte Datei folgt dem neuen Titel (samt Versionen), die alte verschwindet; selbst benannte Dateien bleiben unverändert (`MainWindow.ProgramShell.cs`).
- „Markierung an Claude“ steht im Fokusmodus wieder neben „Claude“ (`MainWindow.PromptAction.cs`).
- Umrechnung der Einstellungen: wer ausdrücklich „Erweitert“ oder „Profi“ gewählt hatte, behält diese Stufe; der alte Schalter „Einfacher Modus“ geht dann aus (`Sys13.cs`).
- BibTeX-Import: jede Klammer-/Anführungs-Suche höchstens 100 000 Zeichen; präparierte Dateien mit vielen offenen Klammern werden schnell abgewiesen, gute Einträge danach gelesen (`KreativKits4.cs`).
- `Cli.Run` wartet nach Programmende den Rest des Zeitlimits (mindestens 10 s) auf die Ausgabe – unter Last brach ein ffmpeg-Lauf sonst mit „Ausgabe unvollständig“ ab.

## Tests

- Neu bzw. erweitert: EinfachBedienen_Ui.Autosave_rename, ClaudeOperate_Ui (Fokusmodus), Sys13Tests (Umrechnung), KreativKits4Tests (BibTeX offene Klammern).
- Erster Volltest brach ab, weil Laufwerk C: voll war (nicht durch Trendo-Code); nach dem Freiräumen erneut.
- Volltest (Stand 956b6767c): Exitcode 0 – Api 16; Core 2489 bestanden, 5 übersprungen; App 1284 bestanden, 5 übersprungen; Paket-Tests OK.
