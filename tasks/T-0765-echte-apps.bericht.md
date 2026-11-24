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

## Nacharbeit nach der unabhängigen Gegenprüfung (Stand nach 51939ff1c)
Kopie `%TEMP%\trendo-auftrag-T-0765-fix1`, neuer Commit auf demselben Zweig (ohne Force).

| Befund | Änderung | Test / Beleg |
|---|---|---|
| M1 Kennungen unmaskiert (→ echtes `<meta refresh>`) | `SitePage.Normalize` (beim Lesen jeder Datei): Seiten-, Sammlungs- und Feld-Kennungen werden eindeutige Slugs `[a-z0-9-]`, Verweise (Data, `seite:`, `summe:`) folgen, App-Kennung geprüft; alle Attributwerte über `E()`; Skript liest Felder über `form.elements.namedItem` statt zusammengesetztem Selektor | Core `T0765_GegenpruefungTests.M1_kennungen_maskiert`; echter Chrome Lauf 3: präparierte App bleibt auf file://, kein `<meta refresh>` im DOM, Formular speichert |
| M2 „Zeit“ vom Zeitstempel überschrieben | Zeitstempel unter `_t` (Slugs enthalten nie `_`); alte Einträge bleiben lesbar | Core `M2_zeit_feld`; Chrome: „Arzt · Zeit: 18 Uhr · Betrag: 12“, gespeichert mit `_t` |
| M3 KI ersetzt vorhandene App | `SitePage.Absorb`: KI-Seiten/-Sammlungen werden angehängt, vorhandene bleiben unverändert, Kennungen eindeutig, App-Kennung bleibt | Core `M3_ki_haengt_an` (Vorlage + `Daten:`/`Seite:` über den echten Workflow) |
| M4 keine Größengrenzen | index.html ≤ 20 MB, Bild ≤ 25 MB, Bilder gesamt ≤ 200 MB, ≤ 2000 ZIP-Einträge, ≤ 300 Bilder; gezählt werden gelesene Bytes; Fehler über `Try` sichtbar | Core `M4_grenzen` (21-MB-HTML aus kleiner ZIP, 30-MB-Bild, zu viele Einträge, große Datei); UI `Gegenpruefung_ui`: Meldung „zu groß“, App unverändert |
| M5 Import überschreibt Dateien / schreibt trotz neuerer seite.json | erst alles lesen und prüfen, dann schreiben; nur .png/.jpg/.gif/.webp; Baukasten importiert nach `importiert/<Zeit>/`; vorhandene Dateien nie überschrieben (sonst neuer Name); bei `_builderProblem` wird nichts geschrieben | Core `M5_nichts_ueberschreiben`; UI `Gegenpruefung_ui` (logo.png bleibt, neuere seite.json bleibt Byte für Byte, keine neue Datei) |
| N1 Nullwerte → NullReferenceException | `Normalize`: Kind/Text/Url/Label/Name/Kennungen `null` → leer bzw. Slug | Core `N1_nullwerte` (Fassung 1 und 2) |
| N2 doppelte Kennungen, große Zahlen | Seiten/Sammlungen/Felder eindeutig; Formular lehnt Zahlen über 1 Billion ab, Anzeige ab 10^15 „zu groß“ | Core `M1_…` (doppelte Kennungen), Chrome: 1e20 abgelehnt, nichts gespeichert |
| N3 ehrlicher Speicher-Satz | `SitePage.StorageNote` als Fußzeile jeder App mit Daten und im Baukasten unter „Daten“ | Core `N3_hinweis`, Chrome (Fußzeile gelesen), UI-Text |
| N4 eine goldene Hauptaktion, Knöpfe erkennbar | Handy/Bildschirm zeigen „✓“ statt Gold; „Öffnen (ZIP) …“ und „Veröffentlichen …“ banden an die rahmenlose `Func<Task>`-Überladung von `Act` – jetzt Anweisungs-Lambda (Rahmen); innere Bildlaufleiste rechts entfernt (Knöpfe außer Sicht) | UI `Gegenpruefung_ui` (genau 1 goldener Knopf im Leerzustand und danach, Rahmen 1 px); Bild `oberflaeche-liste-gegenpruefung.png` |
| N5 Zeilenenden der Belege | `.gitattributes`: `reports/T-0765/** -text`; `sha256sum -c SHA256SUMS.txt` nach frischem Auschecken geprüft | siehe Abgabe |

Testläufe der Nacharbeit: Build 0 Fehler · Core `T0765|T0716|T0711|T0718|PromptAction|NoFixedUser` → **Bestanden!** 116/116 · App `T0765_AppBaukasten_Ui, W4_X03_Ui, AppDesigner_Ui, T0711_HuelleEtappe1b_Ui, T0716_HuelleE4_Ui, T0718WebVeroeffentlichen_Ui, T0718WebVeroeffentlichen_Browser` → 29/29 · Browser-Beleg (Läufe 1–3) → BESTANDEN.
Hinweis: `oberflaeche-text-direkt.png` und `oberflaeche-formularseite.png` stammen vom ersten Stand (Handy noch gold); aktuell ist `oberflaeche-liste-gegenpruefung.png`.

Nicht erneut geprüft: ob die neuen Browser-Prüfungen auf dem alten Stand rot wären (Befund stammt aus der Gegenprüfung); die neuen Core-Tests kompilieren auf dem alten Stand nicht (neue Schnittstellen). CSP `navigate-to` nicht eingesetzt (von Chrome nicht unterstützt) – Schutz ist das Maskieren.

**Status:** umgesetzt, gezielt getestet, gepusht auf `claude/T-0765-echte-apps`; Merge nach main offen (Hauptsitzung).
