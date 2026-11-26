# T-0771 – Bericht: Restpunkte 0.9.110 und lastrobuste Tests

**Auftrag:** `tasks/T-0771-restpunkte.md` (Etappe 0.9.111, freigegeben 26.11.2026) · **Zweig:** `claude/T-0771-restpunkte` (Basis main 542dc1ce7 = 0.9.110) · Version im csproj unverändert (0.9.110).

## Punkte

| Punkt | Vorher | Nachher | Test / Beleg |
|---|---|---|---|
| 1 N-a `EmbedUrl` | Ziele ohne Doppelpunkt blieben erhalten, also auch `//fremd`, `\\fremd`, `/\fremd`, `%2F%2F…`, `&#47;&#47;…`, `javascript%3A…` | Prozent- und HTML-Entschlüsselung (bis 4 Runden). Ziele, die mit `/` oder `\` beginnen, Steuerzeichen und kodierte Doppelpunkte werden zu „#“. Datei, E-Mail, `seite:`, `summe:`, https, mailto bleiben | `T0771Restpunkte.Fremde_ziele_werden_raute` (19 Varianten) und Gegenprobe `Erlaubte_ziele_bleiben`. Rot auf altem Code: 19/19 (`reports/T-0771/rot-punkte1-3-core.log`) |
| 2 N2 TestScope | `TestScope` fügte hinzu und entfernte. Öffnete sich dasselbe Profil zweimal, nahm das Schließen des ersten Fensters dem zweiten die Freigabe. Ein Bereich über einem dauerhaft freigegebenen Ort löschte auch die dauerhafte Freigabe | Zähler je Ort (+1/−1 unter Sperre). Die Freigabe endet erst mit dem letzten Bereich und nie bei dauerhafter Freigabe. Doppeltes Dispose zählt einmal | `Zaehler_bei_wieder_geoeffnetem_profil`, `Bereich_entfernt_dauerhafte_freigabe_nicht` – beide rot auf altem Code |
| 3 Prüfer-N1 | Fehlten rolle.json, settings.json und personen.json, galt das Hauptprofil immer als Ersteinrichtung, also als Besitzer | Ersteinrichtung nur in einem wirklich neuen Datenordner: Keine Datei darf älter sein als der Start dieses Prozesses bzw. als die letzte Rollen-Entscheidung dieses Prozesses (Erstellzeit). Leere alte Ordner zählen nicht. Unlesbar gilt als Kind | `Kind_loescht_rollendateien_bleibt_kind`, `Im_selben_lauf_geloescht_bleibt_kind` (rot auf altem Code), `Echte_ersteinrichtung_bleibt_besitzer` (leerer Ordner, Dateien dieses Laufs, alte leere Ordner, fehlender Ordner, Altstand mit settings.json). Dazu RechteKernFix14 im TRX-Lauf grün |
| 4 N-b Katalog | 1 067 von 1 364 Seiten standen auf 0.9.36, der Rest auf 0.9.92–0.9.110. 27 Programme hatten keine Testergebnisse | Vollständig neu erzeugt mit TRX aus Core, Api und App: 123 Programme, 1 353 Seiten „Stand 0.9.110 (0eb9d52)“, alle 123 TEST_RESULTS mit Lauf, keiner „nicht eingelesen“. Den entfernten Ordner `qual-leistung` hat der Erzeuger gelöscht. Der Erzeuger meldet veraltete Vorbild-Namen sofort (`ProgramCatalog.StaleAnalysisNames`), vor und nach dem Lauf, mit Exit 3 | `T0771Restpunkte.Veraltete_vorbild_namen` (Meldung mit Datei und Name; aktueller Stand ohne Meldung), `VorbildAnalysenTests` und `ProgramCatalog_Ui` grün (`gruen-katalog-tests.log`). Erzeuger-Lauf: `katalog-erzeugt.log` |
| 5 Lastrobuste Tests | siehe unten | siehe unten | `gruen-wiederholung-einzeln.log` (40/40), `gruen-unter-last.log`, `gruen-x09-*.log`, `avalonia-dispatcher-nachstellung.log` |

### Punkt 5 – Ursache und Änderung je Test

Künstliche Last: 16 PowerShell-Endlosschleifen auf 16 logischen Kernen. Die Prozessnummern wurden gemerkt und die Prozesse am Ende genau über diese PIDs beendet.

| Test | Ursache | Änderung (Grenze unverändert) | 5× einzeln | 1× unter Last |
|---|---|---|---|---|
| `T0754…Fremder_Ordner_Papierkorb_bleibt` | Der Test setzt den Arbeitsordner des **ganzen** Testprozesses. Parallel gestartete Prozesse (PowerShell aus X-09, FFmpeg/ffprobe über `Cli.Run` ohne eigenen Ordner) erben ihn und halten ihn offen, darum IOException bei Directory.Delete. Unter Last 2/2 nachgestellt (`rot-last-core-alt.log`) | Eigene, nicht parallele Sammlung „Prozess-Arbeitsordner“. Aufräumen mit Wiederholung (höchstens 10 s, danach der echte Fehler) | 5/5 | grün, auch gemeinsam in einem Prozess |
| `MusicProTests.Hook` | Sehr wahrscheinlich dieselbe Ursache: `MediaStudio.Duration` startet ffprobe im Arbeitsordner des Prozesses. Liegt dort der gerade gelöschte Ordner von T0754, scheitert der Start, `Duration` liefert null und `!.Value` wirft. Nicht direkt mit einem Volltest-Log belegt | keine eigene Änderung, behoben über T0754 | 5/5 | grün |
| `T0766_Gegenpruefung2.N10_parallel` | Unter Last enden Übersetzen und Warten auf die Läufe davor (gleicher Datenordner) an der Zeitgrenze von 10 s oder am Übersetzungs-Kontingent, bevor die Datenordner-Grenze erreicht ist. Die geprüfte Lage entsteht dann gar nicht | Versuche ohne erreichte Grenze („Zeitgrenze“, „Übersetzt noch“, „läuft gerade noch“) zählen nicht (höchstens 5 je Runde). „Fremde Dateien bleiben“ und „Grenze eingehalten“ gelten für jeden Versuch | 5/5 | grün |
| `KiModelT0703…Fallback_on_timeout_but_thinking_keeps_alive` | Das erste Lebenszeichen kam nach `await Task.Delay(150)`. Ist der Thread-Pool ausgelastet, läuft die Fortsetzung erst nach der Grenze von 1 s | Der erste Takt wartet auf dem aufrufenden Thread (`Thread.Sleep(150)`, ohne Pool). Gleiche 150 ms, gleiche Grenze | 5/5 | grün |
| `T0747_Fix2…Zigzag_speed_and_cancel` | `CancelAfter(100)` läuft über den Zeitgeber des Thread-Pools. Unter Last wurde der Abbruch selbst zu spät ausgelöst | „Abbrechen“ kommt nach 100 ms von einem eigenen Thread (wie ein Klick). Gemessen wird weiter vom Start bis zur Ausnahme, Grenze 1500 ms × Lastfaktor | 5/5 | grün |
| `TonT0732_Ui.Record_until_stop` | Zeit und Pegel zieht ein Zeitgeber der Oberfläche alle 100 ms nach (niedrige Priorität). Gelesen wurde direkt nach 3,2 s Aufnahme, die Anzeige stand noch auf dem vorigen Takt („0:02,92“) | Bis zu 10 s warten, bis die Anzeige nachgezogen hat. Geprüft wird: Anzeige ≥ 3 s und nie vor der Aufnahme; der Balken schlägt aus | 5/5 | grün |
| `WebDesignStufe3Teil2_Browser.Real_parser_round_trip` (K6) | **Produkt:** `WebsitePreview` gab Chrome feste 40 Verbindungsversuche (etwa 6 s) und jeder einzelnen CDP-Antwort feste 25 s – unabhängig vom Budget des Laufs (60 s) | `PageSocket` wartet, solange Budget und Chrome-Prozess da sind. Eine einzelne Antwort darf bis zum Budget dauern (höchstens 60 s), und das Warten endet sofort, wenn das Budget abläuft | 5/5 | grün (23 s statt 2 s) |
| Avalonia „different thread owns it“ | **Nachgestellt:** Greift ein Hintergrund-Thread zwischen zwei Tests auf `Dispatcher.UIThread` zu, legt Avalonia den neuen Dispatcher für diesen Thread an. Das ergibt genau den Stack aus den Volltests (5/5 nachgestellt). In Trendo stellten 60 Stellen Hintergrund-Ergebnisse über `Dispatcher.UIThread.Post` zu | **Produkt:** neuer `UiThread` (Dispatcher einmal im Hauptfenster gemerkt), alle 60 `Post`-Stellen umgestellt. Im Programm selbst ändert sich nichts (es gibt einen Dispatcher) | Nachstellung mit `UiThread.Post`: 5/5 ohne Fehler. Neu: `T0771Dispatcher_Ui` (3 Tests) und Quelltext-Wächter `T0771DispatcherWaechter` (Rot auf main: 60 Treffer) | – |
| `T0763…Load_probe_against_performance_counters` (X-09) | Im Volltest liefen andere Klassen im selben Prozess mit, dessen Last schwankte, und PDH und Trendo lesen nicht im selben Augenblick | Eigene, nicht parallele Sammlung „T0763 Lastprobe“. Messung und Grenze unverändert | 5/5 (mehrfach) | **rot** – siehe offene Punkte |

## Testläufe

- Build `Trendo.sln -c Release`: 0 Fehler.
- **TRX-Lauf für den Katalog** (Datenlauf, kein Abnahme-Volltest; `trx-lauf-katalog.log`):
  - Core 3779/3825 grün, 43 übersprungen, 3 rot:
    - `LicensingTests.Version_kommt_aus_einer_Quelle`: erwartet, weil `0.9.111.md` existiert und das csproj 0.9.110 hat. Mit vorübergehend 0.9.111 grün, nicht committet.
    - „S4-06-N Abschottung …“ und „Python-Vorlage besteht den eigenen Test …“: ohne Bezug zu diesem Paket. Einzeln 3× grün (`nachlauf-trx-rote.log`).
  - Api 16/16.
  - App 1868/1889, 20 übersprungen, 1 rot: „T-0681 Code-Editor: Ausführen läuft im Hintergrund“ (Zeile 202, Prozess lebt/CodeRunning). Einzeln 3× grün. Die Prozessnummer wird nicht über `Post` gesetzt. Ein weiterer Kandidat für Lastrobustheit.
  - Kein „Cleanup Failure / different thread owns it“ im App-Lauf.
- Der TRX-Lauf deckt alle Testklassen ab, die die geänderten Dateien berühren (PageBuilder/Baukasten, ChildWrite, ProfileRole/RechteKern, alle Oberflächen mit `UiThread.Post`).

## Abnahmekriterien

| # | Urteil |
|---|---|
| 1 Leere Vorlage → Ergebnis | nicht anwendbar (Restpunkte, kein Studio) |
| 2 Fremddatei | nicht anwendbar |
| 3 Abbruch ≤ 30 s | nicht anwendbar; rolle.json weiter über `AtomicFile` |
| 4 KI-Fachaufgabe | nicht anwendbar |
| 5 Klick bis Datei | TEILWEISE: K6 (echter Chrome), Ton (echtes FFmpeg) und Hook (echtes FFmpeg) laufen mit echten Werkzeugen. Die Punkte 1–3 sind Kerntests mit echten Dateien |
| 6 Einfach/Profi … | nicht anwendbar |

## Was NICHT geprüft wurde

- Kein Abnahme-Volltest (macht die Hauptsitzung).
- Hook: Die Ursache ist aus dem Code abgeleitet; ein Volltest-Log des Fehlers lag nicht vor.
- Prüfer-N1 mit einem echten Neustart der App: nur Kerntests, die den Neustart über Erstellzeiten nachbilden.
- Die Oberfläche hat sich nicht sichtbar geändert, darum kein Render-Bild.

## Offene Punkte

1. **X-09 unter künstlicher Volllast**: Mit 16 Zusatzlasten dauerte `TaskManager.Measure()` 2 bis 400 s je Aufruf (gemessen, `gruen-x09-final.log`). Die beiden Messfenster sind dann nicht vergleichbar. Ein Versuch mit „nur Fenster mit gleich langer Messung“ fand in 12 Versuchen keine drei gültigen und wurde wieder entfernt. Vorschlag (Produkt): `Measure()` als eine Momentaufnahme aller Prozesse, ohne `MainModule` je Prozess. Das würde auch den Task-Manager unter Last schneller machen. Gegen die Volltest-Ursache hilft die eigene Sammlung.
2. Weitere Wackelkandidaten aus den Läufen: „T-0747-fix 4: 10 000 Linien“ (rot unter künstlicher Last zusammen mit anderen Klassen), „T-0681 Code-Editor“, „S4-06-N“ und „Python-Vorlage“ (je einmal im TRX-Lauf).
3. Prüfer-N1, Grenzen: Wer den ganzen Datenordner löscht, löst eine Ersteinrichtung aus. Bei gemeinsamem Windows-Konto lassen sich rolle.json und settings.json außerhalb von Trendo ändern; der Übernahme-Weg „settings.json ohne rolle.json“ bleibt dafür offen. Vorschlag: rolle.json mit dem Schlüssel aus dem geschützten Speicher signieren. Ein Hauptprofil aus sehr alter Version ohne beide Dateien startet als Kind (steht in den Neuerungen).
4. `LicensingTests.Version` bleibt rot, bis die Sammlung die Version auf 0.9.111 hebt.

## Berührte Dateien

- **Kern:** `src/Trendo.Core/PageBuilder.cs`, `src/Trendo.Core/Sys/ChildWrite.cs`, `src/Trendo.Core/Profile.cs`, `src/Trendo.Core/Programs/ProgramCatalog.cs`
- **App:**
  - `src/Trendo.App/UiThread.cs` (neu), `src/Trendo.App/WebsitePreview.cs`, `src/Trendo.App/MainWindow.axaml.cs`
  - 44 Dateien mit umgestellten `Post`-Stellen in `src/Trendo.App/*` (nur `Dispatcher.UIThread.Post(` → `Trendo.App.UiThread.Post(`)
- **Werkzeug:** `tools/Trendo.Render/Program.cs`
- **Tests:**
  - neu: `tests/Trendo.Core.Tests/T0771RestpunkteTests.cs`, `tests/Trendo.App.Tests/T0771DispatcherUiTests.cs`
  - geändert: `T0754DateiRueckgaengigTests.cs`, `T0763SystemAlltagTests.cs`, `ExtensionSandboxFix2Tests.cs`, `KiModelT0703Tests.cs`, `DxfWohnungFraesenFix2Tests.cs`, `TonMedienT0732UiTests.cs`
- **Doku:** `docs/programme/**` (neu erzeugt, `qual-leistung` entfernt), `installer/neuerungen/0.9.111.md` (neu), `tasks/T-0771-restpunkte*.md`, `reports/T-0771/*` mit `SHA256SUMS.txt`

**Status:** gepusht auf `claude/T-0771-restpunkte`; Merge und Release macht die Hauptsitzung.

## Korrekturrunde nach Gegenprüfung (fix1)

| Befund | Änderung | Test (Rot auf 76a08e7c5 → grün) |
|---|---|---|
| HOCH 1: Neuinstallation mit Vollpaket startete als Kind (vollpaket-quelle.txt vom Setup ist älter als der erste Start); dasselbe nach einem abgestürzten ersten Start | `ProfileRole.UsedBefore` statt „irgendeine ältere Datei“: Als „schon benutzt“ zählen nur nicht leere Dateien aus einem früheren Lauf an echten Nutzerdaten-Orten (`UserDataPlaces`: projekte, projects, text, briefe, tabelle, präsentation, bild, künstler, karten, post, finanzen, code-editor, claude-chats, webseiten, abläufe … und profil/anmeldung.json = eigene PIN). Setup- und Startdateien zählen nie (vollpaket-quelle.txt, audit, cache, werkzeuge, sicherheit, freigaben, updates, protokolle, Start- und Absturzspuren, leere Ordner und Dateien). Unlesbare Orte gelten als „unklar“, NICHT als Nutzerdaten; das steht als `Grund` in rolle.json und im Trace (keine stille Aussperrung) | (a) `Vollpaket_vom_setup_bleibt_ersteinrichtung` und (b) `Startspuren_ohne_nutzerdaten_bleiben_ersteinrichtung` rot auf 76a08e7c5 (`rot-korrektur-76a08e7c5.log`), jetzt grün; (c) `Kind_mit_nutzerdaten_bleibt_kind` (Projekt, Brief, PIN) und `Kind_loescht_rollendateien_bleibt_kind` grün; (d) Altstand mit settings.json in `Echte_ersteinrichtung_bleibt_besitzer` grün |
| NIEDRIG 3: `suche.html?q=12%3A30` wurde gesperrt | Die Prüfung auf ein Schema gilt nur für den Teil vor „?“/„#“ (am rohen Text geteilt, entschlüsselt geprüft). Steht vor dem Trenner ein „&“ (mögliche HTML-Kodierung wie `&#58;`), bleibt die strenge Prüfung. Wurzelpfade „/…“ bleiben bewusst gesperrt | Gegenprobe um `suche.html?q=12%3A30`, `plan.html?zeit=12:30`, `seite.html#zeit:12` erweitert (rot auf 76a08e7c5). Neue Umgehungen gesperrt: `javascript%3Fx%3Aalert(1)`, `javascript&#63;x&#58;alert(1)`, `javascript:alert(1)?q=1` |
| NIEDRIG 4: `UiThread.s_main` in Tests | `UiThread.ForgetForTests()` (internal) und `Remembered`. Bewusst kein Zurücksetzen beim Schließen des Hauptfensters: Späte Hintergrund-Arbeit griffe sonst wieder auf `Dispatcher.UIThread` zu | `T0771Dispatcher_Ui.D_ruecksetzen_fuer_tests` |

- Eine Besitzer-Bestätigung für den Fall „unklar“ gibt es im Code nicht. Bei einem fehlenden Profil gibt es auch keine PIN. Ich habe keine neue erfunden: offener Punkt.
- Neue Grenze: Ein Kind, das nichts im Trendo-Datenordner angelegt hat (nur in „Dokumente“ gespeichert), bekommt nach dem Löschen der Rollendateien wieder eine Ersteinrichtung. Das steht in den Neuerungen 0.9.111.
- **Tests:**
  - Core: T0771*, Profile*, T0764*, RechteKern*, PageBuilder, SitePage, X03, Vollpaket*, SammelT0768, Person* – 331/331 grün.
  - App: alle RechteKern*-Ui, SammelT0768, T0771Dispatcher_Ui, alle WebDesign*-Klassen und die Vollpaket-Klassen (32 Klassen) – 121 Tests, 0 rot, 3 übersprungen.
  - Logs: `gruen-korrektur.log`.
