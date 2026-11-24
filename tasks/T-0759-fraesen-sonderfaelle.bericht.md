# Bericht T-0759: Fräsen aus DXF – Sonderfälle

**Auftrag:** `tasks/T-0759-fraesen-sonderfaelle.md`. **Branch:** `claude/T-0759-fraesen-sonderfaelle`, Basis cf3a5ba1b (T-0758, enthält main 0.9.108).
**Geändert:** `src/Trendo.Core/W6/CncCam.cs`, neu `src/Trendo.Core/W6/CncCamSonderfaelle.cs`, Anzeige der Meldungen in `src/Trendo.App/MainWindow.DxfImport.cs`.
Tests: `tests/Trendo.Core.Tests/FraesenSonderfaelleT0759Tests.cs`. Belege: `reports/T-0759/` (Vergleich, Messtabelle, Rohlogs, Prüfsummen).
Grundsatz: Fräsbahnen nur, wo die Deutung eindeutig ist; sonst auslassen und melden. Kein Loch wird als Außenkontur gefräst.

## Was neu ist
1. **Zeichnungsrahmen mit Schriftfeld** (`DropFrame`): ein eindeutiger Rahmen um alle Teile wird nicht gefräst, die Teile darin schon (pruef15 r_rahmen: vorher kein G-Code).
2. **Bemaßungs- und Mittellinien-Ebenen** (`IsAnnotationLayer`): offene Linien darauf sind kein Hindernis; Bäume daraus (Mittelkreuz, Bemaßung mit Pfeilen) schützen nichts mehr.
   Geschlossene Linien darauf (Lochkreis) werden nicht gefräst, Konturen darin vorsichtig ausgelassen. Auf Ebene 0 bleibt alles wie bisher.
3. **Linienverbünde** (`AnalyzeOpen`): offene Linien, die sich kreuzen, mitten auf einer Linie enden oder über Ebenen hinweg berühren, werden als Verbund erkannt.
   Ein Verbund, der für sich einen Bereich einschließt („#“), schützt seine Hülle.
4. **Bereiche aus Linien und geschlossenen Konturen** (`Enclosures`, Regel c): Brücke aus zwei Linien zwischen zwei Platten, U mit sich kreuzenden Überständen,
   Sehne auf anderer Ebene – die beteiligten Konturen werden ausgelassen statt falsch gefräst (pruef18 b1/x1: 758 hatte dort ein Loch **außen** gefräst).

## Nachbesserungen nach der Rückmeldung des Koordinators
- **Löcher neben dichten Gravurlinien bleiben** (r2_diag_3000_loch25x4000): Was ein Linienverbund allein einschließt, liegt in seiner konvexen Hülle. Eine Kontur, die die
  Hülle samt 0,1 mm weder berührt noch schneidet, kann darin nicht liegen; vorher reichte der Rahmen des Verbunds für das Auslassen. Ergebnis: 25 Löcher wie in 758.
- **Linien, die ein Loch ganz durchqueren**, zählen nicht als Anschluss (die Linie liegt dort in Luft und trennt nichts). Gefunden durch die T-0758-Tests
  (`diagonal_60_loecher_50` gab „keine fräsbare Kontur“, `Cancel_quickly` dadurch ebenfalls rot). Überstände, die in einer Kontur enden, und Durchgänge durch Material zählen weiter.
- **Meldungen zusammengefasst** (`CncCam.SummarizeWarnings`): in der Anzeige und im Fehlertext „keine fräsbare Kontur“ höchstens 20 je Art (gleicher Text bis auf Zahlen),
  danach „… und N weitere Meldungen dieser Art“. `CamResult.Warnings` bleibt vollständig (Tests und Auswertungen zählen darauf, z. B. 2500 „zu klein“ in T-0758).
  Das gilt auch für die vorhandenen Meldungen „offene Kontur ausgelassen“ und „Mehrdeutiger Spalt“ (letztere kommt aus dem Verketten und wird beim Import vorn eingefügt);
  ihr Entstehen ist unverändert, nur die Anzeige ist kürzer. r2_diag_3000: 9023 Einzelzeilen → 3 Arten mit je 20 Zeilen plus Zusatzzeile.

## Prüfung
- **Regression** über 1 063 Prüfer-Altfälle (39 Sätze, Runden 9–21): gegen 758 sind 12 Fälle verschieden, jeder begründet (`reports/T-0759/vergleich.md`);
  r2_diag_3000 ist im Endstand byte-gleich mit 758 (eigens nachgeprüft). Prüfprogramme: kein neuer Befund; Befunde von 758 in pruef15/16/18 entfallen (3→0, 5→1, 2→0).
  Der verbleibende Befund u1_u_stoss_loch_in_kerbe ist seit T-0747 bekannt und begründet (Kreis in der Kerbe ist ein eigenes Teil).
- **Tests Kern** (`T0747_*`, `T0758_*`, `SPEC14_*`, `T0266`, `T0273`, `T0256`, `T0257`, `T0759`): siehe Abschnitt Testlauf unten.
- **Rot-Grün:** jeder neue Test ohne die zugehörige Änderung rot, mit grün (Hüllprüfung: `Dense_engraving_keeps_holes_outside_hull`; Durchgang: `Lines_through_holes`, dabei auch
  die beiden T-0758-Tests rot).
- **Laufzeit:** +23 % gegenüber 758 im Gesamtlauf (vom Koordinator angenommen). e2_kante_4000 abwechselnd gemessen ohne Unterschied (der Ausreißer war Last);
  p1_diag_gravur_1000 echt +3,7 s, Ursache und Begründung in `reports/T-0759/messtabelle.md`.
- **Abbruch:** Nachlauf neu höchstens 36 ms (758: bis 807 ms), p1_diag_gravur_5000 ohne Abbruch 6,0 s (758: 6,2 s).
- **Rust:** nicht eingesetzt – die Zeit liegt in der Menge der geprüften Berührstellen, nicht in der Rechengeschwindigkeit von C#.

## Testlauf
- Kern, Klassen `T0747_*`, `T0758_*`, `SPEC14_*`, `T0266`, `T0273`, `T0256`, `T0257`, `T0759`: **161 von 161 bestanden** (4 min 25 s).
- App, Klassen `SPEC14_Ui`, `T0747_DxfUi` (DXF-Fräsen in der Oberfläche), `T0266`/`T0273`/`T0256`/`T0257`, soweit in den App-Tests vorhanden: **14 von 14 bestanden**.
- Aus Platzgründen (C: rund 3 GB frei) **nicht** gelaufen: der Volltest aller Kern- und App-Klassen; laut Regel erst am Schluss vor dem Release.

## Offen
- p1_diag_gravur_1000 (1000 Linien durch einen Punkt, 10 000 Kreise) bleibt etwa 3,7 s langsamer als 758.
- „Mehrdeutiger Spalt“ entsteht beim Verketten (`CadContours`, T-0760-Bereich); hier nur in der Anzeige zusammengefasst.
