# Bericht T-0504: Restliche Grenzen beim Excel- und OBJ-Import

**Auftrag:** Die vier von der Prüfung zu T-0503 genannten Lücken schließen.
**Branch/Worktree:** `claude/T-0504-import-budget`, Worktree `Trend Os-worktrees/T-0504-import-budget`, Basis `claude/T-0503-import-grenzen` 82d0c981a (PR 283 ist noch nicht in main).

## Geänderte Dateien
- `src/Trendo.Core/Office/SheetFiles.cs`
  - `ReadCharts`: Beziehungen über ein Wörterbuch, jedes Diagramm-Ziel wird je Mappe nur einmal geladen, höchstens `MaxEntries` Anker je Mappe. Das Blatt wird nicht mehr ein zweites Mal als XDocument geladen.
  - Klasse `ExtraParts`: gemeinsames Budget je `ReadXlsx` von 512 MB für alle Lesevorgänge in `ReadExtras` und `ReadCharts`. Wird es überschritten, lehnt Trendo die Datei ab, statt sie still ohne Beigaben zu öffnen.
  - `Outermost`: Verschachtelte Titel- und `c:f`-Texte zählen einmal.
  - `RelTargets`: Ziele über 65.535 Zeichen zählen als fehlend, damit sie nicht je Blatt oder Anker verarbeitet werden.
- `src/Trendo.Core/Office/OfficeZip.cs`: `ShallowReader` und `LoadShallowXml` mit `MaxXmlDepth` = 256. Nur vom XLSX-Pfad benutzt.
- `src/Trendo.Core/MeshIO.cs`: `ImportParts(path, unit, maxTriangles = 20 Mio.)`. `ReadObjParts` prüft Punkte, Dreiecke gesamt, Dreiecke je Teil und höchstens 100.000 Teile beim Einlesen und liest die Datei zeilenweise statt komplett in den Speicher.
- `tests/Trendo.Core.Tests/ImportTempoTests.cs`: 21 neue Tests.

## Zusätzlich zum Auftrag gefunden und behoben
Die Negativtests zeigten zwei Ursachen, ohne deren Behebung Punkt 3 nicht erfüllbar war:
- `XElement.Value` ist rekursiv: 30.000 verschachtelte Ebenen in einem Diagramm beendeten den ganzen Prozess (Stack Overflow, im Test gemessen).
- Der XML-Baum läuft beim Aufbau je Knoten alle Eltern ab: 1 Mio. Ebenen hingen länger als 100 s (gemessen, Testlauf abgebrochen).

Deshalb gibt es die Tiefengrenze für alle in XLSX als Baum gelesenen Teile. Die gestreamten Teile (Zellen, gemeinsame Texte, Notizen) haben sie nicht und brauchen sie nicht.

## Nachweis der Negativtests
Vor der Änderung gegen den alten Code ausgeführt:
- Rot: Ankergrenze (je Mappe und über Blätter), Budget für Notizen und Zeichnungen, OBJ-Gesamtgrenze, OBJ-Grenze beim Einlesen, Vieleck, Teile-Grenze.
- Beziehungsliste: 94,7 s alter Code, 0,65 s neuer Code.
- Tief verschachtelte Diagramme: Stack Overflow im alten Code.
- „Viele Anker auf dasselbe Ziel“ war im alten Code grün, weil dort kein Budget existierte. Mit ausgeschaltetem Zwischenspeicher wurde der Test rot, also prüft er den Zwischenspeicher.
- Nicht gegen den alten Code gelaufen: die drei später geänderten oder ergänzten Tests (zwei Tiefentests, „Notizen und Zeichnungen zählen gemeinsam“, „Anker ohne Id“). „Überlange Beziehungsziele“ scheiterte im alten Lauf an der ZIP-Prüfung und wurde danach umgebaut.

## Gleichheit der Ergebnisse
Vergleich von 82d0c981a und diesem Stand auf 105 Dateien (24 echte XLSX, 80 echte OBJ, eine erzeugte Mappe mit Diagrammen): Der SHA-256 über alle gelesenen Daten ist in allen 105 Zeilen gleich (Gesamthash `C0B70E0C…61EB860`). Darin 5 Diagramme, 7 Notizen, 2 Regeln, 1 Auswahlliste, 153 OBJ-Teile, 0 Lesefehler. Das Vergleichswerkzeug liegt nicht im Repository, die Dateien bleiben lokal.

## Bewusste Änderungen im Verhalten
Nur für Dateien, die keine normalen sind oder extrem groß sind:
- OBJ mit mehr als 20 Mio. Dreiecken oder mehr als 100.000 Teilen werden abgelehnt.
- XML tiefer als 256 Ebenen: In Diagramm, Zeichnung oder Notiz-Beziehung kostet es nur diese Beigabe, sonst wird die Mappe abgelehnt.
- Überlanges Blatt-Ziel: Das Blatt liest wie bei einem fehlenden Ziel `worksheets/sheet1.xml`, vorher blieb es leer.

## Prüfung
- **QA (19-qa-verification), nur lesend, nichts ausgeführt:** `BLOCKED`. Der blockierende Testbefund war gerechnet und trifft nicht zu (der Test ist grün). Der zweite Punkt war der fehlende Bericht. Die Hinweise zu Testlücken sind mit zwei Tests und einem genaueren Testnamen erledigt.
- **Sicherheit (03-security-permissions), nur lesend, nichts ausgeführt:** Die vier Auftragspunkte sind ohne Befund, Gesamturteil `BLOCKED` wegen der älteren Lücken unten.

## Kommandos und Testergebnis
- `dotnet test Trendo.sln -c Release`, Exitcode 1: Api Fehler 0, erfolgreich 16, gesamt 16. App Fehler 0, erfolgreich 881, übersprungen 4, gesamt 885 (36 min 4 s). Core Fehler 1 von 1967 (`assimp echt über TrendoOS --assimp-convert`, lief parallel zu App).
- `dotnet test tests/Trendo.Core.Tests -c Release`, Endstand mit allen Tests, allein, Exitcode 0: Fehler 0, erfolgreich 1964, übersprungen 5, gesamt 1969.
- `python -m unittest discover -s tests/claude_handoff`, Exitcode 0: 9 Tests, OK.

Der Assimp-Fehler trat wie bei T-0503 nur im Parallellauf auf, Ursache weiter nicht geklärt.

## Offen: das Schutzziel ist noch nicht vollständig erreicht
Alles bereits vorhanden und außerhalb der vier Punkte, nicht geändert. Soweit nicht anders vermerkt: aus dem Code gelesen, nicht ausgeführt.

Excel:
- Auswahllisten: Je `sqref`-Bereich wird die Liste neu zerlegt und behalten, wenige MB XML können den Speicher füllen.
- `<col>`: Spaltennummern sind nicht auf 16.384 begrenzt, je `<col>` entstehen bis zu 201 Einträge, je Blatt erneut.
- Budget in Bytes, aber keine Knotenzahl: Sehr viele kurze Elemente in stark gepackten Teilen kosten ein Vielfaches an Speicher.
- `Sys10.cs` liest XLSX ohne `OfficeZip`.

OBJ und andere 3D-Formate:
- `mtllib` ist ein ungeprüfter Pfad ohne Größengrenze (`/dev/urandom`, Windows-Freigaben im Netz).
- STL, GLB, glTF und FBX haben keine Grenze beim Einlesen.
- Der Import läuft synchron, große Dateien können die Oberfläche blockieren.
- Speicher an der 20-Mio.-Grenze laut Prüfung bis etwa 6 GB (gerechnet), der Kommentar nennt 4,4 GB. Auf dem Pi reicht das nicht.

Andere Office-Formate (nur geprüft, nicht geändert):
- **DOCX und ODS:** Eine Datei mit 30.000 verschachtelten Ebenen beendet den Prozess (Stack Overflow, gemessen an 82d0c981a). 200 Ebenen laufen normal.
- **PPTX, ODT, ODP:** nutzen dasselbe `LoadXml` und verschachteltes `.Value` (`SlideRead.cs`, `OdtFormat.cs`), also vermutlich gleich anfällig. Aus dem Code gelesen, nicht ausgeführt.
- **ODS:** Wiederholungen (`number-rows-repeated`, `number-columns-repeated`) sind je Zeile bis 10.000 × 1.024 möglich.

## Was NICHT geprüft wurde
- Nach dem letzten Commit lief nur Core erneut. Dort kamen nur Testdateien dazu, App, Api und Paket-Tests liefen auf dem Stand davor.
- Keine Messung an der echten Grenze von 20 Mio. Dreiecken (Tests nutzen den Parameter), keine Messung auf dem Pi 4 oder unter Linux.
- Die Reviewer haben nur gelesen und nichts ausgeführt.

**Status:** Umsetzung fertig, Branch gepusht, kein PR angelegt (das `gh`-Konto gehört nicht zum Projekt), Merge offen.
