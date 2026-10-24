# T-0101 Bericht: UI41-1 Design-Tokens

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC) · **Prüfung gegen Ist-Stand:** `main` @ `979766e`, 2026-10-25
**Status:** TEILWEISE – Rest offen

## Befund
- Farben, Glas, Kante, Akzent und Text liegen zentral als Brushes in `MainWindow.axaml.cs` (Zeilen 20–27). Die Umschaltung Hell/Dunkel/Kontrast läuft über `C(dunkel, hell)` und `ApplyTheme`.
- Ein Theme-Wechsel baut das Fenster neu auf (`Reopen`). Der Fensterzustand bleibt dabei erhalten (`RestoreWindows`). Ein Wechsel ohne Neuaufbau ist nicht gebaut.
- Den Fokusring liefert das Fluent-Theme. Für Tastaturbedienung gibt es den Test „QA-017: Tab bewegt Fokus durch die Navigation, Enter aktiviert“. Einen eigenen Fokus-Style-Test gibt es nicht.
- Es gibt keine `Themes/*.axaml`-ResourceDictionaries und keine Controls unter `Controls/`. Die Bausteine sind Hilfsmethoden: `Panel`, `ShellCard`, `Chip`, `ToolButton`, `SmallOutline`, `Flat`.

## Status
TEILWEISE. Offen: Tokens als `DynamicResource` und Theme-Wechsel ohne Neuaufbau. Das ist ein großer, riskanter Umbau aller Seiten ohne sichtbaren Nutzen für den Anwender. Vorschlag: zurückstellen, bis Edition C abgenommen ist.

## Tests (dieser Prüfauftrag)
Release-Gesamtlauf 2026-10-25 (Windows-PC): Build 0 Warnungen, 0 Fehler · Api 15/15 · Core 543 bestanden, 1 übersprungen (von 544) · App 301 bestanden, 1 übersprungen (von 302), 31 min 46 s · Paket-Tests OK.
