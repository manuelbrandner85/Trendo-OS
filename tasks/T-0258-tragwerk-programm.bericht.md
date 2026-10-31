# Bericht T-0258 – Tragwerk & Mechanik

## Neu
- `src/Trendo.Core/Statik/Frame.cs` – Modell, Querschnitte (IPE/HEA-Tabellen, Rechteck, Rohr, Hohlprofil), Weggrößenverfahren mit Gelenken (statische Kondensation), Schnittgrößen, Biegelinie, Nachweise, Vorlagen
- `src/Trendo.Core/Statik/FrameDrawing.cs` – Zeichnung (Bildschirm/SVG/PDF), CSV, PDF-Bericht
- `src/Trendo.App/FrameCanvas.cs`, `src/Trendo.App/MainWindow.Spec03.cs` – Programm statt Formular, Live-Rechner
- Tests: `tests/Trendo.Core.Tests/FrameTests.cs` (5, gegen Lehrbuchwerte), `tests/Trendo.App.Tests/FrameUiTests.cs` (3, echte Maus/Tastatur); alte Formular-Tests für SPEC-03 ersetzt, T-0207-Formulartests auf SPEC-14 umgestellt
- Nebenbefund behoben: Ein reiner Logik-Test konnte die statischen Pinsel von MainWindow im falschen Thread anlegen → jetzt `AvaloniaFact`

## Prüfwerte (Core, bestanden)
Einfeldträger q=10, L=5: A=B=25 kN, M=31,25 kNm, f=5qL⁴/384EI · Kragarm P=3, L=2: M=6 kNm, f=PL³/3EI · Durchlaufträger 2×4 m, q=8: B=40 kN, M_B=−16 kNm · Rahmen: Gleichgewicht · Fachwerk: Momente 0, A=B=15 kN · Mechanismus erkannt

## Nicht geprüft / offen
- Bedienung am PC (nur headless); kein Abgleich mit einer zweiten Statiksoftware (nur Lehrbuchformeln)
- Trapezlasten, Lagersenkung, Temperatur, schräge Lager, Normen-Nachweis nach Eurocode (nur Vorbemessung)
