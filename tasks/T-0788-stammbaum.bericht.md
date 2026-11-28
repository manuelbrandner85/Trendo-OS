# Bericht T-0788: Neues Studio „Stammbaum“ (Etappe 0.9.115 „Wissen & Lernen E“)

**Auftrag:** `tasks/T-0788-stammbaum.md` · **Branch:** `claude/T-0788-stammbaum` (Basis main 0.9.114 = 9bc135aaa) · eigene Kopie in `%TEMP%\trendo-auftrag-T-0788`.

## Nachbesserung nach der Gegenprüfung (gegen f42783151)
Rot-Läufe: `reports/T-0788/gegenpruefung/rot-core.txt` (neue Core-Tests auf altem Code: CSV, Datum, Zeilenumbruch, Ausblenden, KI-Prüfung, Lebend, doppelte und lange Kennungen, CONC 5 s rot; `Cycle_once` mit 10 000 Personen nach 240 s abgebrochen) und `rot-ui.txt` (KI-Kette und `dokument_lesen` zeigten „Erika“ und „15 AUG 1952“). `Date_reversed_and_ca`, `Check_cached` und `Import_in_background` übersetzen auf dem alten Stand nicht (neue Mitglieder).

| Nr. | Fund | Behebung | Regressionstest |
|---|---|---|---|
| HOCH 1 | KI-Kette und Claude `dokument_lesen` sahen Lebende | `ProgramSpec.KiView = FamKiView`: bei „Lebende ausblenden“ dieselbe Kürzung wie in den Ausgaben (`FamilyKit.Private`). `dokument_lesen` nutzt jetzt für jedes Programm mit `KiView` dieselbe Sicht (gemeinsame Brücke, eine Zeile). Neue Personen namens „Lebende Person …“ lehnt die KI-Prüfung ab | `T0788Gegenpruefung_Ui.Ki_sees_no_living` (kein Name/Datum Lebender im KI-Kontext; ohne Schutz sichtbar), `Bridge_read_private` (ganz und Teilpfad) |
| HOCH 2 | Prüfung im UI-Thread kubisch | Nachschlage-Tabellen `FamilyTree.Indexed()` (Personen, Familien, Eltern-/eigene Familien) in Prüfung, Ausblenden, Baum, Bericht, CSV, GEDCOM-Export und Import; Kreise einmal per Tarjan (iterativ, linear) `FamilyKit.CycleMembers`; gelesene Daten zwischengespeichert (`FamEvent.When`, `GenDate.Parse`); in der App je Dokumentstand EIN gelesener Stammbaum und EINE Prüfung für Statusleiste, Prüfbereich, Liste, Baum und Auswahl. Gemessen (`Big_tree_fast`, 50 000 Personen, tiefe Linie): Prüfung 0,38–0,93 s (mit JIT), Ausblenden ≈ 0,6 s, beide Bäume ≈ 0,07 s, GEDCOM hin und zurück ≈ 1,5–1,8 s; Kreis in 10 000er-Linie 0,14 s (vorher > 240 s) | `T0788GegenpruefungTests.Big_tree_fast`, `Cycle_once` (Arbeit gemessen, Grenze großzügig 5 s), `T0788Gegenpruefung_Ui.Check_cached` |
| MITTEL 3 | CONC quadratisch, Import im UI-Thread | `Gedcom.Parse` setzt Fortsetzungen mit `StringBuilder` zusammen (30 000 CONC: 46 ms, vorher 5 s rot); Knopf und Ablegen lesen über `FamImportAsync` im Hintergrund (`Task.Run`), übernommen wird im UI-Thread; Meldung „Lese … ein“ | `Conc_linear`, `T0788Gegenpruefung_Ui.Import_in_background` (Zerlegen nicht im UI-Thread), `T0788Stammbaum_Ui.Import_gedcom` |
| MITTEL 4 | „lebt“ zu früh verneint | Tot nur mit Tod/Begräbnis oder wenn die SPÄTESTE mögliche Geburt > 110 Jahre zurückliegt: „nach 1900“/„ab 1890“ → lebend; über Vater +80, Mutter +60 Jahre bzw. deren Tod, über Kinder −12, über Heirat −12; Partner allein reicht nicht. Alter Test korrigiert (Kind eines 1890 Geborenen gilt jetzt als lebend) | `Living_conservative`, `T0788StammbaumTests.Living_hidden` |
| MITTEL 5 | Private() ließ NOTE/OBJE/Quellen Lebender stehen | Datensätze und Quellen, auf die nur noch ausgeblendete Personen oder Familien verwiesen, fallen weg (Erreichbarkeit über Personen, Familien, Kopf und weiter über Datensätze); fremde, nie verwiesene Datensätze bleiben | `Private_drops_records` |
| MITTEL 6 | doppelte Kennungen | Einlesen: doppelte Kennung gemeldet, zweiter Datensatz eindeutig umbenannt (beide bleiben); KI-Prüfung lehnt doppelte/vorhandene Kennungen, Änderungen an Datensätzen, Kopf, Titel und Mitte und neue Kreise ab. Zeilen/KI setzen die Mitte nicht mehr | `Duplicate_xrefs`, `Ki_check_strict` |
| NIEDRIG 7 | Datumsgrenzfälle | v. Chr. julianisch (29. FEB 45 v. Chr. gültig, 44 v. Chr. nicht, 31. FEB nie), `@#DJULIAN@ 29 FEB 1700` gültig, Doppeljahr nur mit dem Folgejahr (1699/00), verkehrte Spanne: Eingabe abgelehnt, Prüfung meldet „Spanne verkehrt“; „ca.1850“ lesbar | `Date_edges` (7), `Date_reversed_and_ca` |
| NIEDRIG 8 | CSV-Formeln | Zellen mit = + - @ Tab CR LF am Anfang bekommen ein „'“ | `Csv_formula` |
| NIEDRIG 9 | Umbruch an Kombinationszeichen, lange Kennungen | CONC trennt nie vor einem Kombinationszeichen; Kennungen über 20 Zeichen werden beim Einlesen kurz umbenannt, alle Verweise mitgeführt, gemeldet | `Chunks_keep_combining`, `Long_xrefs` |
| NIEDRIG 10 | Echtlauf als „bestanden“ geführt, obwohl die Heirat fehlte | Der Echtlauf prüft jetzt die Heirat und schreibt ehrlich „TEILWEISE“, wenn sie fehlt; frühere Protokolle bleiben als `echtlauf-1-befund.md`/`echtlauf-2-vor-gegenpruefung.md` erhalten. Urteil Abnahme 4 jetzt TEILWEISE (siehe unten) | `Real_local_ki` (neues Protokoll `reports/T-0788/ki/echtlauf-lokale-ki.md`) |

**Echtlauf nach der Nachbesserung** (`reports/T-0788/ki/echtlauf-lokale-ki.md`, Ollama selbst gestartet und danach beendet, qwen3:14b): **TEILWEISE**. Personen, Eltern und alle Daten stimmen (Erika Muster 15. August 1952 Neustadt; Hans Muster 1921 Musterstadt – 1999 Neustadt; Marie Sommer um 1925 Talheim; Familie mit Kind), ein Rückgängig-Schritt stellt alles her – aber die Heirat (1950 in Neustadt) hat die KI nicht geliefert, wie schon im Lauf davor. Der Plan kam erst nach 1233 s (Grafikspeicher geteilt, ein anderer Lauf nutzte dasselbe Ollama gleichzeitig; etwa 2 Token/s).

**Tests dieser Runde:** Build 0 Fehler. Core (T0788* inkl. Fremdnachlauf echt, PromptAction*, WissenStudios – Zeitleisten, NoFixedUser, StudioTeams, T0782*, Tests inkl. QA-015 bei 91): **227 gesamt, 226 bestanden, 1 übersprungen** (Beleg-Schalter T-0782) – `reports/T-0788/gegenpruefung/core-klassen.txt`. App (16 Klassen: T0788Stammbaum_Ui, T0788Gegenpruefung_Ui, PromptBarTests, UebersichtEinfach_Ui, WissenStudios_Ui, AssistChain_Ui, ClaudeBridge_Ui, T0780Gegenpruefung_Ui, T0782Gegenpruefung_Ui/2/3, T0782Lernen_Ui – wegen `dokument_lesen` mit KiView –, StudioFolds_Ui, Sys13_Qualitaet_Ui, StartMenu_StudioFenster, StudioBilder): **Total 90, Failed 3, Errors 0, Skipped 4** – die 3 sind die bekannten requirements.yaml-Abhängigen (`StudioFolds_Ui.Overview_and_search`, `Sys13_Qualitaet_Ui.Modules`, `StartMenu_StudioFenster.Jedes_Studio`) – `reports/T-0788/gegenpruefung/app-klassen.txt`. Echtlauf `Real_local_ki` gesondert (TEILWEISE, siehe oben).

### Nachtrag: Claude-Wächter bei Lebenden (Auftrag der Hauptsitzung)
Vorher (Rot-Lauf `reports/T-0788/gegenpruefung/rot-claude-schutz.txt`, 4 von 4 rot auf 95e567750): Der Claude-Chat schrieb die ganze Studio-`.tdoc` mit Lebenden in Claudes Projektordner, Plan-Zeilen liefen im geschützten Stammbaum, die Brücke las und schrieb, Claudes `.tdoc`-Änderung wurde übernommen.
Jetzt: `KiLocalOnly = FamClaudeBlocked` – an, solange im offenen Stammbaum „Lebende ausblenden“ an ist UND das Dokument vermutlich lebende Personen enthält (je Dokumentstand zwischengespeichert). Damit greift der gemeinsame Wächter aus T-0782 auf allen Wegen:
- **Claude-Bereich/Chat und .tdoc-Hinweg:** Bereich öffnet nicht (`claude.dock.lokal`); ist er schon offen, sendet der Chat nichts (`claude.chat.lokal`) – die Prüfung gilt jetzt auch für die Seite des Bereichs selbst (`LocalOnlyRefusal(dock.Page)`), und vor dem Schreiben der `.tdoc` wird `AssistLocalOnly(dock.Page)` noch einmal geprüft (geprüft: der `.tdoc`-Weg fragt den Wert ab).
- **.tdoc-Rückweg:** `ClaudeTdocRefused` → `KiStepPreReject` → abgelehnt (`claude.tdoc.zurueckgewiesen`).
- **Plan-Zeilen:** `ClaudeRunPlan` → `KiStepPreReject` → „Nicht ausgeführt“ (`claude.plan.lokal`).
- **Brücke:** jedes Werkzeug außer der Programmliste → abgelehnt (`claude.bruecke.lokal`).
- **Meldung:** neu `ProgramSpec.KiLocalOnlyWhy` (gemeinsam, optional): „Lebende Personen sind geschützt – Claude bekommt diesen Stammbaum nicht. Nimm dafür die KI auf diesem PC oder schalte rechts „Lebende Personen in Ausgaben ausblenden“ aus.“ – im Chat, im Programm und für Claude. Nachhilfe/Kinderprofil behalten ihren Satz.
- **Lokale KI:** arbeitet weiter, mit `FamKiView` (keine Lebenden im Kontext); die KI-Kette nimmt in diesem Zustand nur die KI auf diesem PC.
- Schutz aus oder keine Lebenden: alles wie vorher erlaubt (Claude bekommt dann das ganze Dokument bzw. die – dann gleiche – Sicht).
Tests: `T0788ClaudeSchutz_Ui` (4: Chat/.tdoc an–aus–an, .tdoc-Rückweg, Plan abgelehnt/erlaubt, Brücke abgelehnt/erlaubt ohne Lebende/erlaubt ohne Schutz); `T0788Gegenpruefung_Ui.Bridge_read_private` an das neue Verhalten angepasst (jetzt Ablehnung statt gekürzter Sicht). Kein Claude-Echtlauf (Claude ersetzt).
Testlauf dieser Runde: App T0788ClaudeSchutz_Ui, T0788Gegenpruefung_Ui, T0788Stammbaum_Ui, ClaudeBridge_Ui, AssistChain_Ui, T0782Gegenpruefung_Ui/2/3, T0782Lernen_Ui: **Total 46, Failed 0, Errors 0, Skipped 3** (Echtläufe ohne Schalter) – `reports/T-0788/gegenpruefung/app-claude-schutz.txt`; Core T0788* (ohne Fremdnachlauf): 62/62.
Ehrlich: Eine `.tdoc`, die Claude in einem früheren Chat bei ausgeschaltetem Schutz bekommen hat, bleibt in dessen Projektordner liegen – das hat die Person mit dem Ausschalten so gewählt; neue Kopien entstehen bei eingeschaltetem Schutz nicht.

### 2. Prüfrunde (gegen 7bb702189)
Rot-Lauf: `reports/T-0788/gegenpruefung/rot-runde2.txt` (Core 6 rot, App 3 von 3 rot – in Claudes Ordner standen „Erika“ und „Karl“, die alte Kopie blieb liegen).

| Nr. | Fund | Behebung | Test |
|---|---|---|---|
| HOCH | .tdoc-Rückweg verriet ein geschütztes Dokument (Abgleich mit dem aktiven Dokument, Zurückschreiben des vollen Stands) | (a) Trendo merkt sich je selbst angelegter Arbeitsdatei Programm und Dokument-Id (`_claudeWorkFiles`); der Abgleich nimmt nur dieses Dokument, nie das aktive – gehört die Datei zu keinem offenen Dokument, wird nichts übernommen. (b) Gilt für dieses Dokument oder das Programm „nur lokale KI“ (neu `ProgramSpec.KiLocalOnlyDoc`, Stammbaum: `FamProtected`), wird nie zurückgeschrieben: die von Trendo angelegte Datei wird gelöscht, ehrliche Meldung an Person und Claude, Protokoll `claude.tdoc.zurueckgewiesen`. (c) Sonst wird beim Zurückschreiben nur die `KiView`-Sicht geschrieben. Eine nur zum Lesen geöffnete eigene Datei der Person wird nie als Arbeitsdatei geführt (und damit nie gelöscht) | `T0788Gegenpruefung3_Ui.Tab_switch_during_run`, `Protection_on_during_run` (Inhalt aller .tdoc in Claudes Ordner ohne „Erika“, „Karl“, „15 AUG 1952“); T0782*, SammelT0768KinderschutzUi grün |
| MITTEL | transitive Verweise | Beim Ausblenden wird auch die Erreichbarkeit von den ausgeblendeten Verweisen aus über Datensätze und Quellen verfolgt (Lebender → NOTE → SOUR → REPO) | `T0788Gegenpruefung3Tests.Private_transitive` („Meldeakte Erika“) |
| NIEDRIG | alte .tdoc beim Einschalten | `FamSetOption("hideLiving", true)` löscht bei geschütztem Dokument die von Trendo angelegten Arbeitsdateien genau dieses Dokuments (eigene Liste: Pfad und Herkunft), fremde Dateien im Ordner bleiben; Meldung „… Arbeitskopie(n) … gelöscht“, Protokoll `claude.tdoc.geloescht` | `Protection_on_deletes_old_copy`; `T0788ClaudeSchutz_Ui` angepasst (Kopie jetzt gelöscht statt liegen gelassen) |
| NIEDRIG | „29.02.45 v. Chr.“ | v. Chr. vor der Tagesprüfung (julianisch) | `German_bc_ok`, `German_bc_bad` |
| NIEDRIG | KI und Generationen | `KiProblem` sperrt `gens` | `Ki_gens_locked` |
| NIEDRIG | doppelte Kennung | FAMC/FAMS des umbenannten zweiten Datensatzes werden gelöst und gemeldet | `Duplicate_not_linked` |
| NIEDRIG | `Fresh()` | auch Kennungen, die nur als (verwaister) Verweis vorkommen, gelten als vergeben | `Duplicate_not_linked` (@I3@ verwaist) |
| NIEDRIG | `FamStamp` | Tag (`FamToday`) im Stempel – „lebt“ wird am Folgetag neu bewertet | – (Stempel-Inhalt; mit `Check_cached` mitgelaufen) |
| NIEDRIG | Einsender (SUBM) | Bei „Lebende ausblenden“ (Export, Bericht, KI-Sicht) fallen Adresse, Telefon, E-Mail, Fax, Webseite und eigene Tags des Einsenders weg; der Name bleibt. Grund: Der Einsender ist meist eine lebende Person (oft die Person selbst); der Name zeigt nur die Herkunft der Datei | `Subm_contact_removed` |

Tests dieser Runde: Core T0788*, T0782*, PromptAction*, WissenStudios: **232 gesamt, 231 bestanden, 1 übersprungen** (Beleg-Schalter T-0782) – `reports/T-0788/gegenpruefung/core-runde2.txt`. App (T0788Stammbaum_Ui, T0788Gegenpruefung_Ui, T0788ClaudeSchutz_Ui, T0788Gegenpruefung3_Ui, T0782Gegenpruefung_Ui/2/3, T0782Lernen_Ui, AssistChain_Ui, Assist_Ui, ClaudeBridge_Ui, SammelT0768KinderschutzUi, UebersichtEinfach_Ui, PromptBarTests): **Total 70, Failed 0, Errors 0, Skipped 3** (Echtläufe ohne Schalter) – `reports/T-0788/gegenpruefung/app-runde2.txt`.

### 3. Prüfrunde (gegen 1dbc638b6)
Rot-Lauf: `reports/T-0788/gegenpruefung/rot-runde3.txt` (Core 1 rot, App 3 von 3 rot: Schutz an löschte die gespeicherte Datei der Person, Strg+S schrieb den geschützten Stammbaum wieder in Claudes Ordner, eine gleichnamige eigene Datei wurde gelöscht, schreibgeschützte Kopie still übergangen).

| Nr. | Fund | Behebung | Test |
|---|---|---|---|
| HOCH | Löschen traf die echte Datei der Person | (a) Gelöscht wird nur, was Trendo selbst in Claudes Ordner geschrieben hat, was **nicht** `doc.Path` eines offenen Dokuments ist und dessen SHA-256 noch genau dem zuletzt Geschriebenen entspricht (`TrendoDeleteBlocked`, Hash bei Arbeitsdatei, Speichern in Claudes Ordner und Zurücksetzen). (b) Liegt ein geschütztes Dokument in Claudes Ordner, verlegt Trendo es beim Einschalten des Schutzes und bei jedem Speichern in den Studio-Ordner (`DocSave` → `ClaudeSaveRedirect`): neue Datei schreiben, lesend prüfen, Versionen mitnehmen, erst dann die alte entfernen – nur nach (a). Meldung mit neuem Ort, Protokoll `program.verlegt`. Rückgängig bleibt (Test). (c) Strg+S und automatisches Speichern schreiben bei Schutz nie in Claudes Ordner. Erkannt wird Claudes Ordner am Projektordner eines Claude-Projekts des Studios oder an der eigenen Liste der Arbeitsdateien – ein mit dem Projekt nur verknüpftes Dokument im Studio-Ordner zählt nicht | `T0788Gegenpruefung4_Ui.Own_saved_protect_save` (eigene-gespeichert → Schutz an → Strg+S: keine Lebenden in Claudes Ordner, Dokument am neuen Ort vollständig, Rückgängig), `Foreign_same_name_kept` (gleichnamig-fremd bleibt unverändert) |
| NIEDRIG | Löschfehler still geschluckt | „Arbeitskopie konnte nicht gelöscht werden: <Name> (<Grund>) – bitte schließen bzw. selbst löschen.“ im Programm bzw. im Chat, Protokoll `claude.tdoc.nicht.geloescht` | `Undeletable_reported` (schreibgeschützt; Gegenprobe ohne Schreibschutz: gelöscht) |
| NIEDRIG | nur lesbare Dokumente | dokumentiert mit Begründung in `docs/claude/CLAUDE_DOKUMENT_ABGLEICH.md` (gilt für alle Programme): keine Arbeitsdatei, keine Übernahme ins nur lesbare Dokument, Meldung „… gehört zu keinem offenen Dokument mehr – nichts übernommen“ | Verhalten unverändert seit Runde 2; Meldung geprüft (verständlich, nennt die Datei) |
| NIEDRIG | SUBM-Notizen | NOTE/OBJE des Einsenders fallen weg, verwiesene Datensätze transitiv mit | `T0788Gegenpruefung4Tests.Subm_notes_removed` |

**Folge von Regel (a), bewusst und angepasst:** Ändert Claude die Arbeitsdatei während des Laufs und schaltet die Person danach den Schutz ein, bleibt Claudes Fassung liegen (sie enthält nur, was Claude zu dem Zeitpunkt ohnehin hatte); Trendo schreibt nichts dazu, löscht nicht und meldet „bitte selbst löschen“. Die Tests aus Runde 2 (`Protection_on_during_run`, `T0788ClaudeSchutz_Ui.Tdoc_sync_back_refused`) prüfen dafür jetzt den Dateiinhalt: genau Claudes Fassung, nichts von Trendo hinzugefügt; das Dokument selbst liegt nicht mehr dort. `Protection_on_deletes_old_copy` erwartet jetzt die Verlegungsmeldung.

Tests dieser Runde: Core T0788*, T0782*, PromptAction*, WissenStudios: **233 gesamt, 232 bestanden, 1 übersprungen** – `reports/T-0788/gegenpruefung/core-runde3.txt`. App: T0788Stammbaum_Ui, T0788Gegenpruefung_Ui, T0788ClaudeSchutz_Ui, T0788Gegenpruefung3_Ui, T0788Gegenpruefung4_Ui, T0782Gegenpruefung_Ui/2/3, T0782Lernen_Ui, AssistChain_Ui, Assist_Ui, ClaudeBridge_Ui, SammelT0768KinderschutzUi, UebersichtEinfach_Ui, PromptBarTests, ClaudeLiveSync_Ui, ClaudeProgramDoc_Ui, ClaudePrograms_Ui, ClaudeLive_Ui, T0780Gegenpruefung_Ui, T0779QuizKurse_Ui: **Total 107, Failed 0, Errors 0, Skipped 4** (Echtläufe ohne Schalter) – `reports/T-0788/gegenpruefung/app-runde3.txt`.

### 4. Prüfrunde (gegen 56396d26d)
Rot-Lauf: `reports/T-0788/gegenpruefung/rot-runde4.txt` (App 4 von 4 rot: Versionen und Begleitdateien mit Lebenden blieben in Claudes Ordner, „Speichern unter“ in einen Unterordner legte den geschützten Stammbaum dort ab, Verlegen-Fehler ohne ehrliche Meldung).

| Nr. | Fund | Behebung | Test |
|---|---|---|---|
| HOCH | Versionen blieben in Claudes Ordner (`Directory.Move` warf, `File.Delete` übersprungen) | Neuer Helfer `ClaudeRelocateSidecars`: Zielordner anlegen, jede Version einzeln kopieren, per SHA-256 prüfen, das Original nur nach der Löschregel entfernen (Hash der Versionen wird beim Speichern in Claudes Ordner mitgeführt); Verschieben und Löschen getrennt; leere Versionsordner entfernt. Begleitdateien: Notsicherung und Wiederherstellung kopieren/prüfen/entfernen, Zwischendateien (`.tmp`) entfernen; Vorschaubilder/`.bak` legt die Hülle nicht an (Code geprüft). Meldung: ein Satz je Datei, die bleiben musste – keine Widersprüche, keine Dopplung (beim Verlegen gemeldete Datei wird nicht noch einmal als Arbeitskopie gemeldet) | `T0788Gegenpruefung5_Ui.Versions_moved` (Inhalt von ganz Claudes Ordner ohne Lebende, Zahl der Versionen am neuen Ort gleich, Meldung ohne „konnte nicht“), `Companions_moved` |
| MITTEL | nur exakter Ordner erkannt | „liegt unterhalb“: Wurzel `Projekte\Claude` und Projektordner aller Claude-Projekte aller Studios, normalisierte Pfade (kurz zwischengespeichert); `ProgramNewTarget` wird geprüft und abgelehnt, falls es selbst darunter läge; Umleitung meldet sich auch ohne Altdatei | `Subfolder_save_as` (Unterordner abgelehnt → Studio-Ordner; Gegenprobe ohne Schutz erlaubt) |
| NIEDRIG | voller Datenträger | Verlegen scheitert → Schutz gilt, Dokument bleibt offen und vollständig, Meldung „… Die Kopie liegt noch in Claudes Ordner: <Pfad> – bitte Platz schaffen …“ | `Move_fails_honestly` (Zielname als Ordner belegt) |
| NIEDRIG | doppelte Meldung | siehe HOCH (eine Meldung je Datei) | `Versions_moved`, `Foreign_same_name_kept` |
| NIEDRIG | Doku | `docs/claude/CLAUDE_DOKUMENT_ABGLEICH.md` an das echte Verhalten angepasst (Ordnererkennung, Verlegen mit Begleitdateien, Fehlerfall, Grenzen) | – |

Tests dieser Runde: Core T0788*, T0782*, PromptAction*, WissenStudios: **233 gesamt, 232 bestanden, 1 übersprungen** – `reports/T-0788/gegenpruefung/core-runde4.txt`. App: 26 Klassen (wie Runde 3, dazu T0788Gegenpruefung5_Ui, ProgramShellUiTests, ProgramCrashUiTests, OfficeRecoveryUiTests, T0716_HuelleE4_Ui): **Total 127, Failed 0, Errors 0, Skipped 4** (Echtläufe ohne Schalter) – `reports/T-0788/gegenpruefung/app-runde4.txt`.

**Offen:** Mutierende Befehle lesen den Stammbaum weiter einmal je Klick neu aus dem Dokument (bei 50 000 Personen etwa eine halbe Sekunde), die Prüfung danach einmal je neuem Stand. Arbeitskopien und Prüfsummen aus früheren App-Sitzungen kennt Trendo nicht (die Liste lebt im Speicher) – solche Dateien werden nicht gelöscht, sondern beim Verlegen gemeldet; neue entstehen bei Schutz nicht. Geänderte Arbeitsdateien (Claude oder Person) löscht Trendo nicht – die Person muss sie selbst löschen.

## Was war → was ist
| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| Studio Stammbaum | fehlte (`docs/audit/2026-11-26-neubewertung/fehlende-studios.md`, Wissen & Lernen Rang 10: „kein GEDCOM – neuer Kern“) | Studio `STUDIO-FAMILY-01`, Programm `stammbaum-programm` in der Hülle: Personenliste links (Suche), Baum in der Mitte, Personenzeile oben, Eigenschaften rechts, Prüfung unten | `T0788Stammbaum_Ui` (5 Tests + Echtlauf) |
| Personen, Familien, Ereignisse | – | Person, Vater, Mutter, Partner, Kind (Familie wird mitgeführt); Ereignisse Geburt, Taufe, Tod, Begräbnis, Beruf, Wohnort … (25 Arten) und Familienereignisse (Heirat, Scheidung …); Notizen | `Click_to_files`, `Lines`, `Lines_parent_first` |
| Daten | – | `GenDate`: GEDCOM 5.5.1 (genau, ABT/CAL/EST, BEF, AFT, BET…AND, FROM…TO, INT, Text, B.C., Doppeljahr); deutsche Eingabe („12.03.1850“, „um 1850“, „zwischen 1850 und 1860“, „44 v. Chr.“); Rohtext bleibt erhalten; unlesbare Eingabe mit Beispielen abgelehnt | `Dates` (14), `Input` (14), `Input_rejected` |
| Orte, Quellen | – | Ortsansicht (Zahl der Ereignisse, Umbenennen überall, Koordinaten N/E), Quellen (Titel, Verfasser, Veröffentlichung) an Person oder Ereignis mit Fundstelle | `Mouse_and_keys` (Ort umbenannt), `Read_foreign`, `Report` |
| Baumansicht | – | Vorfahren (Ahnentafel, 2–6 Generationen, „+ Vater/+ Mutter“ anklickbar) und Nachkommen (Partner im Kasten); Mausrad-Zoom um die Mausstelle, Ziehen, + / − / 0, Pfeile, Klick wählt, Doppelklick/„In die Mitte“, Rechtsklick-Menü, Entf; Einpassen hält die Person in der Mitte im Bild (nie kleiner als 60 %) | `Layouts`, `Mouse_and_keys`, `Click_to_files` (Klick auf „+ Mutter“) |
| GEDCOM | – | `Gedcom.Read/Write`: CONC/CONT, Kennungen bleiben, fehlerhafte Zeilen mit Nummer gemeldet; Export immer UTF-8, Kopf GEDC 5.5.1 + SUBM, Zeilen ≤ 255 Byte, CONC nie an Leerzeichen; **unbekannte Tags und Datensätze bleiben erhalten** (an Person, Name, Ereignis, Ort, Quellenangabe, FAMC/FAMS/CHIL, Kopf, eigene Datensätze); zweiter Export zeichengleich | `Roundtrip_lossless`, `Long_values`, `Broken_lines`, `Import_gedcom` |
| Zeichensätze | – | UTF-8 (±BOM), UTF-16, Windows-1252/ANSI, DOS 850, Macintosh; **ANSEL wird erkannt und umgerechnet** (Akzente vor dem Buchstaben → Unicode NFC, ß, Ł, Ø …), unbekannte Zeichen gezählt und in der Meldung genannt; falsche Angaben („ASCII“ bei UTF-8) erkannt | `Ansel`, `Encodings`, `Import_gedcom` (ANSEL per Ziehen) |
| Prüfungen | – | Fehler: Geburt nach Tod, Taufe vor Geburt, Begräbnis vor Tod, Elternteil jünger als Kind, Geburt nach Tod der Mutter / > 1 Jahr nach Tod des Vaters, Heirat vor eigener Geburt / nach eigenem Tod, eigener Vorfahre. Hinweise: Mutter < 12 / > 55, Vater < 13 / > 80, Heirat < 13, Alter > 120, Ereignis > 1 Jahr nach Tod, unlesbare/nicht genormte Daten, leere Familie. Ungefähre Daten mit ±10 Jahren – keine Fehlalarme | `Checks_find_contradictions`, `Checks_no_false_alarm`, `Click_to_files` (unten sichtbar, Rückgängig) |
| Bericht/PDF | – | Personen- und Familienbericht (Lebensdaten, Eltern, Partner mit Heirat, Kinder, nummerierte Quellen mit Fundstelle, Notizen, Prüfung, Quellen- und Ortsverzeichnis) als PDF, DOCX, ODT über die gemeinsamen Textformate; Baum als SVG, PNG, PDF; Personenliste CSV | `Report`, `Click_to_files`, Fremdnachlauf |
| Lebende schützen | – | Haken „Lebende Personen in Ausgaben ausblenden“ (Standard an) für GEDCOM, Bericht, Baum-Bild und CSV: Name „Lebende Person“ + Nachname, keine Daten/Notizen/Quellen, Familienereignisse lebender Paare entfallen, Verknüpfungen bleiben. Lebend = kein Tod/Begräbnis und jünger als 110 Jahre, sonst Schätzung über Eltern/Kinder/Partner/Heirat; ohne Anhalt geschützt | `Living_hidden`, `Click_to_files` |
| KI-Fachaktion | – | `stammbaum.personen` über `ProgramSpec.KiActions` (T-0774): Vorschau ändert nichts, „Ja, behalten“ fügt ein, ein Rückgängig-Schritt. Gesperrt: `sources`, `hideLiving` (`KiLocked`); `KiCheck = FamilyKit.KiProblem`: vorhandene Personen, Familien (nur neue Kinder erlaubt), Quellen und der Schutz dürfen sich nicht ändern, neue Daten müssen lesbar sein. Menüband-Befehle `NoKi` | `Ki_check` (Core), `Ki_persons` (UI, KI ersetzt), **echt** `Real_local_ki` (qwen3:14b) |
| Auftrag außerhalb des Studios | – | `StammbaumGedcomAdapter` („Personen als GEDCOM“): Zeilen → geprüfte GEDCOM-Datei im Projekt, kein Seiteneffekt | `T0788StammbaumTests.Adapter` |
| Gemeinsam (klein) | `TimelineKit.ToSvg/ToPdf` schrieben fest „Zeitleiste“ als Titel | optionaler Titel (Standard „Zeitleiste“, Zeitleisten unverändert) – der Stammbaum nutzt dieselben Zeichenbausteine für Bildschirm, SVG, PNG und PDF | `Layouts`, `WissenStudiosTests` (39) grün |

## Befunde unterwegs → behoben
| Befund (Beleg) | Behebung | Test |
|---|---|---|
| Erster Echtlauf (`reports/T-0788/ki/echtlauf-1-befund.md`): qwen3:14b nannte Hans Muster zuerst als Vater, danach in eigener Zeile mit Daten – die Daten fehlten im Dokument (Person galt schon als „vorhanden“) | `AddLines` unterscheidet Personen, die im selben Aufruf neu angelegt wurden; deren Angaben werden ergänzt. Vorhandene Personen bleiben unverändert | `Lines_parent_first`; zweiter Echtlauf bestanden |
| LibreOffice hing 300 s beim Einlesen des Bericht-DOCX (`fremd`-Lauf 1, Exit −2) – eingegrenzt: ein DOCX, das **mit einer Tabelle endet**, bringt LibreOffice zum Hängen (Varianten „nur Tabelle“ hängen, „Tabelle + Absatz“ 1 s) | Bericht endet immer mit einem Absatz (Stand, Zahl der Personen) | `Report` (letzter Block ist ein Absatz), Fremdnachlauf grün |
| Arbeitsfläche überdeckte bei kleinem Fenster den Bereich „Prüfung“ (Mindesthöhe) und der Baum war zu klein | Mindesthöhe entfernt, Einpassen mit Untergrenze 60 % und Person in der Mitte | Bild geprüft, `Mouse_and_keys` (Trefferprüfung mit `InputHitTest`) |
| Calc macht aus „12. März 1850“ eine Datumszelle (12.03.50) | so belassen (Verhalten von Calc), Prüfung im Beleg angepasst und kommentiert | Fremdnachlauf |

## Echtlauf mit lokaler KI (nichts installiert, Claude nicht beteiligt)
Ollama aus `%LOCALAPPDATA%\TrendoWerkzeuge\ollama` mit Trendos Modellordner selbst gestartet und danach beendet, Modell qwen3:14b (automatische Wahl). Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“). Beleg (erster Stand, jetzt `reports/T-0788/ki/echtlauf-2-vor-gegenpruefung.md`): damals als „bestanden“ geführt, richtig ist **TEILWEISE** (Heirat fehlte) – siehe Nachbesserung oben (Plan nach 19–20 s). Ergebnis: Erika Muster (15. August 1952, Neustadt), Eltern Hans Muster (1921 Musterstadt – 1999) und Marie Sommer (um 1925 Talheim), Familie mit Kind; ein Rückgängig-Schritt stellt das Dokument wieder her.
**Ehrlich:** Die KI ließ die Heirat (1950 in Neustadt) und das Geschlecht von Erika weg – Trendo ergänzt nichts selbst; die Person sieht die fehlende Heirat im Baum und kann sie eintragen. Der Sterbeort von Hans fehlt in der Anzeige des Belegs nur, weil das Protokoll ihn nicht ausgibt (im GEDCOM-Beleg steht er).

## Fremdprogramme (Abnahme 2)
Test `T0788_Fremdnachlauf` (2/2 bestanden, echte Werkzeuge, nichts ersetzt), Belege `reports/T-0788/fremd/` mit Rohlog und SHA-256, Eingang `fremd-eingang.ged` (GEDCOM in der Form verbreiteter Genealogie-Programme, eigene neutrale Daten mit Umlauten, _UID, _FREL, CHAN, QUAY, PEDI, NOTE-/REPO-/OBJE-Datensätzen):
- **Bericht:** Trendo-DOCX und -ODT → LibreOffice (Kopie aus `T0776_LoKopie`, eigenes Profil) liest alle 9 Prüfstellen (Umlaute, Fundstelle, Partner/Heirat/Kinder, Prüfung, Quelle, Ort) und setzt PDF; pdftotext liest Trendos Bericht-PDF.
- **Baum:** Trendo-SVG → Inkscape findet alle Texte (Anzahl = Trendo), speichert einfaches SVG mit „Jürgen Größer“ …, rendert PNG 1200 px; pdftotext liest das Baum-PDF.
- **Personenliste:** CSV → LibreOffice Calc (ODS und zurück) mit allen Zeilen.
- **GEDCOM in einem Genealogie-Programm: BLOCKIERT** – auf diesem PC liegt keines (Gramps, RootsMagic o. ä. nicht vorhanden, nichts installiert). Der Export ist mit Trendos eigenem Leser (Rundreise inhaltsgleich, zweiter Export zeichengleich) und den Formatregeln (≤ 255 Byte, CONC/CONT, Kopf) geprüft. Voraussetzung für den Nachweis: ein vorhandenes Genealogie-Programm (z. B. Gramps) oder eine Freigabe zum Herunterladen.

## Rot-Grün
Neue Testdateien gegen main 9bc135aaa (eigener Worktree, danach entfernt): übersetzen nicht (Core 18 Fehler, App 1 Fehler – Typen fehlen), `reports/T-0788/rot-alter-stand.txt`. Regressionstest `Lines_parent_first`: Befund des ersten Echtlaufs (`echtlauf-1-befund.md`) ist der Rot-Beleg. Endstand grün (Zahlen unten).

## Tests (gezielt, kein Volltest)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core: `T0788StammbaumTests` 44/44; `T0788_Fremdnachlauf` 2/2 echt; berührte Klassen (PromptAction, StudioTeams, NoFixedUser, VorbildAnalysen, T0777, WissenStudios, StudioKnowledge, Tests inkl. QA-015, Assist, T0788StammbaumTests): **202 bestanden, 0 Fehler** (`reports/T-0788/core-klassen.txt`).
- App (20 Klassen: T0788Stammbaum_Ui, PromptBarTests, StudioFolds_Ui, StudioIconTests, StudiosAlle_Ui, StudioTeams_Ui, StartMenu_StudioFenster, ClaudeEverywhere_Ui, EinfachBedienen_Ui, StudiosTechnikWissen_Ui, T0728_Bedienung_Ui, T0755_Ui, WissenStudios_Ui, Sys13_Qualitaet_Ui, StudioBilder, UebersichtEinfach_Ui, AssistChain_Ui, Assist_Ui, ClaudePrograms_Ui, T0779QuizKurse_Ui): **Total 251, Failed 3, Errors 0, Skipped 4** (`reports/T-0788/app-klassen.txt`). Die 3 Fehlschläge hängen nur am fehlenden requirements.yaml-Eintrag: `StudioFolds_Ui.Overview_and_search` („70 Studios“ erwartet, ohne Eintrag 69), `Sys13_Qualitaet_Ui.Modules` (STUDIO-FAMILY-01 ohne Modul), `StartMenu_StudioFenster.Jedes_Studio`. Übersprungen: Echtläufe ohne Schalter. `T0788Stammbaum_Ui.Real_local_ki` gesondert mit `TRENDO_T0788_ECHT` bestanden.
- **QA-015** (`tests/Trendo.Core.Tests/Tests.cs`) bleibt bei 91 und ist **jetzt noch grün**, weil es die Liste aus requirements.yaml zählt; sobald der Eintrag unten übernommen ist, wird er (mit T-0787 zusammen) rot und muss vom Sammel-Helfer angepasst werden.
- Angepasste Zähler (Katalog-Stand mit diesem Studio): `PromptActionTests` 116 Adapter / 87 teilweise, `PromptBarTests` 92 Studios, `StudioFolds_Ui` 70; `docs/improvement-42/studio-capability-matrix.md` mit `TRENDO_UPDATE_MATRIX=1` neu erzeugt; `einfach-vorher-ausgeblendet.txt` um 16 Stammbaum-Zeilen mit Begründung ergänzt (Ersatzweg „Mehr Werkzeuge“). **Zusammenführung mit T-0787:** diese Zähler steigen dort ebenfalls – beim Sammeln jeweils +1 je Studio.

## Bild geprüft
Trendo.Render (eigener `TRENDO_RENDER_ROOT`): Profi mit Beispiel, Einfach mit Beispiel, Profi leer – Dunkel-Gold, eine goldene Hauptaktion („Person“ bzw. „Erste Person“ im Leerzustand), Baum lesbar, Auswahl gold umrandet, Prüfung unten. Studio-Bild mit ComfyUI + FLUX.2 klein 4B lokal erzeugt (`tools/studio_bilder.py`, Prüfsummen ok, eigene ComfyUI auf 127.0.0.1:8189 mit eigener Datenbank, weil die Standard-Datenbank von einer anderen Sitzung belegt war; danach beendet) und angesehen.

## Abnahmekriterien
| Nr. | Urteil | Begründung |
|---|---|---|
| 1 Leere Vorlage → echtes Ergebnis | VOLLWERTIG | `Click_to_files`: leere Vorlage → erste Person → Vater → Mutter im Baum → alle acht Ausgaben als Dateien |
| 2 Fremddatei ein, Export im Fremdprogramm | TEILWEISE | Fremd-GEDCOM eingelesen; Bericht (DOCX/ODT) in LibreOffice, Baum (SVG) in Inkscape, CSV in Calc echt geöffnet. GEDCOM-Export in einem Genealogie-Programm BLOCKIERT (keins vorhanden) |
| 3 Absturz ≤ 30 s | TEILWEISE | über die Programm-Hülle (Autosave, Notsicherung, Wiederherstellung, AtomicFile); kein eigener Absturztest für dieses Studio |
| 4 KI-Fachaufgabe im offenen Dokument, rückgängig | TEILWEISE | Ersatz-KI-Test vollständig; Echtlauf qwen3:14b: Personen, Eltern, Daten und Rückgängig richtig, die Heirat aus dem Text fehlte (Gegenprüfung G10) |
| 5 Test vom Klick bis zur Datei | VOLLWERTIG | `Click_to_files`, `Import_gedcom` |
| 6 Einfach/Profi, Kürzel, Rechtsklick, Ziehen | VOLLWERTIG | `Simple_level`, `Mouse_and_keys` (Mausrad, Ziehen, Rechtsklick-Menü, Entf, Rückgängig), Ablegen einer .ged (`Import_gedcom`); Tasten + / − / 0 / Pfeile gebaut, getestet ist Entf |

## Anforderungen (Clean-Room, `docs/nachbau/stammbaum/anforderungen.md`)
S-01 … S-23 erfüllt (Prüfung je Zeile in der Datei); S-10 Doppelklick gebaut, getestet ist der gleiche Weg über „In die Mitte“ → TEILWEISE. S-24 Ortskarte, S-25 Medien, S-26 weitere Ansichten (Sanduhr/Fächer), S-27 GEDCOM 7/ANSEL schreiben, S-28 mehrere Namen/Duplikate → FEHLT (Stufe 2).

## Vorbild-Tabelle
In `docs/nachbau/stammbaum/anforderungen.md`: Family Tree Maker, RootsMagic, Legacy Family Tree, Gramps (GPL-2.0-or-later, nur Clean-Room), FamilySearch – mit Quelle und Datum 28.11.2026. Ehrlich: meist Vergleichs- und Blogseiten; die genauen Standardgrenzen der RootsMagic-Prüfungen für Elternalter fanden sich nicht öffentlich.

## requirements.yaml-Vorschlag
(Datei für Agenten gesperrt – nicht geändert. Ohne diesen Eintrag fehlt das Studio in der Übersicht und im Startmenü; siehe „Offen“.)
```yaml
- id: STUDIO-FAMILY-01
  name: Stammbaum
  approved_expansion_functions: '0'
  approval_status: APPROVED_SPEC
  implementation_status: IN_PROGRESS
  test_status: NOT_RUN
  evidence: Freigabe im Chat 09.11.2026 (79 neue Studios), Etappe 0.9.115 Wissen & Lernen E 26.11.2026 (T-0788)
```

## Was NICHT geprüft wurde
- GEDCOM-Export in einem fremden Genealogie-Programm (keins vorhanden – BLOCKIERT).
- Echte ANSEL-Datei eines Fremdprogramms: geprüft mit selbst erzeugten ANSEL-Bytes nach der Zeichentabelle (inkl. unbekanntem Zeichen).
- Öffnen in Microsoft Word (nicht vorhanden) – nur LibreOffice.
- Sehr große Dateien (> einige Tausend Personen): Grenze 50 000 Personen, Leistung nicht gemessen; jede Änderung schreibt den ganzen Stammbaum ins Dokument.
- Doppelklick im Baum und Tasten + / − / 0 / Pfeile (gebaut, nicht eigens getestet).
- Claude-Echtlauf (Kontingent, laut Auftrag nicht); Volltest (macht die Hauptsitzung).
- Bild des Studios in der Übersicht (erscheint erst mit dem requirements.yaml-Eintrag).

## Offen / Risiken
- **requirements.yaml:** bis zum Eintrag schlagen erwartbar fehl: `StudioFolds_Ui.Overview_and_search`, `Sys13_Qualitaet_Ui.Modules`, `StartMenu_StudioFenster.Jedes_Studio`; mit dem Eintrag wird QA-015 rot (91 → 92/93 mit T-0787) – Sammel-Helfer.
- **Gemeinsamer Baustein `DocxFormat`:** ein Dokument, das mit einer Tabelle endet, lässt LibreOffice beim Einlesen hängen (hier im Bericht umgangen, im gemeinsamen Schreiber nicht behoben – betrifft z. B. Trendo Text, wenn ein Dokument mit Tabelle endet). Vorschlag: eigener kleiner Auftrag (DOCX immer mit `<w:p/>` vor `w:sectPr`).
- Die KI übernimmt nur, was sie liefert; ausgelassene Angaben (Heirat im Echtlauf) ergänzt Trendo nicht.
- Gemeinsame Dateien berührt (am Ende der Listen): StudioCatalog, StudioIcons, PromptChips, CapabilityCatalog, StudioKnowledge, StudioProfiles, StudioTeams, IntelligencePromptModel, W6 (Init-Zeile), `tools/studio_bilder.py`, `docs/design/STUDIOBILDER.json`, `.gitattributes`, `docs/improvement-42/studio-capability-matrix.md`, `einfach-vorher-ausgeblendet.txt`, Zähler in PromptActionTests/PromptBarTests/StudioFoldsUiTests; klein `Zeitleiste.cs` (optionaler Titel).

## Berührte Dateien
Neu: `src/Trendo.Core/Wissen/Gedcom.cs`, `GenDate.cs`, `Stammbaum.cs`, `StammbaumAusgabe.cs`, `src/Trendo.Core/PromptAction/StammbaumAdapter.cs`, `src/Trendo.App/MainWindow.Stammbaum.cs`, `src/Trendo.App/FamilyTreeCanvas.cs`, `src/Trendo.App/Assets/studios/STUDIO-FAMILY-01.jpg`, `tests/Trendo.Core.Tests/T0788StammbaumTests.cs`, `tests/Trendo.Core.Tests/T0788FremdnachlaufTests.cs`, `tests/Trendo.App.Tests/T0788StammbaumUiTests.cs`, `docs/nachbau/stammbaum/anforderungen.md`, `reports/T-0788/**`, `tasks/T-0788-*.md`.
Geändert: `src/Trendo.App/StudioCatalog.cs`, `StudioIcons.cs`, `MainWindow.PromptChips.cs`, `MainWindow.W6.cs`, `src/Trendo.Core/PromptAction/CapabilityCatalog.cs`, `IntelligencePromptModel.cs`, `StudioKnowledge.cs`, `src/Trendo.Core/W2/StudioProfiles.cs`, `StudioTeams.cs`, `src/Trendo.Core/Wissen/Zeitleiste.cs`, `tools/studio_bilder.py`, `docs/design/STUDIOBILDER.json`, `docs/improvement-42/studio-capability-matrix.md`, `.gitattributes`, Tests `PromptActionTests.cs`, `PromptBarTests.cs`, `StudioFoldsUiTests.cs`, `Daten/einfach-vorher-ausgeblendet.txt`.

## ENTWICKLUNGSBERICHT
- Projekt: Trendo OS · Sprachen: C# (.NET 10, Avalonia 12), Python (Patch-Hilfe, Bildskript) · Rust eingesetzt: NEIN
- Grund: Projekt ist C#; GEDCOM-Lesen/Schreiben, Prüfungen und Layout sind klein und schnell genug (Core-Tests in < 1 s), keine messbare Engstelle.
- Tests: siehe oben · Gemessene Verbesserungen: keine Leistungsmessung nötig · Offene Probleme: siehe „Offen“.
