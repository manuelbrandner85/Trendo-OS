# T-0291 – Spiele-Werkstatt: Level-Editor und neue Spielart „Plattformer“

- Freigabe: Manuel im Chat am 31.10.2026 (Auswahl „Spiele-Werkstatt: Level-Editor“).
- Vorher: „Spiele bauen“ ohne Arbeitsfläche; Level nur als Textzeilen aus # und . (Hüpfspiel: ein Hindernisband).
- Neu:
  - Spielart **Plattformer** (Browser, sofort spielbar): Kacheln # Boden, o Münze, E Gegner (läuft hin und her, Draufspringen besiegt ihn), ^ Stacheln, S Start, Z Ziel-Flagge; Schwerkraft, Kollision, scrollende Kamera, Leben, Punkte. Vorlage „Retro-Plattformer“ mit fertigem Level (60 × 12).
  - Bereich **Level** im Spielprogramm (`LevelCanvas`): Kachelpalette, linke Maustaste malt (auch ziehend), rechte radiert, Start nur einmal, ± 10 Spalten, jeder Strich ein Schritt im Rückgängig; Kennzahlen (Münzen, Gegner, Stacheln) und Prüfung (Start, Ziel, Boden unter dem Start); **▶ Testen** baut das Spiel und öffnet es. Funktioniert auch für Hüpfspiel und Mauerbrecher.
  - Spielart-Auswahl rechts kennt den Plattformer; Spielprüfung nutzt die Level-Prüfung.
- **Fehler behoben:** Die Malfläche holte sich beim Klick den Fokus, dadurch scrollte die Seite und der Strich landete zwei Reihen tiefer – Fokusgriff entfernt.
- Prüfung des Spiels: Plattformer-Code in Node mit Attrappe für Zeichenfläche und Tasten 4 000 Bilder ohne Fehler (Figur läuft, springt, fällt in eine Lücke, verliert ein Leben). Im echten Browser von Hand noch nicht gespielt.
- Tests: neu `LevelEditor_Ui` (echte Maus: ziehend malen, Start versetzen, rechts radieren, Prüfung, Rückgängig, Testen baut Spiel mit dem Level; Kern: Prüfregeln, erlaubte Kacheln, Vorlage ohne Hinweise); `GameProgramUiTests` und Kern-Spieltests grün.
