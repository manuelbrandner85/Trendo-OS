# T-0294 – 3D-Druck: Vorschau auf der Druckplatte, Schichten, Aufstellen, Prüfung

- Freigabe: Manuel im Chat am 31.10.2026 (Auswahl „3D-Druck mit Schicht-Vorschau“).
- Vorher: Datei wählen → in Bambu Studio öffnen → Drucker; vom Modell war nichts zu sehen.
- Neu (Schritt „Vorschau auf der Druckplatte“, für STL, OBJ, 3MF/.gcode.3mf, GLB):
  - 3D-Ansicht: Modell auf der Platte des Bambu Lab A1 (256 × 256 mm) mit Licht und Schatten.
  - Schichten: Regler über alle Schichten (0,2 mm), Ansicht von oben mit aktueller und vorheriger Schicht.
  - Prüfung: passt auf die Platte? Überhang steiler als 45° (cm²) mit Hinweis auf Stützen.
  - „Automatisch aufstellen“: beste der sechs Grundlagen (am wenigsten Überhang, dann niedrigste Höhe); „Um X/Y drehen“.
  - Kennzahlen: Maße, Schichten, Volumen; Filament (g/m PLA) und Druckzeit als **grobe Schätzung** (zwei Wände + 15 % Füllung) – die genaue Zeit rechnet weiterhin Bambu Studio.
- Kern `Trendo.Core.Cad.PrintPrep`: Laden in mm (3MF-Leser mit Einheiten, GLB in m), Platzieren, Drehen, Volumen, Schnitt, Überhang, Aufstellen, Auswertung.
- Grenzen (ehrlich): getrennte, sich überschneidende Körper zählen jeweils ganz (z. B. Unterseite eines aufgesetzten Balkens); STEP hat keine Vorschau (wie bisher nur Bambu Studio).
- Tests: `PrintPreview_Ui` (Kern: Volumen, Schnittumfang, Überhang, Aufstellen, Platte, 3MF hin und zurück; Oberfläche: Vorschau, Warnungen, Aufstellen, Schicht-Regler, STEP ohne Fehler); `N23N24_CadPrintUi` grün.
