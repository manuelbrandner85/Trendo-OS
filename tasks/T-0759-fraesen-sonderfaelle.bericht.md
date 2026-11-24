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

## Gegenprüfung behoben (T-0759-fix1)

Gegenprüfung von 671475769: ein HOCH-, drei NIEDRIG/MITTEL-Befunde. Behoben in eigener Kopie des Zweigs, Basis 671475769.

1. **HOCH – Platte als Zeichnungsrahmen gedeutet, Löcher außen gefräst.** Von der Gestalt her sind „Platte mit Löchern“ und „Rahmen mit Teilen“ dasselbe.
   - Der Ebenenname zählt nur noch als ganzes Wort („Rahmen“, „FRAME“ – nicht „Rahmen-Teil 1“); Name oder Normgröße allein reichen nie.
   - Als Rahmen gilt er nur mit **Text im Schriftfeld** (TEXT/MTEXT, neu `CamSettings.Texts`, beim Import aus der Zeichnung übernommen und im Dokument gespeichert)
     **und unterteiltem Schriftfeld** (mindestens eine weitere Linie darin).
   - Fehlt ein Beleg, wird nichts weggelassen: der Umriss bleibt Platte, alle geschlossenen Konturen darin werden ausgelassen und gemeldet
     („Rahmen oder Platte nicht eindeutig“). Im Zweifel geht G-Code verloren, nie wird ein Loch außen gefräst.
   - Ohne jeden Hinweis (kein Rahmen-Name, keine Normgröße) bleibt es wie bisher eine Platte (Löcher innen).
2. **Platte auf Ebene ACHSE/CENTER/Mitte/BEM/„Achse 1“/„Dimension 3mm“ ohne Fräsbahn.** Kurznamen (BEM, DIM, ACHSE, MITTE, CENTER, auch „Dimension“) zählen
   nur als ganzer Ebenenname ohne Ziffern oder Zusätze; „dimension“ zählt nicht mehr als Wort in längeren Namen. Eine äußerste geschlossene Kontur auf so
   einer Ebene, die selbst geschlossene Konturen enthält, wird wie auf main als Kante gefräst und gemeldet. Ein Lochkreis in einer Platte bleibt Hilfslinie.
3. **Meldungen gekürzt, auch zu Löchern.** Die Art wird jetzt über den Meldungstyp bestimmt; gekürzt werden nur „offene Kontur ausgelassen“ und
   „Mehrdeutiger Spalt“, mit Grund in der Zusatzzeile. Meldungen zu geschlossenen Konturen (Loch/Platte ausgelassen, zu klein, zu nah …) bleiben vollständig.
4. **e2_kante_3000:** abwechselnd gemessen kein Unterschied zu 758 (siehe `reports/T-0759/messtabelle.md`).

Zusätzlich aus den Gegenfällen der Prüferin (Rotlauf zeigte Loch außen auch hier):
- **a5** (Umriss aus Bemaßungslinien mit 0,3-mm-Spalten, Kreis halb in der Hülle): Ein Hilfslinien-Baum gilt nur noch als frei, wenn keine zwei freien Enden
  (auch zu anderen offenen Linien) näher als max(2 mm, Fräser-Ø) liegen; kurze Linien wie Pfeilspitzen zählen dabei nicht.
- **t2/t6** (zwei Linien quer über ein Loch mit Insel – möglicher Steg): Die Durchgangs-Ausnahme gilt nur für leere Löcher. Liegt eine Insel im Loch, zählen
  die Stücke in Luft als Anschluss, und „ganz in einer beteiligten Kontur“ verschont nur noch, wenn keine Linie in dieses Loch hineinreicht (Platten: unverändert,
  Linien darin sind Gravur). 758 fräste die Insel in beiden Fällen außen.

**Prüfung:**
- Regression gegen cf3a5ba1b über 1 103 Fälle (39 Sätze + 40 Gegenfälle der Prüferin): `reports/T-0759/vergleich.md`, Abschnitt „Gegenprüfung behoben“.
  Außerhalb der Gegenfälle dieselben 9 begründeten Unterschiede wie vorher; Prüfprogramme 80 → 74 Befunde, kein neuer.
- Rotlauf: Prüfskript der Prüferin auf 758, 671475769 und fix1 – 671475769: 8 Fälle Loch außen, 7 Platten verloren; fix1: 0 und 0.
- Neue Tests (`Plate_is_not_a_frame` mit allen 5 Varianten und den Gegenproben „Text, nicht unterteilt“ / „unterteilt, ohne Text“ / echter Rahmen,
  `Plate_on_annotation_like_layer`, `Gaps_and_web_over_hole`, erweitert `Warnings_summarized`).
- Kern-Klassen T0747_*, T0758_*, SPEC14_*, T0266, T0273, T0256, T0257, T0759: 164 von 164 bestanden.
- App-Klassen SPEC14_Ui, T0747_DxfUi (Import mit Texten, Anzeige der Meldungen): 14 von 14 bestanden. Volltest nicht gelaufen (erst am Schluss vor dem Release).

## Zweitprüfung behoben (T-0759-fix2)

Befund der zweiten Prüfung von f3d703507: Eine echte Platte (Typenschild mit graviertem Text im unterteilten Eckfeld) erfüllte alle Bedingungen von fix1
und ihre Löcher wurden wieder außen gefräst (g1–g4). Eine Platte mit Beschriftungsfeld ist von einem Zeichnungsrahmen nicht zu unterscheiden.

**Entscheidung des Koordinators, umgesetzt:** Trendo entfernt einen Zeichnungsrahmen nie mehr automatisch.
- `DropFrame` entfernt nichts. Die Erkennung (Rahmen-Ebenenname als ganzes Wort oder Normgröße mit 170–190 mm Eckfeld) dient nur noch der Meldung; bei
  Verdacht werden Umriss und alle geschlossenen Konturen darin ausgelassen.
- Die Meldung sagt, was passiert („nicht eindeutig, deshalb wird darin nichts gefräst, auch nicht der Umriss“), nennt den Grund (Ebene oder Blattgröße,
  Eckfeld, Text) und gibt einen passenden Rat: Rahmen → Ebene ausblenden oder Rahmen löschen; Platte → Eckfeld-Linien und Text auf eigene Ebene legen und
  ausblenden. „Ebene umbenennen“ nur, wenn der Verdacht vom Ebenennamen kommt.
- Ausgeblendete Ebenen werden beim Einlesen wirklich ignoriert (Konturen schon bisher, Texte jetzt auch) – geprüft im Test: Rahmen-Ebene ausgeblendet →
  Teile werden normal gefräst, Nullpunkt an den Teilen; Eckfeld-Ebene ausgeblendet → Platte außen, Löcher innen.
- **Bewusst umgestellte Tests:** `Drawing_frame` erwartete bisher, dass ein erkannter Rahmen weggelassen und die Teile darin gefräst werden; er erwartet jetzt
  „nichts gefräst“ mit ehrlicher Meldung, und der Weg über die ausgeblendete Rahmen-Ebene liefert die Teile. In `Plate_is_not_a_frame` erwartet die Kontrolle
  „echter Rahmen“ jetzt ebenfalls „nichts gefräst“. Grund: Ein Typenschild sieht genauso aus wie ein Rahmen, und ein Loch außen ist schlimmer als fehlender
  G-Code. Kein anderer Test wurde abgeschwächt. Die r_rahmen-Fälle der Prüfer und echte Rahmen (g6) liefern damit wie auf cf3a5ba1b keinen G-Code.
- Neu: `Nameplate_is_never_milled_as_frame` (g1–g4, Meldung ehrlich, Weg über ausgeblendete Eckfeld-Ebene).
- Alle übrigen Änderungen aus 671475769 und f3d703507 bleiben.

**Prüfung:** Regression gegen cf3a5ba1b über 1 109 Fälle (`reports/T-0759/vergleich.md`, Abschnitt „Zweitprüfung behoben“): 0 Löcher außen, G-Code-Verlust
gegenüber cf3a5ba1b nur bei Rahmen-Verdacht und in den schon in fix1 begründeten Fällen. Rotlauf auf f3d703507: 3 der neuen bzw. umgestellten Tests rot.
Tests: Kern-Klassen T0747_*, T0758_*, SPEC14_*, T0266, T0273, T0256, T0257, T0759 – 165 von 165 bestanden; App-Klassen SPEC14_Ui, T0747_DxfUi – 14 von 14 bestanden. Volltest erst am Schluss vor dem Release.
