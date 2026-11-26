# T-0776 – Fremdprogramm-Nachläufe (Restliste Q3)

Trendo schreibt echte Dateien mit seinem eigenen Exportcode; das Fremdprogramm öffnet sie per Kommandozeile und liest sie zurück.
Test: `tests/Trendo.Core.Tests/T0776FremdnachlaufTests.cs` (Klasse `T0776_Fremdnachlauf`, 9 Fälle „T-0776 echt: …“). Nichts ist ersetzt.
Jedes Fremdprogramm lief mit eigenem Profil in einem eigenen Temp-Ordner (LibreOffice `-env:UserInstallation`, Krita/Inkscape über `USERPROFILE`/`APPDATA`/`LOCALAPPDATA`
bzw. `INKSCAPE_PROFILE_DIR` + `XDG_*`, FreeCAD über `FREECAD_USER_HOME/DATA/TEMP`). Unter `%LOCALAPPDATA%\TrendoOS` wurde nichts geschrieben; in den Programmordnern
(Trendo-Werkzeugordner, `C:\Program Files\FreeCAD 1.1`) hat sich keine Datei geändert (Prüfung über Änderungszeit nach dem Lauf).
Pfade ohne Benutzernamen (`C:\Users\<user>`). Prüfsummen aller Dateien: `SHA256SUMS.txt`.

| Ordner | Studio | Fremdprogramm | Was geprüft ist |
|---|---|---|---|
| `werkzeuge.txt` | – | – | Fundorte (nur lesend) und Versionen: LibreOffice 26.2.4, Krita 6.0.4, Inkscape 1.4.4, 7-Zip 26.05 im Trendo-Werkzeugordner; FreeCAD 1.1.4 und 7-Zip in `C:\Program Files`; nichts davon im PATH. `C:\Program Files\LibreOffice\program` enthält nur `logs` – daher die falschen „nicht installiert“-Aussagen in T-0692/T-0694/T-0740/T-0753. Skript: `tools/echtlauf/t0776/werkzeuge_finden.ps1` |
| `libreoffice-text/` | SYS-10 Texte | LibreOffice Writer | Trendo-DOCX und -ODT → Text (Titel, Überschrift, Absatz mit Umlauten, Liste, Tabelle) und PDF (2 Seiten wegen Seitenumbruch) |
| `libreoffice-geld-xlsx/` | SPEC-11 Geld | LibreOffice Calc | Trendo-Auswertung (XLSX) → CSV je Blatt: 3 Blätter, Calc rechnet die Formeln selbst (Überschuss 1035, Monatssumme 1297 / 66,90 / 1230,10) |
| `libreoffice-shop-csv/` | Online verkaufen | LibreOffice Calc | Shopify-CSV Artikel/Kunden/Bestellungen: Spaltenzahl je Zeile, Preise, Bestand, Umlaute, Adresse; „=1+1“ bleibt Text `'=1+1` (Formel-Einschleusung entschärft) |
| `freecad-step/` | CORE-05 CAD | FreeCAD 1.1.4 | Trendo-STEP (Quader + Winkel mit Tasche): 2 gültige geschlossene Körper, Volumen 6000 / 4625 mm³, Umriss 60×20×20, Namen „Quader“, „Winkel“ |
| `freecad-cad-dxf/` | CORE-05 CAD | FreeCAD 1.1.4 | fremde DXF → Trendo bearbeitet → DXF: Ebenen, 10 Kanten (Polylinie, Kreise, Bogen, Linien), bearbeitete Linie bis x = 70. Texte übernimmt FreeCAD ohne Oberfläche nicht – **nicht geprüft** |
| `freecad-wohnung-dxf/` | SPEC-01 Wohnung | FreeCAD 1.1.4 | fremde Wohnungs-DXF → Wand dicker → Trendo-DXF: FreeCAD übernimmt `$INSUNITS` = cm (×10 in mm) und den Umriss auf 1 mm genau wie Trendos eigener Leser |
| `krita-psd-ora/` | Künstlerstudio | Krita 6.0.4 | Trendo-PSD und -ORA → Krita speichert ORA und PNG: Ebenennamen (mit Umlaut), Sichtbarkeit, Hintergrund; Farben an 4 Punkten gleich wie Trendos Gesamtbild |
| `inkscape-svg/` | Logos / Animation | Inkscape 1.4.4 | Logo-SVG: Objekte mit Trendo-Kennung, Lage 20/30/120/60, PNG-Farben wie im Logo. Animations-SVG: Inkscape liest die Datei und die Form (40×40), Hintergrund stimmt; SMIL-Bewegung kennt Inkscape nicht – Bewegung **nicht geprüft** |
| `7zip-zip/` | Daten schützen, Trendo erweitern | 7-Zip 26.05 | Sicherungs-ZIP und Erweiterungs-Export-ZIP: `7z t` „Everything is Ok“, Liste mit CRC, entpackt bitgleich zum Original (SHA-256 je Datei); Vermerk „nicht verschlüsselt“ sichtbar (Umlaute im ZIP-Kommentar zeigt 7-Zip falsch an) |
| `rot/` | – | – | Derselbe Test auf main 2b1ffa923: **2 Fehler** – FreeCAD las Trendos STEP als zwei leere Schalen ohne Fläche (`FACE` statt `FACE_SURFACE`), Krita las die PSD-Ebenennamen mit Füllbytes (`Skizze\0`) und ohne Hintergrund |
| `tests/` | – | – | grün nach der Korrektur: T-0776 9/9, Künstler/CAD/Netz 45/45, App `Kuenstlerstudio_Ui` 6/6, `Studio3DMeshEditUiTests` 5/5; ohne Werkzeuge (leerer Werkzeugordner, leerer Programme-Ordner, kurzer PATH) 9/9 „übersprungen“ mit „BLOCKIERT – fehlt auf diesem PC: …“ |

Neu erzeugen: `T0776_WORK=<eigener Temp-Ordner> TRENDO_BELEG_DIR=<Ordner> dotnet test tests/Trendo.Core.Tests -c Release --filter "FullyQualifiedName~T0776_Fremdnachlauf"`.
