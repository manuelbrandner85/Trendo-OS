# Bericht T-0613: Ruhiges Design

**Zweig:** `claude/T-0613-ruhiges-design` (Worktree `studios-aufraeumen`), enthält T-0611 (Kreativ & KI Teil 2) und T-0612 (kie.ai) sowie den Stand von `main` (0.9.93).

## Inhalt

- **Farben und Flächen** (`MainWindow.Look.cs`, `MainWindow.axaml.cs`): neutrales Grau mit feinen Kanten, Akzent Gold flach (#B38B22), keine Leucht-Schatten, Seitenwechsel nur kurzes Einblenden.
- **Schrift** (`UiFont`): eine Systemschrift (Segoe UI Variable → Segoe UI → Inter → Noto Sans), einmal beim Start ermittelt.
- **Kopf, KI-Zeile, Fenster, Leiste** (`Shell.cs`, `Windows.cs`, `DockLook.cs`): Linien-Symbol statt leuchtender Sonne, ohne Werbespruch, neutrale Rahmen, Leiste ohne Glanz und Goldrand – Bilder bleiben.
- **Studio-Kopf** (`ProgramShell.cs`): ohne Farbband und doppelten Titel; neutrale Stufenwahl.
- **Studio-Übersicht** (`StudiosView.cs`): schlichtes Suchfeld, eckige Filter, Karten mit Bild oben und Text darunter; Hinweise als ruhige Info-Zeile.
- **Linien-Symbole** (`GlyphIcons.cs`): Knöpfe aus einem Zeichen (▶ ✕ ☰ …) zeigen eigene Pfade; Name/Hinweis wechseln mit dem Zeichen; gesperrte Knöpfe blass; das Zeichen bleibt für Claude-Brücke, Befehlssuche und Tests abrufbar.
- **Ruhe-Durchgang** (`CalmLook.cs`): nach jedem Seitenwechsel restliche Verläufe → ruhige Einzelfarbe, Leuchten weg; ausgenommen Bilder (und Verläufe darüber), Logo, Hintergrund, echte Schatten (Entwürfe, Video-Titel, Systemstile) und bewusst behaltene Verläufe der Stile Windows/Mac/Linux.
- **Texte:** ca. 1500 Schmuckzeichen aus Beschriftungen entfernt (Skript), Werbesprache sachlich. Statuszeichen ⚠ ✓ ✕ ✗ bleiben als Textzeichen (Farbe, Fehlerart und Audit hängen daran). Zeichen mit Bedeutung als Wort: Vorwärts/Rückwärts, Elektro, (w)/(m), „auf/ab“, „Suche“; ★ gemerkt, ● Aufnahme, ◆ Erinnerung.
- **Dichte** (`UserSettings.Density`, Einstellungen › Bedienung): Normal/Kompakt.

## Unabhängige Prüfung (Agent, nur lesend)

14 Befunde; behoben: Schatten aus Entwürfen/Video-Titeln/Systemstilen wurden gelöscht; Name/Hinweis der Symbol-Knöpfe blieben beim Zeichenwechsel stehen; Claude-Brücke und Tests fanden Symbol-Knöpfe nicht mehr; Abspielrichtung, Fahrzeugart, Figurengeschlecht, Plattform-Art gingen verloren; alte Chats mit „✳ Claude (dein Abo)“ wurden als Ersatz-KI gezeigt; Auf-/Abstieg asymmetrisch; gesperrte Symbol-Knöpfe sahen aktiv aus; mehrdeutige feste Namen (●, ✓); Kompakt-Mindesthöhe vergrößerte kleine Knöpfe; Systemstile verloren ihre Verläufe.
Nicht geändert (niedrig): Verläufe aus Stil-Zuständen (:pointerover) würden eingefroren – im Code kommen keine Verlaufs-Stile vor; nachgeladene Bilder unter einem Verlauf werden nicht erneut geprüft.

## Leistung

Erster Volltest: App 86 min statt ~44 min. Messung (40 Seiten): 30 s neu gegen 16 s alt; Ursache die Schriftliste im Fenster-Stil (Fallback-Suche bei jedem Text), nicht Ruhe-Durchgang oder Symbole. Nach `UiFont`: 15,8 s.

## Testergebnis

- Lauf 1: 38 Fehler (Statuszeichen mit entfernt, zwei Zeitüberschreitungen) – behoben.
- Lauf 2 (Stand a690cff2b): Exitcode 0 – Api 16; Core 2281 bestanden, 5 übersprungen; App 1228 bestanden, 4 übersprungen (36 min).
- Nach den Prüf-Befunden: 102 betroffene Tests grün.
- Abschluss-Volltest nach Zusammenführung mit `main` (0.9.94, Stand 4f689f49a): Exitcode 0 – Api 16; Core 2417 bestanden, 5 übersprungen; App 1238 bestanden, 5 übersprungen (49 min); Paket-Tests OK. Auszug `reports/volltest-T-0613/lauf3-abschluss.txt`.

## Belege

`reports/volltest-T-0613/` (Testauszüge, Vorher/Nachher-Bild). Bei „Bilder mit KI“ und „Videoschnitt“ zeigt das Vorher-Bild eine geöffnete Vorlage, das Nachher-Bild die Startansicht – kein Funktionsunterschied.

## Nicht geprüft / offen

- Aussehen im echten Fenster auf dem PC (nur ohne Fenster gerendert).
- Kompakt wirkt nur auf Knöpfe/Felder ohne eigenen Innenabstand.
- Session B nutzt eigene Programme: neue Bausteine (GlyphIcons, CalmLook) greifen dort automatisch.
