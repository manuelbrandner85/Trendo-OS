# T-0223: Funktionsumfang der Vorbilder (REA) und Auswahl für Trendo Office

**Stand:** 2026-10-25, Schritt 1 und 2 aus `tasks/T-0223-trendo-office.md`. Noch nichts gebaut.
**Vorbilder:** die mitgelieferten Fassungen aus dem Vollpaket (`artifacts/vollpaket/staging/`): LibreOffice 26.2.4, SumatraPDF 3.6.1, qpdf 12.4.2.

## Was ermittelt wurde (Rohbelege in diesem Ordner)
| Vorbild | Weg | Ergebnis | Belegart |
|---|---|---|---|
| SumatraPDF.exe | REA `inspect_pe_resources` (`ev_4e83b428…`) → Ressource RCDATA 2 an den gemeldeten Dateistellen gelesen | 555 Bedientexte, davon 125 Menüpunkte mit Tastenkürzel (`sumatra-bedientexte.json`, Englisch + Deutsch) | beobachtet (ausgelieferte Datei) |
| qpdf30.dll | REA `search_strings` mit Ghidra 12.1.4 (`ev_a448a32d…`) | 37 Befehle der PDF-Werkzeuge bestätigt (Seiten, Teilen, Drehen, Ver-/Entschlüsseln, Überlagern, Anhänge, Seitenbeschriftungen, Prüfen, Komprimieren …) | beobachtet (statisch) |
| LibreOffice Writer/Calc | Menüdateien `soffice.cfg/modules/{swriter,scalc}/menubar/menubar.xml` gelesen (sha256 `8b9416e6…`, `5aeccb00…`) | Writer 492 Menübefehle in 11 Menüs, Calc 425 in 11 Menüs (`libreoffice-menues.json`) | beobachtet (ausgelieferte Datei) |

**Unbekannt / nicht ausgewertet:** RCDATA 3 von SumatraPDF (LzSA1-komprimiert); Calc-Tabellenfunktionen (Formelliste) und Dialoginhalte von LibreOffice; das tatsächliche Verhalten der Befehle (nichts davon wurde gestartet). Die deutschen Menünamen von LibreOffice sind noch nicht zugeordnet.

**Abgeleitet:** Der Großteil der Befehle (Formulare, Datenbanken, Makros, Hangul/Chinesisch, Fernzugriff, Spenden, 3D-Objekte) wird im Alltag selten gebraucht. Ein vollständiger 1:1-Nachbau (≈ 900 Befehle plus Dateiformate) ist nicht in einem Schritt machbar. Darum folgt der Nachbau in Stufen.

## Was Trendo heute schon selbst kann
`src/Trendo.Core/Sys/Sys10.cs`: Word-Datei (.docx) neu anlegen und Text lesen, Excel-Datei (.xlsx) anlegen und lesen. `Letters.cs`: Briefe als .docx schreiben (DIN 5008). Darauf wird aufgebaut – kein zweites Office-Modul.

## Vorschlag Stufe 1 (zur Freigabe)

### Trendo Text (Vorbild Writer)
| Nr. | Funktion | Vorbild-Befehle |
|---|---|---|
| T1 | Neu, Öffnen, Speichern, Speichern unter (.docx, .odt), zuletzt geöffnete Dateien | NewDoc, OpenFromWriter, Save, SaveAs, RecentFileList |
| T2 | Rückgängig, Wiederholen, Ausschneiden, Kopieren, Einfügen (auch ohne Formatierung), alles markieren | Undo, Redo, Cut, Copy, Paste, PasteUnformatted, SelectAll |
| T3 | Fett, Kursiv, Unterstrichen, Durchgestrichen, Hoch-/Tiefgestellt, Schriftgröße, GROSS/klein | Bold, Italic, UnderlineSingle, Strikeout, SuperScript, SubScript, Grow, Shrink, ChangeCase… |
| T4 | Absatzarten: Titel, Untertitel, Überschrift 1–3, Text, Zitat; Aufzählung, Nummerierung; links/mittig/rechts/Blocksatz | TitleParaStyle, Heading1–3ParaStyle, TextBodyParaStyle, QuoteParaStyle, … |
| T5 | Tabelle einfügen, Zeile/Spalte einfügen und löschen | InsertTable, InsertRows…, DeleteRows/Columns |
| T6 | Bild einfügen, Seitenumbruch | InsertGraphic, InsertPagebreak |
| T7 | Suchen und Ersetzen, Wörter zählen | SearchDialog, WordCountDialog |
| T8 | Rechtschreibprüfung Deutsch, offline | SpellingAndGrammarDialog, SpellOnline |
| T9 | Als PDF speichern, Drucken | ExportDirectToPDF, Print |
| T10 | Vorlagen: Privat- und Geschäftsbrief DIN 5008 (vorhanden), Lebenslauf, Rechnung | OpenTemplate |

### Trendo Tabelle (Vorbild Calc)
| Nr. | Funktion | Vorbild-Befehle |
|---|---|---|
| C1 | Neu, Öffnen, Speichern (.xlsx, .ods lesen), CSV ein- und ausgeben | NewDoc, OpenFromCalc, Save, SaveAs |
| C2 | Zellen bearbeiten, Zeilen/Spalten einfügen und löschen, mehrere Blätter | InsertRows…, DeleteRows…, Insert (Blatt) |
| C3 | Formeln mit Zellbezügen (A1, A1:B5): + − × ÷, SUMME, MITTELWERT, MIN, MAX, ANZAHL, WENN, RUNDEN, HEUTE und ≈ 20 weitere Alltagsfunktionen | (Formelzeile) |
| C4 | Sortieren auf-/absteigend, Filter | DataSort, SortAscending, DataFilterAutoFilter |
| C5 | Zahlenformate: Zahl, Euro, Prozent, Datum | (Format Zellen) |
| C6 | Fett, Füllfarbe, Rahmen, Spaltenbreite | Bold (Füllfarbe und Rahmen nur über „Zellen formatieren“, nicht als eigener Menübefehl) |
| C7 | Diagramm: Balken, Linie, Kreis | InsertObjectChart |
| C8 | Als PDF speichern, Drucken | ExportDirectToPDF, Print |

### Trendo PDF (Vorbilder SumatraPDF + qpdf)
| Nr. | Funktion | Vorbild |
|---|---|---|
| P1 | Ansehen: blättern, erste/letzte Seite, gehe zu Seite, Zoom (Seite, Breite, Ist-Größe), fortlaufend, Doppelseite, drehen, Vollbild | SumatraPDF: Next/Previous/First/Last Page, Go to page, Fit Page/Width, Actual Size, Facing, Rotate, Fullscreen |
| P2 | Suchen im PDF, Text kopieren, Lesezeichen anzeigen | Find, Copy Selection, Show Bookmarks |
| P3 | Zusammenfügen, Seiten auswählen, Teilen, Seiten drehen | qpdf: pages, split-pages, rotate, collate, empty |
| P4 | Passwort setzen und (bei eigenen Dateien) entfernen, Infos (Seitenzahl, verschlüsselt?) | qpdf: encrypt, decrypt, show-npages, is-encrypted, requires-password |
| P5 | Markieren, Unterstreichen, Notiz (Anmerkungen) | SumatraPDF: Highlight, Underline, Strike Out, Free Text |
| P6 | Formulare ausfüllen | qpdf: generate-appearances, flatten-annotations |

**Nicht in Stufe 1:** Makros, Datenbanken, Formular-Entwurf, Serienbriefe, Pivot-Tabellen, Folien (Impress), Zeichnen (Draw), Formeleditor, Online-Übersetzen und -Suchen aus den Menüs.

## Technik (Vorschlag, freie Lizenzen)
- **Dateiformate:** Ausbau von `Sys10` (DOCX/XLSX selbst, ohne Fremdbibliothek wie bisher); ODT/ODS lesen.
- **PDF ansehen:** PDFium über die .NET-Bibliothek PDFtoImage (MIT, PDFium BSD-3/Apache-2.0); PDF bearbeiten (Zusammenfügen, Teilen, Drehen, Passwort) mit PDFsharp 6 (MIT). Beide müssten als NuGet-Paket neu dazukommen – dafür frage ich vorher.
- **Rechtschreibung:** deutsches Hunspell-Wörterbuch aus dem LibreOffice-Paket (Lizenz GPL/LGPL/MPL, wird beim Einbau geprüft) mit WeCantSpell.Hunspell (MIT).
- **Text-Editor:** Avalonia hat keinen fertigen Editor für formatierten Text. Stufe 1 baut einen eigenen absatzbasierten Editor (Absatzarten, Hervorhebung innerhalb eines Absatzes, Tabellen, Bilder). Das ist der aufwendigste Teil.
- **Kein fremder Programmcode übernommen**, keine Namen oder Symbole der Vorbilder.
