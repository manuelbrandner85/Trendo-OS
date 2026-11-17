# Bericht T-0742: Aufgaben automatisieren – KI legt einen echten Ablauf an

**Auftrag:** `tasks/T-0742-automatisieren.md` (Restliste Rang 17, SYS-05)
**Branch/Arbeitskopie:** `claude/T-0742-automatisieren`, eigene Kopie von `main` (a90e697aa) in `%TEMP%\trendo-auftrag-T-0742`, nach dem Push gelöscht.

## Was war, was ist jetzt

| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| KI-Aktion | nur ein Text-Plan (`ablauf.planen`, `SystemPromptAdapter.cs`: „Der Ablauf ist nicht gelaufen“; audit-4 Teil C) | Feld „Mit einem Satz beschreiben“ auf der Seite Abläufe: Satz → `Intel.AskAsync` (Claude-Abo oder lokale KI) → JSON → `WorkflowSentence.FromAnswer` prüft jede Angabe → echter `Workflow` in der vorhandenen `WorkflowEngine` (Auslöser, Bedingung Wochentage/Dateimuster, Schritte). Er ist AUS, steht als Bausteinkette im Editor, ist dort änderbar; „Rückgängig“ entfernt ihn, „Wiederholen“ legt ihn wieder an. Unsichere Antworten (Pfad außerhalb des Benutzerordners, `..`, unbekannte Schrittart, Agent-Schritt, fehlende Uhrzeit) legen nichts an. | `T0742_Ui.Satz_wird_Ablauf_im_Editor` (KI ersetzt), `T0742_Ui.Unsichere_Antwort_legt_nichts_an`, `T0742_Ablauf_aus_Satz.*`; **echt:** `T0742_Echtlauf_Ui` mit Claude Code (Abo) |
| Start erst nach Bestätigung | – | Einschalten eines KI-Ablaufs nur über Rückfrage + Freigabe im Kern (`workflow.enable`, an Id und Inhalts-Prüfsumme gebunden, `ApprovalGate.Execute`). „Nein“ → bleibt aus. | `T0742_Ui.Satz_wird_Ablauf_im_Editor`, `T0742_Ablauf_aus_Satz.Einzelstart_wenn_dran` |
| Bedingungen | nur Dateimuster | zusätzlich „nur an diesen Wochentagen“ (tägliche Abläufe), im Inspektor änderbar. Prüfsumme bleibt ohne Wochentage gleich → gespeicherte Freigaben älterer Abläufe gelten weiter. | `T0742_Ablauf_aus_Satz.Wochentage_und_alte_Freigaben` |
| Läuft nur bei offenem Trendo | Minutentakt nur im offenen Fenster (audit-4 Teil C, Kriterium 12) | Zeitgesteuerte Abläufe (täglich, Wochentage, Intervall) bekommen auf Wunsch eine **Windows-Aufgabe** (`schtasks /Create /XML`, eigenes Konto, Anmeldeart interaktiv, geringste Rechte, kein Kennwort, kein Dienst, keine Registry, kein Admin). Sie startet nur `TrendoOS.exe --ablauf-faellig <Id> --data-root …`; Trendo führt dann genau diesen Ablauf aus, wenn er dran ist, ohne Fenster, und beendet sich. Anlegen nur nach Rückfrage (`workflow.windows-aufgabe`), sichtbar unter dem Ablaufplan mit Aufgabennamen, „Windows-Aufgabe entfernen“ = ein Klick; Löschen des Ablaufs und Rückgängig nehmen sie mit; geänderter Zeitplan wird als veraltet angezeigt. Offenes Trendo und Aufgabe laufen nicht doppelt (Laufstände werden über die Datei abgeglichen). | `T0742_Ui.Windows_Aufgabe_nur_mit_Zustimmung` (schtasks ersetzt), `T0742_Ablauf_aus_Satz.Zweiter_Prozess_kein_Doppellauf`, `…Aufgabe_Argumente_und_Grenzen`, `…Aufgabe_anlegen_und_entfernen`; **echt:** `T0742_Echtlauf_Ui` |
| Ehrliche Grenze | – | „Neue Datei im Ordner“, „von Hand“ und Abläufe mit KI-/Agent-Schritt laufen weiter nur bei offenem Trendo; das steht deutlich gelb unter dem Ablaufplan („⏸ Läuft nur, solange Trendo offen ist …“), ohne Knopf zum Anlegen. Linux: kein Windows-Aufgabenplan → derselbe Hinweis. Die Aufgabe läuft nur, solange die Person angemeldet ist (Benutzeraufgabe ohne gespeichertes Kennwort); verpasste Starts holt Windows nach („StartWhenAvailable“). | `T0742_Ui.Windows_Aufgabe_nur_mit_Zustimmung` (Teil „neue Datei“), Bild-Sichtprüfung |

Erste Fassung nutzte `schtasks /TR`; der Kern-Test zeigte mit langen Temp-Pfaden 276 > 261 Zeichen. Deshalb Aufgaben-XML (Programm und Argumente getrennt) – derselbe Weg über schtasks.exe, ohne Längengrenze.

## Echtbeleg `reports/T-0742/` (nichts ersetzt außer Fensterdarstellung und dem „Ja“ im Rückfrage-Dialog)

Ablauf (`0-ablauf.log`): Satz über die Oberfläche („Schreib mir jeden Tag um 0:01 Uhr im Ordner Berichte eine Textdatei mit dem heutigen Datum im Namen, Inhalt: Tagesbericht – Trendo war hier.“) → echtes Claude Code (Abo) → Ablauf „Täglicher Tagesbericht“, täglich 00:01, Schritte „Ordner anlegen“, „Textdatei schreiben“, aus (`1-ablauf-von-der-ki.json`) → Einschalten nach Rückfrage → Windows-Aufgabe über den Knopf mit dem echten `schtasks.exe` angelegt (`2-windows-aufgabe-abfrage.txt`, `3-windows-aufgabe.xml`: „Nur interaktiv“, Konto-SID ersetzt) → `schtasks /Run` startet das gebaute `TrendoOS.exe --ablauf-faellig` ohne Fenster → Datei `Berichte\Tagesbericht_2026-11-17.txt` entsteht (Name wählt Claude; der Satz ist frei formuliert) (`4-erzeugte-datei.txt`, `5-windows-aufgabe.log`, `6-ablauf-protokoll.jsonl`) → „Windows-Aufgabe entfernen“ → `schtasks /Query` = 1 (weg). Prüfsummen in `SHA256.txt`; alle Dateien LF, `reports/T-0742/** -text` in `.gitattributes`, Hashes gegen die Commit-Blobs geprüft. Keine Trendo-Ablauf-Aufgabe blieb auf dem PC zurück.

Hinweis: Die Aufgabe wurde mit `schtasks /Run` sofort gestartet statt um 0:01 zu warten; der Zeitplan selbst (nächster Lauf 18.11.2026 00:01) steht in `2-windows-aufgabe-abfrage.txt`.

## Abnahmekriterien

1. Aus leerer Vorlage echtes Ergebnis über die Oberfläche: **VOLLWERTIG** – Satz → Ablauf → Datei (Echtbeleg).
2. Fremddatei importieren/exportieren, im Fremdprogramm öffnen: **FEHLT** (nicht Teil dieses Pakets; Trendo-Abläufe haben weiter keinen eigenen Import/Export, nur n8n). Die von Windows importierte Aufgaben-XML ist kein Import einer Fremddatei.
3. Prozessabbruch ≤ 30 s: **TEILWEISE** – unverändert: jede Änderung wird sofort atomar gespeichert (T-0681); der KI-Entwurf liegt nach dem Anlegen sofort in der Datei. Nicht neu gemessen.
4. KI führt Fachaufgabe aus, Ergebnis im offenen Dokument, rückgängig: **VOLLWERTIG** – KI-Ablauf erscheint im offenen Ablauf-Editor, „Rückgängig“ entfernt ihn (UI-Test), echter Claude-Lauf im Beleg.
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** – `T0742_Echtlauf_Ui` (Klick „Ablauf aus dem Satz anlegen“ → … → Datei, mit echtem Claude und echtem schtasks/TrendoOS.exe).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **TEILWEISE** – Enter im Satzfeld, Strg+Z/Strg+Y und Ziehen der Bausteine wie bisher; kein eigenes Rechtsklick-Menü; Bedienstufen unverändert, nicht gesondert geprüft.

## Kommandos und Ergebnisse

- `dotnet build Trendo.sln -c Release` → 0 Fehler
- Core: `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "…T0742|SYS05_Workflows|T0005_NotStopp|WorkflowStepsTests|T0681|FreigabenKern|AgentRunTests|NoFixedUser|PromptActionTests|NoBrandingInDocuments|AbsturzKind"` → Bestanden!, 144 erfolgreich, 0 Fehler
- App (je `-class`): T0742_Ui 3/3 · SYS05_Ui 1/1 · AblaufEditor_Ui 1/1 · WorkflowFlow_Ui 1/1 · WorkflowError_Ui 1/1 · T0681_Ui 6/6 · T0717_HuelleE5_Ui 13/13 · FreigabenKernUi 5/5 · AgentRun_Ui 5/5 · Suggestions_Ui 1/1 · N8nWorkspace_Ui 1/1 · Modern_Ui 13/13 · T0201_Dateiauswahl_Ui 4/4 · W5_X01_Ui 1/1 · DesignPaket3_Ui 14/14 · DesignHonesty 5/5 · W0b_Look 3/3 · LogoTests 4/4 – jeweils Failed 0, Errors 0 (nach der letzten Änderung erneut: T0742_Ui, SYS05_Ui, AblaufEditor_Ui grün)
- Echtlauf: `TRENDO_T0742_ECHT=<repo>\reports\T-0742 Trendo.App.Tests.exe -class Trendo.App.Tests.T0742_Echtlauf_Ui` → Total 1, Failed 0
- Bild: `TRENDO_SHOT=seite TRENDO_STUDIO=SYS-05` (Leerzustand: goldener Knopf „Ablauf aus dem Satz anlegen“) und mit `TRENDO_WF=1` (Ablaufplan mit gelbem Hinweis „nur, solange Trendo offen ist“) angesehen.
- Kein Volltest (macht die Hauptsitzung).

## Was NICHT geprüft wurde

- Ein Start durch die Uhrzeit selbst (0:01) – nur `schtasks /Run`; Abmelden/Neustart, Akku-Betrieb, „verpasster Start nachholen“.
- Windows-Aufgabe aus einer per Velopack installierten Fassung (Pfad `…\TrendoOS\current\TrendoOS.exe`); geprüft mit dem gebauten Programm.
- Lokale KI (Ollama) für den Satz – nur Claude echt, sonst Ersatz-KI.
- Der echte Rückfrage-Dialog (im Test antwortet `FreigabeConfirm` „Ja“/„Nein“); Kinderprofil bei den neuen Rückfragen.
- Linux-Anzeige (nur über `WorkflowTaskPlan.Supported` abgedeckt, nicht auf Linux gestartet).
- Kein Volltest.

## Offene Punkte / Risiken

- PromptAction-Kette (`ablauf.planen`, Capability „Nur der Plan“) bewusst unverändert – der echte Weg ist das Satzfeld auf der Seite Abläufe; die Kette könnte später dorthin verweisen.
- Läuft Trendo offen und die Aufgabe gleichzeitig, verhindert die Sperrdatei einen Doppelstart; Laufstände werden unter einer Sperre über beide Prozesse eingemischt (siehe „Gegenprüfung behoben“).
- Import/Export eigener Trendo-Abläufe (Kriterium 2) fehlt weiter.
- Abläufe mit KI-/Agent-Schritt bleiben an das offene Trendo gebunden.

## Berührte Dateien

- `src/Trendo.Core/W2/Workflows.cs` (Wochentage, Herkunft, Windows-Aufgabe am Ablauf; `IsTimeDue`, `RunOneIfDueAsync`, `RequestEnable/ApplyEnable`, `SyncFromDisk`, `NoteWinTask`)
- `src/Trendo.Core/W2/WorkflowSentence.cs` (neu), `src/Trendo.Core/W2/WorkflowTaskPlan.cs` (neu)
- `src/Trendo.App/MainWindow.Workflows.cs` (Satzfeld, Einschalten mit Rückfrage, Zeitplan-Karte, Wochentage im Inspektor, `RunWorkflowHeadless`)
- gemeinsam: `src/Trendo.App/MainWindow.ItemLinks.cs` (nur `WfLink`: Rückgängig/Wiederholen des Anlegens), `src/Trendo.App/Program.cs` (Schalter `--ablauf-faellig` vor dem Fenster), `.gitattributes` (eine Zeile)
- Tests: `tests/Trendo.Core.Tests/T0742AblaufAusSatzTests.cs`, `tests/Trendo.App.Tests/T0742AblaufAusSatzUiTests.cs`
- Beleg: `reports/T-0742/*`; Auftrag/Bericht: `tasks/T-0742-automatisieren*.md`

## Gegenprüfung behoben (17.11.2026)

Alle Prüfungen mit ersetztem schtasks (`WorkflowTaskPlan.Run`); es wurde **keine** Windows-Aufgabe angelegt, kein `schtasks /Create` oder `/Run` ausgeführt, `T0742_Echtlauf_Ui` nicht gestartet. Nachher `schtasks /Query /FO CSV | grep -i Trendo-Ablauf` leer.

| # | Befund | Jetzt | Test (Rot-Probe) |
|---|---|---|---|
| 1 | MITTEL: Aufgabenprozess schrieb die ganze, beim Start geladene Liste zurück (Pausieren usw. überschrieben) | `RunOneIfDueAsync` liest die Datei frisch, prüft Existenz, eingeschaltet, Freigabe, `WhyNotWithoutApp`, Not-Stopp, fällig; danach schreibt der Prozess nur `LastRun/LastAttempt/FailCount/LastError` dieses einen Ablaufs in die frisch gelesene Datei. Lesen–Ändern–Schreiben in beiden Prozessen unter einer Sperrdatei (`ablaeufe.json.sperre`, `FileShare.None`, Wiederholung bis 15 s, beim Schließen gelöscht); das offene Trendo mischt beim Speichern die Laufstände der Aufgabe ein | Kern `Pausieren_bleibt_erhalten`, `Neu_geloescht_Freigabe_bleiben` (rot auf altem Code), `Pausieren_waehrend_des_Laufs_bleibt` (Rot-Probe: Nur-Laufstand-Modus abgeschaltet → rot); **echt zwei Prozesse**: `T0742_Gegenpruefung_Ui.Zwei_Prozesse_Sperre_echt` startet das gebaute `TrendoOS.exe --ablauf-faellig`, hält die Sperre, pausiert dazwischen – Pausieren bleibt, Laufstand kommt hinzu (Rot-Probe → rot) |
| 2a | MITTEL: Deinstallieren ließ Aufgaben zurück | Velopack 1.2.161 hat `OnBeforeUninstallFastCallback` (schon für `UninstallPrompt` genutzt); `UninstallPrompt.Run` ruft zuerst `RemoveWorkflowTasks` → entfernt alle `Trendo-Ablauf-*`-Aufgaben, die dieses Programm starten. Installer-Vertrag unverändert | App `UninstallPromptTests.Ablauf_Aufgaben_werden_entfernt` (schtasks ersetzt) |
| 2b | Verwaiste Aufgaben nach Löschen/Ordnerwechsel | Beim Start (Hintergrund) `CleanupOrphans`: `schtasks /Query /XML ONE` (ohne Admin) → nur Namensmuster `Trendo-Ablauf-<id>-<6 hex>` **und** Programm = eigene exe **und** Argumente im eigenen Format; entfernt, wenn Datenordner fehlt, Name nicht zu Ordner/Id passt oder der Ablauf dort fehlt; unlesbare Ablaufdatei → bleibt | Kern `Verwaiste_Aufgaben_aufraeumen` (fremdes Programm und fremder Name bleiben; Rot-Probe → rot) |
| 2c | Aufgabe legte fehlenden Datenordner neu an | `MainWindow.RunFromTask`: Ordner fehlt → nichts anlegen, eigene Aufgabe entfernen, still Rückgabe 0; Ablauf fehlt im vorhandenen Ordner → Aufgabe entfernt sich ebenfalls | App `Fehlender_Datenordner_wird_nicht_angelegt`, `Ablauf_weg_oder_KI_Schritt` (Rot-Proben → rot) |
| 2d | Löschen bei gescheitertem Entfernen | `WfDelete`: Aufgabe nicht entfernbar → Ablauf bleibt mit Hinweis; Rückgängig des KI-Anlegens: Aufräumen beim nächsten Start bzw. Selbstentfernen der Aufgabe | App `Loeschen_ohne_entfernte_Aufgabe_behaelt_Ablauf` (rot auf altem Code) |
| 3 | NIEDRIG: Doppellauf, „läuft schon“ als Fehler | `RunAsync(onlyIfDue)` prüft nach dem Belegen erneut (Laufstand frisch von der Platte); „läuft schon“/„nicht mehr dran“ = `WfRun.Skipped`, kein Fehlversuch, Rückgabe 0, neutral protokolliert | Kern `Nach_der_Sperre_erneut_pruefen` (Rot-Probe → rot), App `Laeuft_schon_ist_kein_Fehler` (rot auf altem Code) |
| 4 | NIEDRIG: Argumente | Id nur `[A-Za-z0-9-]` (sonst `ArgumentException`, `Create` meldet es), Datenordner über `WorkflowTaskPlan.Root` ohne abschließenden Trenner (auch im Aufgabennamen) | Kern `Arguments_lehnt_unsichere_Id_ab`, `Arguments_ohne_abschliessenden_Backslash` (rot auf altem Code) |
| 5 | NIEDRIG: nachträglicher KI-/Agent-Schritt | Aufgabenprozess prüft `WhyNotWithoutApp` vor dem Lauf; Karte zeigt „⏸ Läuft nur, solange Trendo offen ist …“ plus Hinweis und Knopf „Windows-Aufgabe entfernen“ | Kern `KI_Schritt_und_Freigabe_vor_dem_Lauf` (rot auf altem Code), App `Ablauf_weg_oder_KI_Schritt` |
| 6 | INFO: Einrückung | `sp.Children.Add(L("Alle … Minuten"…` eingerückt | – |

Testzahlen (gezielt, kein Volltest): Kern 78/78 (T0742_Ablauf_aus_Satz, SYS05_Workflows, T0681_*, WorkflowStepsTests, FreigabenKern, T0005_NotStopp, CORE02/03/10/12, X05_FreeAi, AgentRunTests); App 58/58 (T0742_Ui, T0742_Gegenpruefung_Ui, SYS05_Ui, T0681_Ui, AblaufEditor_Ui, WorkflowFlow_Ui, WorkflowError_Ui, Startargumente, T0717_HuelleE5_Ui, FreigabenKernUi, UninstallPromptTests, W2_Ui, AgentRun_Ui, Suggestions_Ui, CORE02_Backends, CORE03/10/12_Ui, X05_Ui).

Offen nach der Gegenprüfung: Die geänderte Zeitplan-Karte ist nur über den Oberflächentest geprüft, nicht im Bild (das Render-Werkzeug startet ohne Abläufe, der Kopflos-Test zeichnet nicht). Das Aufräumen beim Start und der Deinstallations-Hook liefen nur mit ersetztem schtasks, nicht mit einer echten Aufgabe und nicht aus einer per Velopack installierten Fassung.

Berührte Dateien (Gegenprüfung): `src/Trendo.Core/W2/Workflows.cs`, `src/Trendo.Core/W2/WorkflowTaskPlan.cs`, `src/Trendo.App/MainWindow.Workflows.cs`, `src/Trendo.App/Program.cs`, `src/Trendo.App/UninstallPrompt.cs`; Tests `tests/Trendo.Core.Tests/T0742AblaufAusSatzTests.cs`, `tests/Trendo.App.Tests/T0742AblaufAusSatzUiTests.cs`, `tests/Trendo.App.Tests/UninstallPromptTests.cs`.

## Sprache

C# (.NET 10). Rust eingesetzt: NEIN – kleine Erweiterung bestehender C#-Module, kein Leistungsengpass.

**Status:** umgesetzt, gezielt getestet, Echtbeleg vorhanden; Merge nach `main` nicht Teil dieses Auftrags.
