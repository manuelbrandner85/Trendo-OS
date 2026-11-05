# T-0211 – Plan: Office, PDF und Netz

Stand der Sichtung: lokales `main` bei `c291903` (`feat: Prompt-zu-Auftrag-Kern für Verbesserung 42 (#241)`).
Dieser Stand ist ein Zwischenstand. Die PRs #243–#259 sind offen und ändern dieselben Registrierungsdateien.
Dieser Text plant nur. Er ändert keinen Adapter, keine Matrix und keinen Nachbau.

## 1. Was der Auftrag ist

Im Repo gibt es kein Dokument mit dem Titel „Paket 45“. Die Zuordnung steht an drei Stellen:

| Quelle | Inhalt |
|---|---|
| `tasks/T-0211-office-pdf-netz.md` | PDF-Formular, Druckvorschau, Downloads. Fertig, wenn Dialog, Felder, gespeicherte und geöffnete Kopie, Seitenbilder, Fortschritt, Anhalten/Weiter, klarer Hinweis ohne Adresse, Offline-Kopie oder PDF bzw. eine Erklärung. |
| `docs/audit/LOGIK_AUDIT.md`, Prio 10 und 11 | Seite `doku-pdf`: „Felder lesen“, „Ausgefüllt speichern“, „Vorschau“ stumm. Seiten `netz-laden` und `netz-browser`: Laden, Anhalten, Sortieren, Offline, Als PDF, Tor-holen stumm (Box offline). Arbeitspaketname: PDF/Downloads. |
| Auftragstext zu T-0211 | Studios Texte & Tabellen (SYS-10), PDF und Surfen & Herunterladen (SYS-03). Einfach, hochprofessionell, echte Dateien (DOCX/XLSX/PDF). Adapter nur über `IStudioActionAdapter`, wenn das ohne die gesperrten Dateien geht. |

`MainWindow.W45.cs` ist die gemeinsame Hülle der Wellen 4 und 5 (`InitWave45` ruft `InitSys03` und `InitSys10` auf). Das ist nicht ein eigenes Fachpaket 45.

Kataloge, bereits freigegeben und im Code als gebaut geführt:

- `docs/spec/kataloge/SYS-10_KATALOG.md` – 25 Funktionen, darunter P02 DOCX/XLSX, P17 Druckvorschau, P18 Drucken nur nach Klick, P23 Formular.
- `docs/spec/kataloge/SYS-03_KATALOG.md` – 25 Funktionen, darunter P07 Download mit Fortsetzen, P10 nur der offizielle Tor-Link, P14 Seite als PDF (A4), P18 Sortieren mit Rückgängig, P22 Offline-HTML.
- `requirements.yaml`: SYS-03 und SYS-10 stehen auf `IMPLEMENTED` / `PC_VERIFIED`. Das bleibt so. Diese Lücken sind Bedienung, kein neuer Modulstatus.
- `docs/spec/IMPROVEMENT_43.md`: echte Datei am Ende, Veröffentlichen nur nach Freigabe, lokale KI, kein Bezahl-Abo.
- `docs/improvement-42/architecture-decision.md`: Facharbeit nur in registrierten Adaptern. Dieselbe Datei sagt für die damalige Phase ausdrücklich: keine Office- und keine LibreOffice-PDF-Adapter.

Namen aus `StudioCatalog`: „Texte & Tabellen“ (SYS-10), „Surfen & Herunterladen“ (SYS-03). Keine zweiten Namen, keine zweite Navigationsebene.

## 2. Ist-Stand

Die Studios existieren. Sie dürfen nicht gelöscht und nicht durch Platzhalter ersetzt werden.

| Oberfläche | Datei | Bleibt |
|---|---|---|
| PDF, Schreiben, Ordnen | `src/Trendo.App/MainWindow.Sys10.cs` | `doku-pdf`, `doku-schreiben`, `doku-ordnen` |
| Sicher prüfen, Laden, Browser | `src/Trendo.App/MainWindow.Sys03.cs` | `netz-pruefen`, `netz-laden`, `netz-browser` |
| Ältere Dokumentseite | `src/Trendo.App/MainWindow.W1Office.cs` | Seite `office` |
| Ältere Downloadliste | `src/Trendo.App/MainWindow.W1Web.cs` | Seite `downloads` |
| Briefstudio | `src/Trendo.App/MainWindow.W1Letter.cs` | nicht Teil dieser Lücken |

Echte Dateien gibt es schon, ohne Prompt-Adapter:

- `Ooxml.NewDocx` / `Ooxml.NewXlsx` in `src/Trendo.Core/Sys/Sys10.cs`, Knöpfe „Neues Word-Dokument“ und „Neue Tabelle“.
- `PdfForm.Fields` / `PdfForm.Fill`: klassisches PDF, inkrementelle Kopie `*-ausgefuellt.pdf`, Original unverändert. Test `Sys10_Office.Form` in `tests/Trendo.Core.Tests/Sys10Tests.cs`.
- `WebTools.DownloadAsync`: Range-Fortsetzen über `.part`. Test `Sys03_Netz_Ui.DownloadResume` in `tests/Trendo.App.Tests/Sys03UiTests.cs` (lokaler `HttpListener`).
- Leere Eingaben melden sich. `tests/Trendo.App.Tests/OfficeNetzFeedbackTests.cs` erwartet Texte für `form-read`, `form-save`, `print-preview`, `dl-stop`, `page-offline`, `page-pdf`, `dl-start`, `dlsort-apply`. `Say` in `MainWindow.W45.cs` setzt die Meldung und baut die Seite neu.

Dateidialog: `MainWindow.Files.cs` hängt „Datei wählen …“ an Felder, die `PathFields.Classify` erkennt. `form-file` und `print-file` enden auf `-file`, `dlsort-folder` ist ein Ordner. Der Dialog aus T-0201 ist damit schon an diesen Feldern. T-0211 soll ihn benutzen, nicht einen zweiten Dialog erfinden.

## 3. Lücken, die der Plan schließt

### 3.1 Formular (`doku-pdf`, SYS-10-P23)

„Felder lesen“ schreibt `_formFields` und ruft `Say` auf. Der Neuaufbau malt die Textfelder aus `_formFields`. Ob die Felder sichtbar sind, ist damit wahrscheinlich schon da und muss ein UI-Test belegen, bevor jemand die Karte umbaut.

Sicher fehlt: „Ausgefüllt speichern“ ruft `PdfForm.Fill` auf und nennt den Dateinamen. `Launch` fehlt. Der Auftrag will die Kopie speichern und öffnen. Öffnen geht über das vorhandene `Launch` (`FakeLauncher` in den Tests, Audit `user.direct_launch`).

Moderne PDFs ohne klassischen `xref` wirft `PdfForm.Fill` bereits „Dieses PDF nutzt eine moderne Struktur …“. Diese Meldung bleibt. Kein zweiter PDF-Schreiber, kein stilles Überschreiben des Originals.

### 3.2 Druckvorschau (`doku-pdf`, SYS-10-P17)

„Vorschau“ zeigt Seitenzahl, Millimeter und A4-Hinweis und öffnet Sumatra oder die Datei. Seitenbilder in der Karte gibt es nicht.

Ziel: die ersten Seiten als PNG in `print-info`, über `pdftoppm` (Poppler). Fehlt `pdftoppm`, bleiben Zahl und Format, plus der Satz, dass die Bildvorschau Poppler braucht. Drucken bleibt hinter „Drucken …“ und „Ja, drucken“ (`print-ask` / `print-yes`). `PrintNow` bleibt.

Das ist nicht SYS-03-P14 (Chrome `--print-to-pdf`).

### 3.3 Download (`netz-laden`, SYS-03-P07)

Kern und Hash-Test für Abbruch plus Fortsetzen sind da. In der Oberfläche hängt der Status an `_dlStatus`, das erst beim nächsten `Say` sichtbar wird. `Say` baut die Seite neu und wirft die laufende `ProgressBar` weg. „Anhalten“ ruft `Say` sofort auf.

Ziel: während des Ladens dieselbe Karte behalten. Ein benannter Stand (`dl-status`) und `dl-progress` aktualisieren sich auf dem UI-Thread. Unbekannte Länge zeigt Bytes, nicht nur 0 %. „Anhalten“ bricht den `CancellationToken` ab, ohne die Karte vorher neu zu bauen. „Laden / Weiter laden“ bleibt der vorhandene zweite Aufruf von `DownloadAsync` (Range). Ohne Adresse und im Offline-Modus bleiben die vorhandenen Sätze.

### 3.4 Offline-Kopie und „Als PDF“ (`netz-laden`, SYS-03-P22 und P14)

`PageJob` erklärt Offline und fehlende Adresse schon. HTML schreibt `Web.OfflineCopy` in den Projektordner `webseiten`. Das bleibt.

`SavePdf` braucht Chrome, Edge, Brave oder Chromium. `Cli.Run` beendet den Prozess nach 90 s. Auf diesem Cloud-Image liegt `google-chrome` im PATH, druckt laut `AGENTS.md` US Letter (612×792 pt) und beendet sich nach `--print-to-pdf` nicht von selbst. Dieselbe Datei sagt: die App bleibt dafür unverändert. T-0211 ändert `PrintToPdfArgs` und `PrintableA4` nicht und markiert P14 nicht als bestanden.

### 3.5 Tor (`netz-browser`, SYS-03-P10)

Der Knopf prüft Offline und öffnet sonst nur `Web.TorOfficial`. Kein Bundle, kein Download. Nur prüfen, ob die Meldung im vorhandenen Test fehlt. Die Karte nicht ersetzen.

### 3.6 Was schon professionell genug ist

Schreiben und Ordnen (neues DOCX/XLSX, Serienbrief, Protokoll, Tabelle lesen) bleiben. Die älteren Seiten `office` und `downloads` bleiben. Der Audit listet dort keine stummen Knöpfe von T-0211. `MainWindow.W1Office.cs` und `MainWindow.W1Web.cs` stehen im Auftragszettel, die wirksamen Knöpfe liegen in `Sys10` und `Sys03`. Die W1-Dateien werden nicht angefasst, solange der UI-Test sie nicht als Lücke zeigt.

## 4. Adapter – erst nach dem Merge, und nur PDF-Formular

`IStudioActionAdapter` liegt in `src/Trendo.Core/PromptAction/Model.cs`. `PromptActions.CreateDefault()` auf diesem `main` gibt `[]` zurück. `tests/Trendo.Core.Tests/PromptActionTests.cs` erwartet dadurch für alle 54 Katalog-IDs `NotImplemented`.

Der Stapel hat die Registrierung schon gefüllt. Gelesen von `cursor/v42-system-fa7f` (PR #257, Basis der Übergabe #258): `CreateDefault` enthält unter anderem `LetterPromptAdapter`, `SheetPromptAdapter` (SYS-10, `tabelle.rechnen`, echtes XLSX, `SideEffectClass.None`) und `SystemActions.Create()`. SYS-03 hat dort keinen Lade-Adapter. Die Matrix-Zeile SYS-03 sagt weiter: nichts im Hintergrund laden.

PR #248 (`cursor/programme-nachbauen-2b4f`, X-10) ändert zusätzlich `src/Trendo.Core/Rebuild/Adapter.cs`, `CapabilityCatalog.cs`, `IntelligencePromptModel.cs` und die Matrix. PR #259 hängt `ArbeitsplatzAdapter` an dieselbe Liste.

Deshalb:

- Die Umsetzung von T-0211 fasst `PromptActions.CreateDefault`, `CapabilityCatalog`, `docs/improvement-42/studio-capability-matrix.md` und `src/Trendo.Core/Rebuild/` nicht an.
- Kein zweites DOCX- oder XLSX-Studio. Brief und Tabelle kommen aus #244 und #245.
- Kein Prompt-Download, kein Prompt-„Als PDF“, kein Tor aus einem Auftrag. Netz bleibt ein Klick in der Oberfläche. `SideEffectClass` hat kein „Netz“; ein stiller Lade-Adapter würde die Zeile „Nichts im Hintergrund laden“ brechen.
- Nachdem #243–#259 auf `main` liegen und die Liste stabil ist, darf ein Folgeauftrag genau eine neue Datei `src/Trendo.Core/PromptAction/PdfFormPromptAdapter.cs` anlegen (SYS-10, Aktion `formular.fuellen`, `SideEffectClass.None`, `CanPerformSideEffect` falsch) und sie mit einer Zeile an `CreateDefault` hängen. Wirkung: `PdfForm.Fill` in den Projektordner, Vorschau der Feldwerte, Beleg nur ein Test, der wirklich läuft. Die Matrix-Datei nur anfassen, wenn ein Test nach dem Merge die ganze Markdown-Tabelle aus `CapabilityCatalog.Markdown` vergleicht. Dann ausschließlich die SYS-10-Zeile, und das in der PR-Beschreibung markieren. Bis dahin bleibt der Adapter aus dem ersten Umsetzungsschritt draußen. Eine nicht registrierte Klasse wäre toter Code.

## 5. Umsetzungsplan (ein späterer Branch)

Ein Branch nach dem Merge, nicht dieser Plan-Branch. Kleine Commits, nur die genannten Dateien.

1. UI-Test für das Formular zuerst, gegen den heutigen Code. Seite `doku-pdf`, kleines klassisches PDF wie `FormPdf` in `Sys10Tests` (Textfeld „Name“, Kästchen „Zustimmung“). Pfad ins Feld `form-file`, „Felder lesen“. Erwartung: Meldung mit der Feldzahl und ein `TextBox` je Feld. Schlägt das fehl, fehlt der Neuaufbau; dann `Show` nur über das vorhandene `Say` prüfen, nicht die Karte neu schreiben. Besteht er, diese Karte nicht umbauen.
2. „Ausgefüllt speichern“: Werte aus den Feldern, `PdfForm.Fill`, Datei vorhanden, Original bytegleich, `Launch` der Kopie, Meldung mit Dateinamen. `FakeLauncher.Opened` enthält den Pfad. Leeres Formular behält den Satz „Erst „Felder lesen““.
3. Vorschau-Helfer neben `Pdf` in `src/Trendo.Core/Sys/Sys10.cs`: Argumente für `pdftoppm` (erste drei Seiten, PNG, Ausgabeordner unter dem Projekt). Die Ansicht `DokuPdfView` zeigt die Bilder in `print-info` und lässt den Text (Seiten, mm, A4) stehen. Fehlt das Werkzeug, nur der ehrliche Satz, kein externes Raten.
4. `NetLoadView`: Status und Balken in der bestehenden Karte aktualisieren. `Say` erst, wenn der Ladevorgang fertig, abgebrochen oder fehlgeschlagen ist. „Anhalten“ nur `Cancel`, solange `_dlStatus` „Lädt …“ ist; sonst der vorhandene Satz „Gerade läuft kein Download.“
5. Tor-Satz in den bestehenden Theorie-Test aufnehmen, nur wenn `Ui.Open(offline: true)` den Knopf `tor-get` noch ohne die Offline-Meldung lässt.
6. `requirements.yaml`, Katalog-Spalten und QA-Status nicht anheben. QA-017, QA-019 und Codesignatur nicht erwähnen als bestanden.

Nicht anfassen: `PromptActions.CreateDefault`, `CapabilityCatalog`, `studio-capability-matrix.md`, `src/Trendo.Core/Rebuild/`, `CleanRoom`, `MainWindow.W1Office.cs`, `MainWindow.W1Web.cs`, `PrintToPdfArgs`, `PrintableA4`, Design-41-Seiten, Briefstudio.

## 6. Testplan

Vor dem Push der Umsetzung, wie in der README und in `AGENTS.md` für den Cloud-Agenten:

- `dotnet build Trendo.sln -c Release`
- `dotnet test Trendo.sln -c Release`

Gezielt, sobald die neuen Tests existieren:

- `dotnet test tests/Trendo.App.Tests -c Release --filter OfficeNetzFeedbackTests`
- `dotnet test tests/Trendo.Core.Tests -c Release --filter Sys10_Office`
- `dotnet test tests/Trendo.App.Tests -c Release --filter Sys03_Netz_Ui`

Neue oder erweiterte Fälle, jeder mit echtem Lauf:

| Fall | Projekt | Erwartung |
|---|---|---|
| Formular lesen und speichern, Original unverändert, Launcher sieht die Kopie | `Trendo.App.Tests` | grün |
| Leere Felder, die `OfficeNetzFeedbackTests` schon hat | dasselbe | bleiben grün, nicht löschen |
| `pdftoppm` vorhanden: PNG existiert, `print-info` hat ein Bild | App- oder Core-Test | grün |
| `pdftoppm` fehlt | derselbe Test | `Assert.Skip` mit dem Werkzeugnamen. Kein `return`, das als bestanden zählt |
| Download gegen `127.0.0.1`: Balken oder Byte-Text ändert sich, Anhalten, Weiter, gleiche SHA-256 wie `DownloadResume` | `Trendo.App.Tests` | grün. Port belegt oder `HttpListener` startet nicht: `Assert.Skip`, kein erfundenes Grün |
| Offline und leere Adresse | vorhandene Tests | unverändert grün |

Bekannt und nicht als Erfolg von T-0211 werten:

- `Sys03_Pdf.Pdf` macht bei fehlendem Chrome ein stilles `return` (zählt als bestanden, ist aber kein Lauf). Liegt `google-chrome` im PATH, erwartet der Test A4 und scheitert auf diesem Image am Papierformat. App nicht anpassen. Im Bericht als bekannt offen nennen, mit der echten Ausgabe, falls der Lauf rot ist.
- `Sys10_Office` macht bei fehlendem LibreOffice oder Tesseract ebenfalls `return`. Diese alten Fälle nicht nebenbei in Skips umschreiben, außer ein neuer Test in derselben Methode entsteht. Neue Tests nutzen Skip.

Kein Netz außer dem lokalen Listener. Kein Tor-Download, kein SMTP, kein Bezahl-API, kein Release.

Manuell, wenn ein Fenster da ist: `dotnet run --project src/Trendo.App -c Release -- --no-first-run --page doku-pdf` und `--page netz-laden`. Auf der Cloud ohne Display reicht `tools/Trendo.Render` nicht als Beweis für Fortschritt und Anhalten; dafür ist der Avalonia-Headless-Test da.

## 7. Abnahme des späteren Umsetzung-PRs

- Die genannten Studios und Knöpfe sind noch die echten, keine Platzhalter.
- Formular: Felder sichtbar, Kopie gespeichert, im Test-Launcher geöffnet, Original unverändert.
- Vorschau: Seitenbilder oder ein ehrlicher Skip ohne Poppler.
- Download: Fortschritt sichtbar, Anhalten und Weiter, Hinweis ohne Adresse.
- Offline-Kopie oder „Als PDF“: Datei oder eine sichtbare Erklärung. P14-A4 auf diesem Image nicht als bestanden geführt.
- `CreateDefault`, Matrix und `Rebuild/Adapter.cs` ohne Diff. Steht doch eine Zeile in `CreateDefault`, ist sie in der PR-Beschreibung als Kollision mit #243–#259 markiert und auf den Folgeauftrag nach dem Merge beschränkt.
- Drei Testprojekte gelaufen. Zahlen und der bekannte Chrome-Fall stehen im Bericht `tasks/T-0211.bericht.md`. Nur gelaufene Tests. Nichts mergen.

## 8. Dieser Planungsstand

Geändert ist nur diese Datei. Build und Tests sind dafür nicht gelaufen. Offen bleibt die Umsetzung selbst, und sie wartet, bis der Merge-Lauf von #243–#259 `main` verlassen hat.
