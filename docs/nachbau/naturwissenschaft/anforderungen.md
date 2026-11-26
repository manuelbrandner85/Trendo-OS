# Naturwissenschaft – Anforderungen (Clean-Room)

Studio `STUDIO-SCIENCE-01`, Seite `naturwissenschaft-programm`, Kategorie Wissen & Lernen. Freigabe: 79 neue Studios am 09.11.2026, Etappe Wissen & Lernen am 26.11.2026 (T-0781).

## Vorbilder (nur Bedienung und Funktionsumfang, kein Code, keine Texte, keine Farben oder Symbole übernommen)

| Vorbild | Was wir uns ansehen | Lizenz / Zugang | Quelle (abgerufen 26.11.2026) |
|---|---|---|---|
| Ptable | Periodensystem als Raster, Einfärben nach Art oder nach Eigenschaftswert, Detailfeld je Element, Suche | proprietär, Marke („Updated Oct 3, 2024“) | ptable.com |
| ChemDoodle 2D | Molmasse, Summenformel, Elementaranalyse (Massenanteile), Stöchiometrie | kommerziell (v12.12.1) | chemdoodle.com |
| PhET „Balancing Chemical Equations“ | Faktoren ändern, bis die Atombilanz stimmt; Rückmeldung „ausgeglichen“ | Simulationen bis 29.03.2026 CC BY 4.0, danach eigene Vereinbarung – nur Bedienvorbild | phet.colorado.edu (Lehrerhandreichung, Lizenzseite) |
| WebQC Equation Balancer | strenge Schreibweise (Co ≠ CO), Ladungen, Knopf „Ausgleichen“, Stöchiometrie-Tabelle | proprietär (© 2026) | webqc.org/balance.php |

## Daten und Lizenzen

| Daten | Quelle | Lizenz / Umgang |
|---|---|---|
| Atomgewichte | IUPAC/CIAAW „Standard Atomic Weights“: gekürzte Werte (Bericht 2021) mit den Änderungen vom 23.10.2024 für Zr (91,222), Gd (157,25), Lu (174,97) | Zahlenwerte als Fakten, eigene Tabelle in `Elemente.cs`; keine Texte übernommen |
| Massenzahl radioaktiver Elemente | langlebigstes bekanntes Isotop, in eckigen Klammern, als „geschätzt“ markiert | eigene Tabelle |
| Elektronegativität | Pauling-Werte (übliche Tabellenwerte); Pm, Eu, Tb, Yb, Lr ohne Angabe | eigene Tabelle |
| Elektronenkonfiguration | aus der Madelung-Regel berechnet, 20 bekannte Ausnahmen (Cr, Cu, Nb … Lr) | berechnet |
| Konstanten | CODATA 2022 (NIST Version 9.0, 09.05.2024) bzw. exakt seit der SI-Revision 2019 | Zahlenwerte |
| Abgleich | PubChem Periodic Table (CSV) – **nur zur Prüfung**, nicht als Datenquelle (Lizenz uneinheitlich, data.gov nennt ODbL) | Beleg `reports/T-0781/elemente-abgleich.txt` |

Verworfen: UnitsNet (MIT-0) – schon für die Alltags-Einheiten (`Units` in `Sys02.cs`) geprüft und verworfen (2,6 MB, keine deutschen Namen); Dimensionsprüfung mit Unsicherheiten braucht ohnehin eigenen Code. NIST SRD 144 (Atomgewichte) ist nicht gemeinfrei und veraltet (2015). BODR (Expat) nicht nötig.

**Rust-Prüfung (CLAUDE.md):** kein Rust – Tabellen und Rechnungen sind klein (118 Elemente, Gleichungssysteme bis 10 Stoffe), alles läuft in Millisekunden im .NET-Prozess.

## Vorhandenes genutzt
- Fehlerfortpflanzung nach Gauß aus „Messdaten & Auswertung“ (`StatsPlus.Propagate`, SPEC-05) rechnet die Unsicherheit jeder Physik-Rechnung.
- Messreihen (Statistik, Regression, Tests) bleiben im Programm `wissenschaft`; Menüband „Messwerte → Messreihe auswerten …“ öffnet es.
- Termleser und Brüche des Mathe-Helfers (Formeln, exakte Ausgleichsrechnung), `Rechenblatt` (PDF/Markdown), `AtomicFile`, Programm-Hülle, KI-Kette mit `ProgramSpec.KiActions`.

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Vorbild | Stand |
|---|---|---|---|---|---|
| NW-01 | Periodensystem | 118 Elemente im 18er-Raster, Lanthanoide/Actinoide in der Fußzeile, Ordnungszahl + Symbol je Feld, gewähltes Element mit Goldrand | Core `NaturKernTests.Vollstaendig`, UI `Natur_klick_bis_datei`; Bild | Ptable | erfüllt |
| NW-02 | Elementdaten | Name, Masse, EN, Periode/Gruppe/Block, Art, Zustand bei 25 °C, Konfiguration (hochgestellt) | Core `Stichproben`, `Neu2024`, `Abgleich_pubchem` (118 Elemente, Masse/EN/Konfiguration) | Ptable | erfüllt |
| NW-03 | Einfärben | nach Art (10 Arten mit Legende), nach Elektronegativität, nach Atommasse | Bild `reports/T-0781/bilder/` | Ptable | erfüllt |
| NW-04 | Suche | Name, Symbol oder Ordnungszahl → Element gewählt | Core `Stichproben` (`Elemente.Finde`) | Ptable | erfüllt |
| NW-05 | Summenformel lesen | Klammern ( ) und [ ], Kristallwasser ·5H2O, Ladung ^2-, Fehler bei falscher Groß-/Kleinschreibung | Core `Molmasse`, `Formel_fehler` | WebQC | erfüllt |
| NW-06 | Molmasse | Rechenweg (Atome zählen, Anzahl × Masse, Summe), Massenanteile in %, Probe: Anteile = 100 %, Zeilen = Summe | Core `Molmasse` (7 Stoffe) | ChemDoodle | erfüllt |
| NW-07 | Reaktionen ausgleichen | Atombilanz als Gleichungssystem, exakt gelöst, kleinste ganze Zahlen; Ionen mit Ladungsbilanz; Probe je Element, Ladung und Masse | Core `Ausgleichen` (7 Reaktionen, auch Redox KMnO₄/HCl und MnO₄⁻/Fe²⁺) | PhET, WebQC | erfüllt |
| NW-08 | Unmögliches ehrlich | nicht ausgleichbar / mehrdeutig / nur mit negativen Faktoren → klare Meldung | Core `Unmoeglich` | WebQC | erfüllt |
| NW-09 | Rechnen mit Einheiten | „3 km / 20 min“, „100 km/h in m/s“, Vorsätze (k, M, m, µ …), °C/°F nur als Einzelwert; Rückrechnung als Probe | Core `Einheiten_rechnen` (10 Fälle) | – | erfüllt |
| NW-10 | Dimensionsprüfung | Meter + Sekunde, kg in m, sin(3 m), unbekannte Einheit → Meldung mit Größenname | Core `Dimension_fehler` | – | erfüllt |
| NW-11 | Formelsammlung | 27 Formeln (Mechanik, Elektrizität, Wärme, Wellen, Optik, Chemie), nach jeder Größe umgestellt, Konstanten vorbelegt | Core `Formeln_konsistent` (jede Formel dimensionsrichtig, jede Umstellung mit Zufallswerten gegengerechnet) | Formelsammlungen der Schule | erfüllt |
| NW-12 | Physik-Rechnung | Werte in beliebigen passenden Einheiten, in SI rechnen, Ergebnis in Wunsch-Einheit, Dimensionsprüfung, Unsicherheit nach Gauß; Probe: Einsetzen in die Ausgangsgleichung | Core `Beispiel`; UI `Natur_klick_bis_datei` | PhET | erfüllt |
| NW-13 | Laborheft | jede Rechnung als Eintrag (immer frisch aus dem Kern gerechnet), ✓/!, Rechenweg des gewählten Eintrags, Entf und Rechtsklick „Löschen“ | UI `Natur_klick_bis_datei` | – | erfüllt |
| NW-14 | Export | Laborheft als PDF, Ergebnisse als CSV, Markdown, Periodensystem als CSV (Semikolon, Dezimalkomma) | UI `Natur_klick_bis_datei`; Beleg: CSV in LibreOffice zu XLSX, PDF in pdftotext | Ptable (PDF drucken) | erfüllt |
| NW-15 | KI: Textaufgabe → Eintrag | „natur.rechnung“ (Molmasse, Reaktion, Einheiten, Physik): KI nennt nur Aufgabe/Formel/gesuchte Größe; Faktoren der KI abgelehnt; Vorschau, „Ja, behalten“, ein Rückgängig-Schritt | UI `Natur_ki_textaufgabe`; Echtlauf `Real_local_ki` | – | erfüllt |
| NW-16 | KI-Auftrag ohne offenes Dokument | Adapter „natur.rechnen“ (Molmasse, Reaktion, Einheiten) als Markdown | Core `WissenAdapterTests` | – | erfüllt |
| NW-17 | Strukturformeln, 3D-Moleküle, Simulationen | nicht gebaut (ChemDoodle/Avogadro/PhET-Simulationen) | – | ChemDoodle, PhET | fehlt (nicht im Auftrag) |
| NW-18 | Isotope, Temperaturregler fest/flüssig/gasförmig | nicht gebaut (keine geprüften Schmelz-/Siedepunkte in der eigenen Tabelle) | – | Ptable | fehlt |
