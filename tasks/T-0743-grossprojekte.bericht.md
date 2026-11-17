# T-0743 – Bericht: Großprojekte, Karte an Studio übergeben, Ergebnis zurück

**Auftrag:** `tasks/T-0743-grossprojekte.md` (Restliste D7 Rang 18, X-01) · **Zweig:** `claude/T-0743-grossprojekte` (eigene Kopie von `main` a90e697aa in `%TEMP%\trendo-auftrag-T-0743`)

## Vorher → Nachher

| Punkt | Vorher (Beleg) | Nachher | Beweis |
|---|---|---|---|
| Karte tut etwas | „Zuständige KI-Rolle“ nur Etikett, keine Karte wird abgearbeitet (audit-4 X-01 Nr. 12, `MainWindow.W5Fusion.cs` alt) | Karte bekommt ein Studio (alle Programme der Programm-Hülle). „An Studio übergeben“ (golden, Strg+Enter, Rechtsklick) schreibt den Auftrag `grossprojekt/auftraege/<Karte>.md`, trägt ihn als Übergabe `auftrag` (X-01 → Studio) ein und öffnet das Programm mit vorbereitetem Dokument, gespeichert im Projekt | `GrossprojektUebergabe_Ui.Text_to_pdf_chain` (echt), `BigBoardHandoffTests.Handover_and_return` |
| Ergebnis zurück | Belege nur von Hand per Pfad (audit-4 Nr. 4) | Jeder Export des Dokuments (Menüband/Menü der Hülle oder Claude „exportieren“) bringt die Datei zurück: Kopie ins Projekt mit SHA-256, Übergabe `ergebnis` (Studio → X-01, angenommen), Auftrag angenommen, Status „erledigt“, Spalte „Fertig“, Vorschau (PDF-Seite, Bild oder Text). Von Hand: Datei wählen + „Als Ergebnis übernehmen“ oder Datei auf die Karte ziehen | `Text_to_pdf_chain`, `Free_chain_and_handling` (Ablegen), `Return_after_restart` (nach Neustart über den Dokumentpfad) |
| Ketten frei | 4 fest verdrahtete Ketten (audit-4 Nr. 2) | Kette = Reihenfolge der Karten: anlegen, Studio wählen, „↑ Früher/↓ Später“, Rechtsklick, Alt+↑/↓; Zeile „Kette: 1. … → 2. …“. Eingabe einer Karte ist das Ergebnis der Karte davor. Die 4 Abläufe bleiben als Reiter und als Vorlage; aus einer Vorlage bekommen die Schritte ihr Programm, wo es eines mit dem Namen gibt (z. B. Bildbearbeitung, Spiele bauen) | `Free_chain_and_handling`, `BigBoardHandoffTests.Chain_input_and_reorder`; `W5_X01_Ui`, `StudioUebergabe_Ui` weiter grün |
| Kein zweites Register | – | Tafel im `ProjectStore` (wie bisher), beide Richtungen über `ProjectHandoff` (`.trendo/uebergaben.json`), Dokumente über `ProgramNew/ProgramSave/ProgramOpen` der Hülle | Code: `BigBoardHandoff.cs`, `MainWindow.GrossprojektUebergabe.cs` |
| Alte Tafeln | – | neue Felder optional, alte `tafel.json` bleibt lesbar | `BigBoardHandoffTests.Old_board_reads` |

Echtbeleg Text → PDF: `reports/T-0743/` (BELEG.md, Rohlog, SHA256SUMS). Bücher schreiben (TextPdf) → PDF-Werkstatt (Seitenzahlen über PdfTools); beide PDFs mit dem Fremdprogramm `pdftotext` (xpdf 4.06) gelesen: Text der Karte und „Seite 1 von 2 / Seite 2 von 2“, exit=0. Oberfläche im Bild nachgeprüft (`tafel-erledigt.png`, `tafel-offen.png`): eine goldene Hauptaktion je Zustand („An Studio übergeben“ bzw. „Ergebnis öffnen“; „＋ Neues Großprojekt“ ist bei gewählter Karte nicht mehr golden).

## Abnahmekriterien

1. Aus leerer Vorlage echtes Ergebnis über die Oberfläche: **VOLLWERTIG** für dieses Paket – leere Tafel → zwei Karten → zwei echte PDFs, Klickweg im Test.
2. Fremddatei importieren/bearbeiten/exportieren, Export im Fremdprogramm: **TEILWEISE** – Export (PDF) im Fremdprogramm `pdftotext` geöffnet; ein Import eines fremden Projektplans gibt es weiter nicht.
3. Absturz ≤ 30 s: **TEILWEISE** – Tafel schreibt jede Änderung atomar (`ProjectStore.WriteFile` → `AtomicFile`), das vorbereitete Dokument liegt sofort gespeichert im Projekt und die Hülle sichert weiter im 20-s-Takt; nach Neustart kommt der Export zurück (`Return_after_restart`). Ein echter Prozessabbruch wurde nicht getestet. `ProjectHandoff` schreibt `uebergaben.json` weiterhin nicht atomar (gemeinsame Datei, nicht geändert).
4. Claude/KI führt Fachaufgabe aus, Ergebnis im Dokument, rückgängig: **TEILWEISE** – Claude „exportieren“ bringt das Ergebnis jetzt ebenfalls an die Karte zurück (eine Zeile in `ClaudeBridge.cs`), der Auftrag ist für Claude über `uebergaben` sichtbar; nicht mit echtem Claude-Lauf geprüft. Eine KI-Aktion „aus einem Ziel Karten anlegen“ fehlt weiter.
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** – `Text_to_pdf_chain` (echte Studios, echte Exporte, nichts `*Override`).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen und Ablegen: **TEILWEISE** – Strg+Enter, Alt+↑/↓, Rechtsklick-Menü auf Karten, Dateien auf eine Karte ziehen (Ergebnis bzw. Beleg), Karten ziehen wie bisher; die Tafel hat keine eigene Einfach/Profi-Unterscheidung.

## Tests (ausgeführt, Zahlen aus der Ausgabe)

- Build `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core: `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "FullyQualifiedName~BigBoard|FullyQualifiedName~W5Fusion|FullyQualifiedName~ProjectHandoff|FullyQualifiedName~ProjectStore"` → Bestanden, 9/9; nach der letzten Änderung `~BigBoard` → 6/6.
- App: `Kanban_Ui, W5_X01_Ui, StudioUebergabe_Ui, PdfWerkstatt_Ui, KreativStudios2_Ui, T0725_Fachstudios_Ui, ReferenzStudioSpiele_Ui, GrossprojektUebergabe_Ui, ClaudeBridge_Ui, BueroAusbau_Ui` → Total 72, Failed 0. Nach der letzten Änderung `GrossprojektUebergabe_Ui, Kanban_Ui` → Total 4, Failed 0.
- Rot-Grün: die neuen Tests nutzen `BigBoardHandoff`, `BigHandOver`, `big-handover` … – auf dem alten Stand kompilieren bzw. finden sie nichts (rot); auf dem neuen grün.
- Kein Volltest (Vorgabe: macht die Hauptsitzung).

## Nicht geprüft

- Echter Claude-Lauf (Abo) mit „exportieren“ an einer übergebenen Karte.
- Echter Prozessabbruch während einer Übergabe.
- Rückweg für Studios außerhalb der Programm-Hülle (Trendo Text, Tabelle, PDF-Ansicht …): diese sind nicht in `Programs` und deshalb nicht als Kartenstudio wählbar; dort bleibt nur „Als Ergebnis übernehmen“/Ablegen von Hand.
- Vorbereitung des Dokuments ist nur für „Bücher schreiben“ (Titel + Aufgabe + Text-Eingabe) und „PDF-Werkstatt“ (Eingabedatei) fachlich; andere Programme bekommen Titel, Auftrag als Übergabe und den Rückweg.
- Die 4 Ablauf-Vorlagen selbst wurden nicht mit FFmpeg/Blender neu belegt.

## Offene Punkte

- `ProjectHandoff.Save` schreibt nicht atomar (gemeinsame Datei `src/Trendo.Core/W2/ProjectHandoff.cs`, bewusst nicht angefasst).
- Lauf einer ganzen Kette im Hintergrund mit Fortschritt/Abbrechen, KI-Aktion „Ziel → Karten“, Export der Tafel (PDF/CSV) – weiter offen (audit-4 Punkte 3–5).
- Weitere Programme mit fachlicher Vorbereitung (Bildbearbeitung, Spiele bauen …) über `BigPrepare` ergänzen.

## Berührte Dateien

- neu: `src/Trendo.Core/BigBoardHandoff.cs`, `src/Trendo.App/MainWindow.GrossprojektUebergabe.cs`, `tests/Trendo.Core.Tests/BigBoardHandoffTests.cs`, `tests/Trendo.App.Tests/GrossprojektUebergabeUiTests.cs`, `reports/T-0743/*`, `tasks/T-0743-grossprojekte*.md`
- geändert: `src/Trendo.Core/BigBoard.cs` (Felder der Karte), `src/Trendo.App/MainWindow.W5Fusion.cs` (Tafel)
- gemeinsam, je eine Zeile: `src/Trendo.App/MainWindow.ProgramShell.cs` (Export → `BigExported`), `src/Trendo.App/MainWindow.ClaudeBridge.cs` (Claude „exportieren“ → `BigExported`), `tools/Trendo.Render/Program.cs` (`TRENDO_BIG_DEMO` für die Bildprüfung)

## Entwicklungsbericht

- Sprachen: C# (.NET 10, Avalonia). Rust eingesetzt: NEIN – Dateiübergabe und Oberfläche, kein Rechen- oder Speicherengpass; vorhandener C#-Code wird erweitert.
- Status: umgesetzt, gezielt getestet, nicht gemergt.
