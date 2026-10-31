# Bericht T-0259 – Schaltung & Simulation

## Neu
- `src/Trendo.Core/Elektro/Circuit.cs` – Modell, Netz (Union-Find inkl. T-Abzweig), Simulator (MNA, implizites Euler, Shockley-Dioden mit Newton + Spannungsbegrenzung), Prüfung, SPICE, Stückliste, Vorlagen
- `src/Trendo.Core/Elektro/CircuitDrawing.cs` – DIN-Schaltzeichen, Spannungsfarben, SVG
- `src/Trendo.App/CircuitCanvas.cs`, `MainWindow.Spec02.cs` – Programm statt Formular, Simulationstakt, Oszilloskop
- `src/Trendo.App/MainWindow.LiveCalc.cs` – gemeinsamer Sofort-Rechner (auch von „Tragwerk“ genutzt)
- Tests: `tests/Trendo.Core.Tests/CircuitTests.cs` (5), `tests/Trendo.App.Tests/CircuitUiTests.cs` (3, echte Maus); alte SPEC-02-Formulartests ersetzt

## Prüfwerte (Core, bestanden)
Spannungsteiler 12 V/10 k/4,7 k: 3,837 V · RC 1 kΩ/100 µF: 63 % nach τ, voll nach 5τ · LED rot an 9 V/330 Ω: ~2 V, ~21 mA · LC 1 H/15 µF: ~41 Hz · Gleichrichter lädt auf ~10,9 V
Gefunden und behoben: Newton-Abbruch zu früh (LED blieb sperrend), Vorzeichen des Quellenstroms.

## Nicht geprüft / offen
- Bedienung am PC (nur headless); Simulation nicht gegen ngspice gegengeprüft (nur Formeln)
- Transistoren, OPV, ICs, PCB-Layout, AC-Frequenzgang
