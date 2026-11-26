# Bericht T-0776: Fremdprogramm-Nachläufe mit vorhandenen Werkzeugen (Restliste Q3)

**Auftrag:** `tasks/T-0776-fremdnachlauf.md` (Freigabe 26.11.2026, Etappe 0.9.112) · **Zweig:** `claude/T-0776-fremdnachlauf` · **Basis:** main 0.9.111 = 2b1ffa923
**Status:** IMPLEMENTED, gezielt getestet mit den echten Fremdprogrammen. Kein Volltest (macht die Hauptsitzung).

## 1. Werkzeuge gefunden (nur lesend)
`reports/T-0776/werkzeuge.txt` (Skript `tools/echtlauf/t0776/werkzeuge_finden.ps1`): LibreOffice 26.2.4, Krita 6.0.4, Inkscape 1.4.4 und 7-Zip 26.05 liegen im
Trendo-Werkzeugordner `%LOCALAPPDATA%\TrendoWerkzeuge`; FreeCAD 1.1.4 und 7-Zip zusätzlich in `C:\Program Files`; keines im PATH.
`C:\Program Files\LibreOffice\program` enthält nur `logs` – dort hatten T-0692/T-0694/T-0740/T-0753 gesucht und „nicht installiert“ geschlossen.

## 2. Nachläufe (alle echt, nichts ersetzt) – Test `T0776_Fremdnachlauf` (9 Fälle), Belege `reports/T-0776/<fall>/` mit Rohlog und SHA-256
| Fall | Studio | Ergebnis |
|---|---|---|
| Text DOCX + ODT → LibreOffice Writer (Text, PDF) | SYS-10 | Titel, Überschrift, Absatz mit Umlauten, Liste, Tabelle kommen zurück; PDF 2 Seiten (Seitenumbruch) |
| Geld-Auswertung XLSX → LibreOffice Calc (CSV je Blatt) | SPEC-11 | 3 Blätter; Calc rechnet selbst: Überschuss 1035, Monatssumme 1297 / 66,90 / 1230,10; 5 Buchungen |
| Shopify-CSV Artikel/Kunden/Bestellungen → LibreOffice Calc | Online verkaufen | alle Zeilen 21/20/14 Spalten; Preise 21 / 22,5, Bestand 3, Umlaute, Adresse; „=1+1“ bleibt Text `'=1+1` |
| CAD-STEP → FreeCAD 1.1.4 | CORE-05 | **Fehler gefunden und behoben** (s. 3). Jetzt: 2 gültige geschlossene Körper, 6000 / 4625 mm³, Umriss 60×20×20, Namen „Quader“, „Winkel“ |
| CAD-DXF (fremde DXF → bearbeitet → DXF) → FreeCAD | CORE-05 | Ebenen erkannt, 10 Kanten (Polylinie, 2 Kreise, Bogen, Linien), bearbeitete Linie bis x = 70 |
| Wohnungs-DXF (fremd → Wand dicker → DXF) → FreeCAD | SPEC-01 | FreeCAD übernimmt `$INSUNITS` = cm (×10 in mm); Umriss auf 1 mm gleich wie Trendos eigener Leser |
| PSD + ORA → Krita 6.0.4 (ORA + PNG) | Künstlerstudio | **Fehler gefunden und behoben** (s. 3). Jetzt: Ebenen „Versteckt“ (verborgen), „Farbe rötlich“, „Skizze“, „Hintergrund“; Farben an 4 Punkten = Trendos Gesamtbild |
| Logo-SVG + Animations-SVG → Inkscape 1.4.4 | Logos / Animation | Logo: Objekte mit Trendo-Kennung, Lage 20/30/120/60, PNG-Farben gleich; Animation: Datei und Form 40×40 gelesen, Hintergrund stimmt |
| Sicherungs-ZIP + Erweiterungs-Export-ZIP → 7-Zip 26.05 | Daten schützen, Trendo erweitern | `7z t` „Everything is Ok“; entpackt bitgleich (SHA-256 je Datei = Original); Vermerk „nicht verschlüsselt“ sichtbar |

Ohne Werkzeuge (leerer Werkzeugordner über `TRENDO_INSTANZ`, leerer Programme-Ordner, kurzer PATH): 9/9 „übersprungen“ mit „BLOCKIERT – fehlt auf diesem PC: …“ (`reports/T-0776/tests/ohne-werkzeug-uebersprungen.txt`).
Jedes Fremdprogramm lief mit eigenem Profil im eigenen Temp-Ordner unter `$TEMP/trendo-auftrag-T-0776` (LibreOffice `-env:UserInstallation`, Krita/Inkscape über `USERPROFILE`/`APPDATA`/`LOCALAPPDATA` bzw. `INKSCAPE_PROFILE_DIR`/`XDG_*`, FreeCAD `FREECAD_USER_*`).
Nach dem Lauf: keine Datei unter `%LOCALAPPDATA%\TrendoOS`, im Trendo-Werkzeugordner oder in `C:\Program Files\FreeCAD 1.1` geändert; Krita-/Inkscape-/FreeCAD-Profile des Nutzers unverändert (Änderungszeit). Prozesse wurden nur von Trendos `Cli.Run` (eigene Prozesse, Zeitlimit) gestartet; keiner musste beendet werden.

## 3. Gefundene Fehler und Korrekturen (Rot-Grün)
Rotlauf auf main 2b1ffa923 mit demselben Test: **2 Fehler** (`reports/T-0776/rot/`), nach der Korrektur 9/9 grün.
- **STEP (CORE-05):** `CadExport.WriteStep` schrieb jede Facette als nacktes `FACE`. FreeCAD/Open CASCADE übernimmt nur `FACE_SURFACE` und las deshalb zwei leere Schalen ohne eine Fläche (Volumen 0). Jetzt `FACE_SURFACE` mit `PLANE` je Dreieck (Normale und Bezugsrichtung, Zahlen mit Dezimalpunkt). Trendos eigener STEP-Leser liest die Datei weiter (`MeshEditTests.Step_facetted_roundtrip`, `CadTests` grün). Die alte Zusage im Kommentar („lässt sich in FreeCAD, Fusion, SolidWorks öffnen“) war damit falsch; jetzt steht dort, was geprüft ist.
- **PSD (Künstlerstudio):** Krita übernahm die Füllbytes des Pascal-Namens („Skizze\0“), Umlaute kamen als „_“, und der Hintergrund fehlte (freie Stellen durchsichtig statt Hintergrundfarbe). Jetzt schreibt `PaintFiles.Psd` zusätzlich den Unicode-Namen (`luni`, wie Photoshop) und unten die Ebene „Hintergrund“ (wie schon in ORA). `KuenstlerTests.Files_roundtrip` prüft deshalb 3 statt 2 Ebenen in der PSD (Erwartung an das neue Format angepasst, nicht abgeschwächt).

## 4. Falsche „nicht installiert“-Aussagen berichtigt
Als Nachtrag (die alten Sätze bleiben lesbar, mit Verweis auf `reports/T-0776/`): `tasks/T-0692-text-editor.bericht.md`, `tasks/T-0694-geld-rechnungen.bericht.md`,
`reports/T-0694/BELEG.md`, `tasks/T-0753-verkaufen-pod.bericht.md`, `reports/T-0740/BELEG.md` (Gegenprobe dort nicht nachgeholt), `tasks/T-0682-b1c-office-schutz.bericht.md`,
`reports/T-0691/BELEG.md` und `tasks/T-0691-3d-cad.bericht.md` (FreeCAD war damals wirklich nicht da – Nachtrag „inzwischen vorhanden, geprüft“), `tasks/T-0754-dateien-schutz.bericht.md`,
Audit `docs/audit/2026-11-26-neubewertung/BERICHT.md` (Abschnitt 3, K2) und `restliste.md` (Q3 erledigt). Rohlogs (`reports/T-0753/csv-fremdprogramme.log` mit `.sha256`) bleiben unverändert.
Code: Eine feste falsche „nicht installiert“-Meldung gibt es im Produkt nicht – alle Meldungen hängen an der echten Suche (`DependencyManager.ResolvePath`, die den Werkzeugordner kennt). Falsch war nur die FreeCAD-Zusage im STEP-Kommentar (s. 3).
Katalog: `docs/programme/katalog.json` enthält keine dieser Aussagen; neu erzeugt wird er mit Q5 (die neuen Testnamen kommen dann hinein) – hier nicht erzeugt.

## 5. Tests (gezielt, Zahlen aus der Ausgabe)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core `T0776_Fremdnachlauf`: 9/9 bestanden (`reports/T-0776/tests/core-T0776-gruen.txt`); Rotlauf main: 2 Fehler, 7 bestanden.
- Core `KuenstlerTests|CadTests|MeshEditTests`: 45/45 bestanden.
- App `Kuenstlerstudio_Ui` 6/6, `Studio3DMeshEditUiTests` 5/5 (`reports/T-0776/tests/app-gezielt.txt`).
- `tools/echtlauf/testschichten.py` zählt die 9 neuen Tests als „echt“ (131 → 140).

## 6. Abnahmekriterien (für dieses Querschnittspaket)
1. Leere Vorlage → echtes Ergebnis über die Oberfläche: **NICHT GETESTET** (nicht Teil des Pakets; die Dateien entstehen mit Trendos Exportcode ohne Oberfläche).
2. Fremddatei importieren/bearbeiten/exportieren, Export im Fremdprogramm: **VOLLWERTIG** für die neun Fälle (DXF-Fälle mit echtem Fremd-Import, Bearbeitung, Export), mit den Grenzen unter 7.
3. Abbruch ≤ 30 s: **NICHT BETROFFEN**.
4. KI-Fachaufgabe: **NICHT BETROFFEN**.
5. Klick bis zur echten Datei: **NICHT GETESTET** (Kern-Exporte, nicht die Knöpfe).
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **NICHT BETROFFEN**.

## 7. Nicht geprüft / offen
- **DXF-Texte in FreeCAD:** `freecadcmd` übernimmt TEXT-Elemente nicht (auch nicht mit „dxftext“); Texte nur im Log gezählt, nicht geprüft.
- **Animation in Inkscape:** Inkscape kennt kein SMIL (meldet `animate`/`animateTransform` als unbekannt) – die Bewegung ist nicht geprüft, nur Datei, Form und Hintergrund.
- **Umlaute im ZIP-Vermerk:** 7-Zip liest den ZIP-Kommentar in der Windows-Codepage, Trendo schreibt UTF-8 → „verschlÃ¼sselt“. Vorschlag: Vermerk nur in ASCII (JSON mit `\u00fc`) – nicht umgesetzt, Entscheidung offen.
- **Shopify-Beschreibung:** Trendo schreibt Umlaute dort als HTML-Zeichen (`&#246;`); Calc zeigt sie so an. Für Shopify (HTML) richtig, für eine Tabelle unschön – nur festgehalten.
- Microsoft Office, Photoshop, Fusion/SolidWorks: nicht vorhanden bzw. nicht geprüft. T-0740-Dateien in LibreOffice: nicht nachgeholt.
- FreeCAD steht nicht im Trendo-Werkzeugkatalog (`DependencyCatalog`); der Test findet es über `TRENDO_FREECAD` / `%ProgramFiles%\FreeCAD*`. Ein Katalogeintrag wäre ein eigener Auftrag.

## 8. Berührte Dateien
- Neu: `tests/Trendo.Core.Tests/T0776FremdnachlaufTests.cs`, `tools/echtlauf/t0776/freecad_pruefe.py`, `tools/echtlauf/t0776/werkzeuge_finden.ps1`, `reports/T-0776/**`, `tasks/T-0776-fremdnachlauf.md`, `tasks/T-0776-fremdnachlauf.bericht.md`
- Geändert (Code): `src/Trendo.Core/Cad/CadExport.cs`, `src/Trendo.Core/Kreativ/Kuenstler.cs`, `tests/Trendo.Core.Tests/KuenstlerTests.cs`
- Geändert (Doku): `.gitattributes` (`reports/T-0776/** -text`), `docs/audit/2026-11-26-neubewertung/BERICHT.md`, `docs/audit/2026-11-26-neubewertung/restliste.md`, `reports/T-0691/BELEG.md`, `reports/T-0694/BELEG.md`, `reports/T-0740/BELEG.md`, `tasks/T-0682-b1c-office-schutz.bericht.md`, `tasks/T-0691-3d-cad.bericht.md`, `tasks/T-0692-text-editor.bericht.md`, `tasks/T-0694-geld-rechnungen.bericht.md`, `tasks/T-0753-verkaufen-pod.bericht.md`, `tasks/T-0754-dateien-schutz.bericht.md`

Rust: nicht eingesetzt (Prüfläufe und kleine Korrekturen im bestehenden C#-Code; kein Leistungs- oder Sicherheitsvorteil).
