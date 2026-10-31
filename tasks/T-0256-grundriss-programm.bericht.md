# Bericht T-0256 – Grundriss-Programm (SPEC-01)

## Geänderte/neue Dateien
- `src/Trendo.Core/Arch/FloorPlan.cs` – Modell (Wände, Räume, Öffnungen, Möbel, Formatversion 1), Fangen, Achsfang, Raum aufziehen mit geteilten Wänden, Raum erkennen (Rasterfüllung + Umriss), Öffnungen, Ziehen mit angeschlossenen Wänden, Treffer, Kennzahlen, WoFlV, Material, Prüfung, Vorlagen, Übernahme der alten Raumliste
- `src/Trendo.Core/Arch/PlanDrawing.cs` – eine Zeichnung für Bildschirm, SVG, DXF (R12, Ebenen, Wände an Öffnungen unterbrochen), PDF (A4/A3, 1:50/1:100/1:200), Raumbuch, Materialliste
- `src/Trendo.Core/Arch/Plan3D.cs` – 3D-Körper mit Brüstung/Sturz
- `src/Trendo.App/FloorPlanView.cs` – Zeichenfläche (Maus, Tastatur, Zoom/Verschieben, Vorschau, Auswahl, Griffe)
- `src/Trendo.App/MainWindow.Spec01.cs` – Programm statt Formular
- `src/Trendo.App/MainWindow.ProgramShell.cs`, `MainWindow.axaml.cs` – Modus „volle Höhe“ für Zeichenprogramme (kompakter Kopf, Speichern unter als Aufklappfeld)
- `tools/Trendo.Render/Program.cs` – Bildschirmfotos mit Vorlage/Auswahl/3D, Lageausgabe
- Tests: `tests/Trendo.Core.Tests/FloorPlanTests.cs` (8), `tests/Trendo.App.Tests/FloorPlanUiTests.cs` (4, echte Maus- und Tastatureingaben); alte Formular-Tests ersetzt, `FachBedienungTests` auf SPEC-03 umgestellt

## Tests (gezielt)
- Core T0256: 8/8 bestanden
- App SPEC01_Ui: 4/4 bestanden; Studio-/W6-/Fach-UI-Klassen: 51 Tests, danach alle grün
- Volltest: siehe Release-Bericht `reports/volltest-0.9.24/`

## Nicht geprüft / offen
- Echte Bedienung am PC (nur headless mit simulierten Maus-/Tasteneingaben und Bildschirmfotos)
- Doppelklick-Raumerkennung nur im Kern getestet (nicht per Maus-Doppelklick)
- KI-Aktion „Raum anlegen“ schreibt weiter in die alte Raumliste (übernehmbar per Vorlage) – Anbindung an den Grundriss offen
- Mehrere Geschosse, Dachschrägen als Geometrie, Bemaßung frei setzen, Bilder als Vorlage hinterlegen – noch nicht
