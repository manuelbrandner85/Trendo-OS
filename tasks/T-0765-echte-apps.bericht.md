# Bericht T-0765: X-03 „Apps zusammenklicken“ – echter App-Baukasten

**Branch:** `claude/T-0765-echte-apps` (Basis main d8df585a5 = 0.9.109), Arbeitskopie `%TEMP%\trendo-auftrag-T-0765` (nach dem Push gelöscht)

## Geänderte Dateien
- `src/Trendo.Core/PageBuilder.cs` – Seiten (`AppScreen`), Datensammlungen (`DataCollection`, `DataField`), Bausteine `form`/`datalist`/`stat`, Knopf-Ziel `seite:<id>`, Vorlage „Einkaufsliste“, App-HTML (Navigation, Abschnitte, eingebettetes Skript mit CSP-Hash, localStorage), `FromExport` (Rundreise aus ZIP/Ordner/index.html), Fassung 2 (einfache Seite bleibt Fassung 1)
- `src/Trendo.App/MainWindow.W4Builder.cs` – Seitenleiste (anlegen, wechseln, umbenennen, sortieren, löschen), Navigation in der Vorschau, Daten-Bereich (Felder: Name, Text/Zahl, Pflicht, entfernen), Ziel-/Sammlungs-/Auswertungsauswahl, Doppelklick/F2 = Text direkt, Esc, Entf, Rechtsklick-Menü, Leerzustand mit erstem Schritt, Abgrenzungssatz, „Öffnen (ZIP) …“ und Ziehen-und-Ablegen einer ZIP
- `src/Trendo.Core/PromptAction/SystemPromptAdapter.cs` – `seite.schreiben`: `Daten:` und `Seite:`; vorhandene App bleibt (einfacher Text ersetzt nur die erste Seite, App-Kennung bleibt); JSON in derselben Form wie der Baukasten
- `src/Trendo.App/MainWindow.SimpleMode.cs` – „baukasten“ nicht hinter „Mehr anzeigen“ (Designer ist die Arbeitsfläche)
- `tools/Trendo.Render/Program.cs` – `TRENDO_BUILDER_DEMO` für Bildprüfung
- Tests: `tests/Trendo.Core.Tests/T0765_AppBaukastenTests.cs` (neu, 6), `tests/Trendo.App.Tests/T0765AppBaukastenUiTests.cs` (neu, 2), `tests/Trendo.Core.Tests/T0716_HuelleE4Tests.cs` (eine Zeile: „neuere Fassung“ ist jetzt `FormatVersion + 1` statt fest 2, weil Fassung 2 jetzt gültig ist – die Prüfung selbst bleibt)
- Beleg: `reports/T-0765/` (Skript `browser-beleg.cs`, `rohlog.txt`, `SHA256SUMS.txt`, `Einkaufsliste.zip`, `index.html`, Browser- und Oberflächenbilder)

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler
- `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "…T0765|T0716_HuelleE4|PromptActionTests|Extension|Fusion|CreationSuite|NoFixedUser"` → **Bestanden!** 107/107
- `Trendo.App.Tests.exe -class T0765_AppBaukasten_Ui AppDesigner_Ui W4_X03_Ui T0716_HuelleE4_Ui W5_X04_Ui T0718WebVeroeffentlichen_Ui ClaudeDock_Ui T0201_Dateiauswahl_Ui PromptBarTests SimpleMode_Ui DesignPaket1_Ui StudiosAlle_Ui` → Total 169, Failed 0
- `dotnet run reports/T-0765/browser-beleg.cs -- reports/T-0765` → ERGEBNIS: BESTANDEN (Chrome 157, headless, eigenes Temp-Profil, Proxy/Hostauflösung gesperrt, nur file://-Anfragen, Beenden über Browser.close, sonst nur eigene PID)
- Kein Volltest (macht die Hauptsitzung).
- Rot-Grün: Die neuen Tests nutzen neue Schnittstellen und laufen auf altem Code nicht (Kompilierfehler). Während der Arbeit war `T0716_HuelleE4_Ui.Builder_cycle` rot (Neuladen erzeugte wegen der Versionsangabe einen Scheinschritt, Wiederholen wurde gelöscht) – behoben, indem die Hülle genau die gespeicherte Form vergleicht.

## Abnahme
- Einkaufslisten-App aus der Vorlage speichert Einträge und zeigt sie nach Neustart des Browsers: **echt geprüft** (rohlog: Lauf 1 PID A, Lauf 2 PID B, gleiches Profil)
- Zwei Seiten verlinkt (Knopf und Navigation): **echt geprüft** im Browser und im UI-Test
- Exportierte ZIP läuft ohne Netz im echten Browser: **echt geprüft** (2 Netzanfragen, beide file://)
- Rundreise ZIP → wieder öffnen: Core-Test, UI-Test über „Öffnen (ZIP) …“, Beleg-Skript
- Oberfläche im Bild: `reports/T-0765/oberflaeche-*.png`

## Abnahmekriterien 1–6
1. Aus leerer Vorlage echtes Ergebnis: **VOLLWERTIG** (Vorlage → funktionierende Offline-App, im Browser geprüft)
2. Fremddatei importieren/exportieren, Export im Fremdprogramm: **TEILWEISE** – Export öffnet sich in Chrome (Beleg); Import nur eigener Exporte (ZIP/Ordner/index.html mit eingebetteten App-Daten), keine fremden HTML-Seiten
3. Prozessabbruch ≤ 30 s: **VOLLWERTIG** im Rahmen der Hülle – jede Änderung wird sofort atomar gespeichert, Getipptes (auch die Direktbearbeitung) kommt in die 20-s-Notsicherung; Abbruchtest T-0716 (CrashProbe) grün; für die Direktbearbeitung kein eigener Abbruchtest
4. KI-Fachaufgabe im offenen Dokument, rückgängig: **VOLLWERTIG** (UI-Test: `seite.schreiben` → Baukasten zeigt Ergebnis → Rückgängig/Wiederholen, auch im Projekt); die KI selbst ist im Test der feste Feld-Weg, kein Modell
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** (UI-Test Vorlage-Klick → ZIP; Browser-Beleg mit ZIP aus demselben Code-Weg `Publish` + `ZipFile.CreateFromDirectory`)
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **VOLLWERTIG** – Strg+Z/Y/Umschalt+Z, F2, Esc, Entf, Rechtsklick-Menü, Bausteine ziehen, ZIP ablegen; „Einfach“ zeigt den Designer ganz

## Nicht geprüft
- Edge (nicht vorhanden), Firefox, echte Handys
- Die ZIP im Browser-Beleg ist nicht über den UI-Knopf entstanden, sondern über denselben Kern-Weg; den UI-Export prüft der UI-Test
- Ziehen und Ablegen einer ZIP aus dem Explorer (nur Code, kein Test)
- Rendering im Modus „Einfach“ im Bild: das Bild sah aus wie „Erweitert“ (Einstellung im Render-Werkzeug nicht sicher wirksam)

## Offene Punkte
- Daten bleiben pro Browser (localStorage); kein Abgleich zwischen Geräten, kein Export der Einträge (CSV) – wäre der nächste Schritt
- Sammlung löschen/umbenennen der Kennung fehlt (Name änderbar)
- Werkzeugleiste bricht bei 1600 px in zwei Zeilen um (schon vorher so)

**Status:** umgesetzt, gezielt getestet, gepusht auf `claude/T-0765-echte-apps`; Merge nach main offen (Hauptsitzung).
