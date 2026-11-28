# Bericht T-0785: Blender startet automatisch, voller Zugriff, stabile MCP-Verbindung

**Auftrag:** `tasks/T-0785-blender-automatisch.md`. Wunsch des Nutzers vom 28.11.2026.
**Branch:** `claude/T-0785-blender-automatisch`. Basis ist `claude/T-0784-sammel-0.9.113`, Kopf 1978f1a78 wurde vor der Korrektur hineingemergt. Ziel ist Release 0.9.114.
**Stand:** erste Runde plus Korrekturrunde nach der Gegenprüfung (H1–H3, M1–M4, N1–N5).

## Wie die Verbindung bisher funktionierte (geprüft)
- **Kette:** Claude Code startet den Blender-MCP-Server per stdio, eingetragen in der `.mcp.json` des Projekts. Trendo kopiert den Eintrag nur lesend aus `~/.claude.json`. Auf diesem PC ist das `C:\tools\blender-mcp-1.0.1` (blmcp von Blender Lab).
- **Verbindung je Aufruf:** Der Server öffnet für jeden Werkzeugaufruf eine neue TCP-Verbindung zum Add-on „MCP“ in Blender (`bl_ext.user_default.mcp` 1.0.3). Standard ist 127.0.0.1:9876, über `BLENDER_MCP_PORT` einstellbar. Das Protokoll ist JSON mit Null-Byte. Zeitgrenze im Server: 300 s je Aufruf.
- **Brücke im Add-on:** Sie startet nur, wenn das Add-on eingeschaltet ist, „Auto Start“ an ist und Blender Online-Zugriff erlaubt. Sie läuft im Hauptfaden von Blender.
- **Trendo bisher:**
  - schrieb nur die `.mcp.json`; Blender gab es nur im Modus „Voll“
  - startete Blender ohne Rücksicht auf ein schon offenes Blender
  - gab die Blender-Werkzeuge nicht frei
  - startete bei „In Blender öffnen“ jedes Mal ein weiteres Blender
  - hatte keine Überwachung, keinen Neustart und kein Statusfeld

## Was jetzt gilt

### Start
- **`BlenderLink` (Core), eine Verbindung je Sitzung.** Erst eine Kennungsabfrage (PID und offene Datei). Antwortet ein Blender, wird es weiterbenutzt; die Oberfläche zeigt die offene Datei.
- **Kein zweites Blender:** Ist ein fremdes Blender ohne Brücke offen, startet Trendo keins dazu und nennt den Grund.
- **Eigener Start:** Sonst startet Trendo `blender.exe` sichtbar, mit der zuletzt offenen bzw. jüngsten Projekt-.blend und einem per SHA-256 geprüften Startskript.
  - Skript und Statusdatei liegen in Trendos Temp-Ordner, nicht im Projekt.
  - Ohne .blend speichert Trendo die Szene als neue `szene.blend`, nie über eine vorhandene Datei.
- **Kein doppelter Start:** Gleichzeitige Anfragen laufen nacheinander (Semaphore).

### H1: Sicherung, nie über die Datei der Person
- **Benennung je Quelldatei:** `.trendo-wiederherstellung-<name>.blend` mit Begleitdatei `.json` (Quelle, Startzeit, PID), im Ordner der gerade offenen Datei. Nach einem Dateiwechsel landet die Sicherung im Ordner der neuen Datei. Zwei .blend in einem Ordner überschreiben sich nie.
- **Wann gesichert wird:** alle 30 s bei Änderungen.
- **Wann die Sicherung wegfällt:** wenn die Person speichert (`save_post`), wenn eine andere Datei geöffnet wird, und bei sauberem Schließen (`atexit`, zusätzlich Trendo bei Code 0).
- **Wiederherstellung** nur nach einem Absturz (Code ≠ 0) und nur, wenn Quelle, PID und Startzeit genau passen. Die Sicherung wird als **eigene Datei** `<name>-wiederhergestellt-<zeit>.blend` geöffnet, mit Hinweis in Blender: „EIGENE Datei … Speichern unter ‚<name>.blend‘ übernimmt die Änderungen“. Das Original bleibt unverändert; Trendo speichert nie selbst darüber.

### H2: von der Person geschlossen
- **Eigenes Blender mit Code 0:** kein Neustart. Status „Blender von dir geschlossen“, Knopf „Neu verbinden“.
- **Offenes Blender der Person geschlossen:** ebenso „von dir geschlossen“; Trendo startet keins.
- **Neustart** nur nach einem Absturz, mit Wartezeit (1, 2, 4 … höchstens 30 s) und höchstens 5 Neustarts in 10 min. Vor dem Neustart prüft Trendo, ob inzwischen ein fremdes Blender offen ist; dann startet es keins.

### H3: Werkzeuge
- **Vorab freigegeben** (`--allowedTools`) sind 24 lesende bzw. ungefährliche Werkzeuge einzeln, nicht der ganze Server: `get_*`, `search_*`, `jump_*`, `render_*`.
- **`execute_blender_code` und `execute_blender_code_for_cli`** bleiben nutzbar, laufen aber durch die Auto-Prüfung von Claude Code.
- Blender gibt es weiter nur im Modus „Voll“.

### M1: Online-Freigabe
- Ist die Online-Freigabe in Blender aus, startet die Brücke nicht. Blender öffnet seine Einstellungen (Bereich System), und Trendo zeigt eine Anleitung: „System → Netzwerk → Online-Zugriff erlauben“.
- Trendo setzt die Freigabe nie selbst; das Produkt startet ohne `--online-mode`.
- Gibt die Person frei, verbindet Trendo von selbst.

### M2: MCP-Add-on
- Ist das Add-on ausgeschaltet, fragt Trendo beim ersten Mal sichtbar, mit dem Hinweis „bleibt bei automatisch gespeicherten Einstellungen dauerhaft an“.
- Die Zustimmung merkt Trendo in `<Datenordner>/blender-link.json`. Erst dann schaltet das Skript das Add-on ein (über eine Zustimmungsdatei).
- Bei „Nein“ schaltet Trendo nichts ein und erklärt, wie die Person es selbst tun kann.
- Eigene Testinstanzen (eigenes Profil) schalten es ohne Rückfrage ein.

### M3: Port
- Ist der Port belegt, ohne dass eine Blender-Brücke antwortet, nimmt Trendo einen freien eigenen Port. Der kommt in die `.mcp.json`; sie wird nach dem Verbinden neu geschrieben.
- Läuft in Blender schon eine Brücke auf einem anderen Port, übernimmt Trendo diesen Port; die Brücke wird nie beendet oder umgesetzt.

### M4: offenes Blender der Person
- Die Oberfläche zeigt, welche Datei offen ist.
- Gehört sie nicht zum Projekt, fragt Trendo: „Projekt öffnen“ (nur ohne ungespeicherte Änderungen) oder „In der offenen Datei arbeiten“.
- Im zweiten Fall darf Claude die Datei nicht speichern; die Regel `StudioMcp.BlenderRule(false)` verbietet `save_mainfile` und `save_as` ausdrücklich.

### N1–N5
- **N1:** Solange Claude arbeitet (`Quiet()`), schickt der Herzschlag keine Kennungsabfragen, prüft nur den Port.
- **N2:** Nach `StudioLaunchProgram("blender")` läuft die Überwachung, **bis Trendo beendet wird**, nicht nur solange das Studio offen ist.
- **N3:** Die Neustart-Sperre (zu viele Abstürze oder „von dir geschlossen“) hebt nur „Neu verbinden“ auf.
- **N4:** Der ECHT-Test beendet das gehaltene `Process`-Objekt.
- **N5:** `SHA256SUMS.txt` mit LF, `reports/T-0785/** -text` in `.gitattributes`.

### Weiter wie in Runde 1
- **Aufträge** (`RunAsync`): Zeitgrenze 6 h. Wiederholbare Aufträge laufen nach dem Neuverbinden weiter, andere melden „unterbrochen“.
- **Claude-Bereich:** Statusfeld, jeder Abbruch und jedes Neuverbinden als Schritt.
- **Protokoll:** `blender.link.*` (start, neustart, abbruch, geschlossen, wiederhergestellt, port, zustimmung, addon-frage …).
- **Fehlt Blender:** ehrliche Meldung, Einrichten nur per Knopf.

## Tests (Regressionstest je Punkt, mit dem alten Stand rot)
- **Core `BlenderLinkTests`, 22 Tests** mit Ersatz-Brücke und nachgebildetem Prozess (Blender ist dabei **ersetzt**):
  - Grundfälle: Start einmal, Weiternutzen und M4-Dateianzeige, fremdes Blender ohne Brücke, eigene Instanz, Neustart nach Absturz
  - H1a Wiederherstellung als eigene Datei, H1b sauberes Schließen mit N3, H1 falsche Begleitdatei, H1 gleicher Ordner mit Dateiwechsel
  - H2 Blender der Person geschlossen
  - Verbindung getrennt, N1 Quiet, langer Lauf, Auftrag unterbrochen, N3 Neustart-Grenze
  - M1 Online-Freigabe aus, M2 Zustimmung (Ja/Nein), M3 Port belegt bzw. übernommen, M3 fremdes Blender beim Neustart
  - Skript und Projektwahl, H3 Freigabeliste und Port
- **App:** `BlenderLink_Ui` (H3-Freigabeliste mit und ohne Brücke, M4 Projekt öffnen bzw. in fremder Datei ohne Speichern, M2 Frage und Merken, Start einmal und Statusfeld, fehlendes Blender) und `StudioMcp_Ui`.
- **ECHT** `BlenderLinkEchtTests`: Blender 5.2.3 LTS mit dem echten Add-on, eigenem freien Port, eigenem Temp-Profil (`BLENDER_USER_RESOURCES`, Add-on nur dorthin kopiert, `--online-mode` nur für dieses Profil). Vorher war **kein fremdes Blender offen**.
  - Start sichtbar, Fenstertitel geprüft.
  - Der echte MCP-Server der Person meldete per stdio 26 Werkzeuge, `get_objects_summary` lief.
  - Würfel angelegt, nicht gespeichert; Sicherung mit Begleitdatei nach 30 s.
  - **Absturz** (eigenes Process-Objekt hart beendet, Code −1): Neustart, Sicherung als eigene Datei `szene-wiederhergestellt-…blend` geöffnet, Würfel darin. `szene.blend` ist **bitgleich unverändert** (SHA-256 vorher = nachher), die Sicherung ist weg.
  - **Sauberes Schließen** (gespeichert, Fenster per `CloseMainWindow` wie die Person): Code 0, Status „von dir geschlossen“, nach mehreren Herzschlägen **kein Neustart**, kein eigener Prozess mehr.
  - Beendet wurden nur eigene PIDs.
  - Beleg: `reports/T-0785/echt-blender-rohlog.txt`, `echt-dotnet-test.txt`, `SHA256SUMS.txt`.

**Ergebnisse der Korrekturrunde:**
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core (BlenderLinkTests, StudioMcpTests, KittlConnectTests, PodDesignTests, GameToolsTests, BlenderScript*, Integrations*, NoFixedUserTests): „Bestanden! Fehler: 0, erfolgreich: 82“; BlenderLinkTests allein, zweiter Lauf: 22/22.
- Core ECHT: „Bestanden! erfolgreich: 1“ (1 min 3 s).
- App: 41 berührte Testklassen, „Total: 254, Errors: 0, Failed: 0, Skipped: 8“ (die übersprungenen sind ECHT-Läufe anderer Pakete). Exitcode 0.
- **Rot-Grün:** Die neuen Tests nutzen neue Schnittstellen (`Geschlossen`, `TakeRecovery`, `RecoveryFor`, `Quiet`, `AskAddonConsent`, `BlenderSafeTools`, `BlenderAskOverride` …) und lassen sich mit dem Stand der ersten Runde nicht bauen, schlagen also fehl. Fachlich wären sie dort rot: H1a (das alte Skript speicherte über die Originaldatei), H1b und H2 (der alte Code startete neu), H3 (dort stand der ganze Server in der Freigabe).

## Korrekturrunde 2 (zweite Prüfrunde: M5, M6, M3-Rest, H2-Grenzfälle, N6–N10)

### Pflicht vor dem Merge
- **M5: Herzschlag ohne Stau.** Das Add-on nimmt Verbindungen nur im Hauptfaden an, mit einer Warteschlange von 5. Deshalb gilt jetzt:
  - Zwischen den Kennungsabfragen prüft Trendo nur, ob der Prozess lebt. Abfragen gibt es beim eigenen Blender höchstens alle 15 s, beim Blender der Person alle 45 s.
  - Unter Quiet (Claude arbeitet) und während eines Auftrags gibt es **keine** TCP-Prüfung.
  - Es ist **nie mehr als eine** Prüfverbindung offen. Bekommt sie keine Antwort, bleibt sie offen, bis Blender antwortet, statt neu zu verbinden. Der Status heißt dann „arbeitet“.
  - Verbinden hat eine eigene Zeitgrenze von 5 s. Windows meldet einen geschlossenen Port auf 127.0.0.1 erst nach etwa 2 s; eine echte Zeitüberschreitung bei lebendem Prozess heißt „arbeitet“, nicht Abbruch.
  - Das Schließen von Blender durch die Person erkennt Trendo am Prozess, ohne TCP.
- **M6:** Das Speicherverbot lässt sich in Blender nicht sicher technisch durchsetzen. `save_pre`-Handler können das Speichern nicht abbrechen. Die Texte sagen das jetzt ehrlich: „Claude soll sie nicht speichern – technisch sperren kann Trendo das nicht.“ Das steht in der Frage, im Schritt und in der Regel an Claude.
- **M3-Rest:** Nach jedem neuen Verbindungsaufbau macht Trendo fünf Kennungsabfragen.
  - Liefern sie verschiedene PIDs, lauschen zwei Brücken auf demselben Port (SO_REUSEADDR). Trendo warnt deutlich.
  - Ist Trendos eigenes Blender dabei, zieht **nur dessen** Brücke auf einen freien Port um (Code prüft die eigene PID).
  - Sind es zwei fremde Blender, verbindet Trendo sich mit keinem und erklärt, warum.
  - Ein PID-Wechsel im laufenden Betrieb löst die Prüfung neu aus.

### Mitgenommen
- **H2-Grenzfälle:**
  - Wird Blender innerhalb von 2 min ein zweites Mal hart beendet (z. B. „Task beenden“), fragt Trendo „Neu starten?“, statt weiter neu zu starten. Bei „Nein“ hilft nur „Neu verbinden“.
  - Beim Schließen von Trendo wird die Verbindung freigegeben (`Dispose`); während des Herunterfahrens startet nichts neu.
- **N6:** Speichern einer Kopie (`copy=True`, die `_for_cli`-Werkzeuge) lässt die Sicherung stehen: Der `save_post`-Pfad weicht dann von der offenen Datei ab. Bei „Speichern unter“ fällt auch die alte Sicherung weg.
- **N7:** Verwaiste Sicherungen (deren Blender läuft nicht mehr) zeigt das Statusfeld als Knopf „Sicherung von … öffnen“. Sie öffnen nie automatisch, sondern als eigene Datei; das Original bleibt unverändert.
- **N8:** `…-wiederhergestellt-…`-Dateien zählen nie als Projektdatei (`BlenderLink.IsProjectBlend`, auch in `StudioMcp.LaunchArgs`).
- **N9:** Als „Blender offen“ zählt nur ein Blender mit Fenster; Hintergrundläufe (`-b`) nicht.
- **N10:** Ein „Nein“ zum Add-on wird gemerkt. Trendo fragt erst nach „Neu verbinden“ wieder.

### Tests der Runde 2
- **Core `BlenderLinkTests`: 31 Tests**, zwei Läufe der berührten Klassen mit je 91 Tests (die vorhandene N1-Prüfung war an das neue Verhalten anzupassen: unter Quiet gibt es jetzt auch kein „verbindet neu“ mehr). Neu:
  - `M5_blocked_bridge`: blockierte Ersatz-Brücke mit Warteschlange 5, höchstens eine Prüfverbindung, kein „getrennt“, Claudes Aufruf geht danach durch
  - `M5_quiet_no_tcp` und `M5_foreign_interval`
  - `M3_duplicate_bridges`: eigenes Blender zieht um; bei zwei fremden keine Verbindung
  - `H2_quick_restart_asks` (Ja/Nein) und `H2_dispose_no_restart`
  - `N7_N8_orphans` und `N6_copy_save_keeps_recovery` (mit M6-Text)
- **App:** `N10_no_remembered`, `Close_disposes_link`, M4-Text angepasst.
- **ECHT** (eigener Port, eigenes Temp-Profil, kein fremdes Blender), zusätzlich zu Runde 1:
  - **N6:** Kopie gespeichert, die Sicherung blieb.
  - **M5:** Blender 40 s blockiert, ohne Quiet, Herzschlag alle 2 s. Status nur „Verbunden → Arbeitet“, nie „getrennt“ oder „verbindet neu“; höchstens eine offene Prüfverbindung. Ein MCP-Aufruf über den echten MCP-Server der Person, 12 s nach Beginn der Blockade gestartet, ging nach dem Ende der Blockade durch.
  - **N9:** Ein eigener Hintergrund-Blender zählte nicht als offen und wurde danach beendet.
  - Absturz → eigene Datei (Original SHA-gleich) und sauberes Schließen → kein Neustart, wie in Runde 1.
  - Rohlog SHA-256 `6608641e…`.

**Ergebnisse Runde 2:**
- Build: 0 Fehler.
- Core, berührte Klassen: zweimal „Fehler: 0“ bei 91 Tests. Nach der Anpassung der N1-Prüfung wurden N1 und M5 (7 Tests) noch einmal geprüft: grün.
- Core ECHT: „Bestanden! erfolgreich: 1“ (1 min 47 s).
- App, 41 berührte Klassen: Total 256, 1 Fehler. Das war eine Textprüfung im M4-Test, die nicht zum neuen ehrlichen Text passte. Danach `BlenderLink_Ui` und `StudioMcp_Ui`: Total 9, Failed 0. Die übrigen 247 Tests waren im 256er-Lauf grün.
- **Rot-Grün:** Die neuen Tests nutzen neue Schnittstellen (`MaxOpenProbes`, `ProbeInterval`, `AskRestart`, `IsDisposed`, `OrphanRecoveries`, `OpenOrphan`, `IsProjectBlend`, `BlenderReconnectClick` …) und sind auf dem Stand von fix1 nicht baubar. Fachlich wären M5 (dort gab es einen TCP-Test bei jedem Herzschlag) und H2-quick (dort wurde ohne Frage neu gestartet) rot.

### Nicht geprüft (Runde 2)
- Das Speicherverbot ist nicht technisch durchgesetzt (M6), nur ehrlich benannt.
- Die Frage „Neu starten?“ nach zweimaligem Beenden ist nicht echt mit dem Task-Manager geprüft, nur mit Ersatz.
- Zwei echte Blender-Brücken auf einem Port sind nicht echt nachgestellt, nur mit Ersatz.
- Eine volle Warteschlange beim **Verbinden** (Abweisung sofort) ist nur über die Toleranz „nach ‚arbeitet‘ einmal abwarten“ abgedeckt. Fremde Programme, die die Warteschlange füllen, kann Trendo nicht verhindern.

## Korrekturrunde 3 (dritte Prüfrunde: Abriss unter Quiet, „Neu verbinden“, N9)

- **Abriss unter Quiet wird sichtbar:**
  - Auch während Claude arbeitet (Quiet) oder ein Auftrag läuft, schickt Trendo alle 30 s (`QuietProbeInterval`) genau **eine** Prüfverbindung (`ProbeAsync`). Sie bleibt offen, bis Blender antwortet; es gibt keinen Stau.
  - Weist Blender die Verbindung ab, obwohl der Prozess lebt (Brücke gestoppt): erst „verbindet neu“, dann „Getrennt – die MCP-Brücke in Blender ist gestoppt …“.
  - Wurde vorher „arbeitet“ gemeldet, gilt die erste Abweisung als volle Warteschlange. Dann prüft Trendo im nächsten Herzschlag gleich noch einmal.
- **„Neu verbinden“ bei laufendem eigenen Blender ohne Brücke:** Trendo legt eine Neustart-Datei an. Das Startskript in Blender schaut alle 2 s danach und startet die Brücke neu. Wartezeit höchstens 10 s statt 180 s; klappt es nicht, kommt sofort ein Hinweis, wie die Person die Brücke in Blender startet.
- **N9:** Ein Blender ohne Fenster zählt in den ersten 15 s nach seinem Start doch als offen (`ProcessBlenderHost.CountsAsOpen`), denn sein Fenster erscheint vielleicht noch. Danach zählt ein Hintergrund-Blender nicht. Nebenwirkung: Trendos eigene kurze Hintergrundläufe zählen in ihren ersten 15 s als „offen“.
- **Tests:**
  - Der N1-Test prüft wieder „verbindet neu“ und dann „getrennt“ nach einem Abriss unter Quiet, mit längerer Wartezeit, dazu höchstens eine Prüfverbindung je Quiet-Abstand. Auf dem Stand von fix2 ist er rot, dort blieb es bei „Verbunden“.
  - Neu: `Reconnect_restarts_bridge` und `N9_young_without_window`. `M5_quiet_no_tcp` ist auf „höchstens eine Verbindung“ umgestellt. `M5_blocked_bridge` bleibt grün.
- **ECHT** (eigener Port, eigenes Temp-Profil, kein fremdes Blender), zusätzlich:
  - **Blockade 40 s:** nur „Verbunden → Arbeitet“, nie „getrennt“; der MCP-Aufruf mitten in der Blockade ging durch.
  - **Brücke unter Quiet gestoppt:** nach 33 s „verbindet neu“ (unter 60 s), Blender lief weiter. „Neu verbinden“ startete die Brücke im selben Blender in 5 s neu: verbunden, ohne Neustart von Blender.
  - **Hintergrund-Blender** nach 17 s: zählt nicht als offen.
  - Absturz → eigene Datei und sauberes Schließen → kein Neustart wie zuvor.
  - Beendet wurden nur eigene PIDs.

**Ergebnisse Runde 3** (volle Klassenläufe):
- Build: 0 Fehler.
- **App**, 48 Klassen (alle Claude*, Studio3D*, W4Studio3D, Kreatur, StudioMcp*, BlenderLink_Ui und alle Klassen, die die geänderten Wege berühren): Total 278, Errors 0, **Failed 0**, Skipped 8 (ECHT-Läufe anderer Pakete), Exitcode 0.
- **Core** (BlenderLink*, StudioMcp*, NoFixedUser, Claude*, Studio3D*, Kreatur*, KittlConnect, GameTools, BlenderScript): „Bestanden! Fehler: 0, erfolgreich: 148, übersprungen: 2“. Übersprungen sind der ECHT-Test ohne Umgebungsvariable (separat bestanden) und T-0688 ECHT.
- **Core ECHT:** „Bestanden! erfolgreich: 1“ (2 min 36 s); Rohlog SHA-256 `89254a5a…`.

## Korrekturrunde 4 (vierte Nachprüfung, N9-Nebenwirkung)
- **Problem:** `CountsAsOpen` zählte jedes Blender ohne Fenster, das jünger als 15 s ist, als offen, auch Trendos eigene `blender -b`-Läufe und die `_for_cli`-Hintergrundläufe des MCP. Fiel ein Absturz in so ein Zeitfenster, startete Trendo fälschlich nicht neu.
- **Jetzt:** Ohne Fenster zählt ein Blender nicht, wenn seine Befehlszeile `-b` oder `--background` enthält. Gezählt wird nur vor einem `--`, Anführungszeichen werden beachtet, „-b“ in einem Dateinamen zählt nicht. Ebenso nicht, wenn es ein Kindprozess von Trendo ist (Elternprozess = Trendo).
- **Befehlszeile und Elternprozess** liest `ProcessFacts` nur lesend: unter Windows über `NtQueryInformationProcess` (dieselbe Angabe wie `Win32_Process.CommandLine`, ohne neues WMI-Paket), unter Linux über `/proc`.
- Ist die Befehlszeile nicht lesbar, bleibt die bisherige Zeitregel.
- **Test** `N9_background_young_not_counted` mit nachgebildeten Prozessdaten:
  - Hintergrundlauf jünger als 15 s: nicht offen
  - eigener Kindprozess ohne Fenster: nicht offen
  - Blender der Person ohne Fenster, jünger als 15 s: offen
  - `-b` nach `--`: offen
  - Befehlszeile unlesbar: Zeitregel
  - dazu eine echte Leseprobe am eigenen Testprozess

**Ergebnisse Runde 4:**
- Build: 0 Fehler.
- Core (BlenderLink*, StudioMcp*, QualityLight, NoFixedUser): „Bestanden! Fehler: 0, erfolgreich: 49, übersprungen: 1“. Übersprungen ist der ECHT-Test ohne gesetzte Umgebungsvariable; ein ECHT-Lauf war für diese Runde nicht verlangt.
- App (BlenderLink_Ui, StudioMcp_Ui): Total 9, Failed 0.
- **Rot-Grün:** Der neue Test nutzt die neuen Parameter von `CountsAsOpen` und lässt sich mit dem Stand von fix3 nicht bauen. Fachlich wäre er dort rot: Ein Hintergrundlauf jünger als 15 s galt als offen.

## Volltest 0.9.114 (Sammel-Zweig T-0786): drei Funde behoben

1. **QA-019: Der Kern kennt keine Sockets mehr.**
   - Der Transport zur Brücke liegt hinter `IBlenderWire` (Core): Verbinden über 127.0.0.1 mit dem Ergebnis angenommen, abgewiesen oder Zeitgrenze, außerdem ein freier Port. Der Kern arbeitet nur noch mit Datenströmen (`Stream`).
   - Die echte TCP-Umsetzung ist `Trendo.App.LoopbackBlenderWire`. Sie wird beim Laden der App gesetzt (`ModuleInitializer` → `BlenderBridge.Wire`).
   - Die Core-Tests setzen eine eigene Test-Umsetzung (`BlenderTestWire`, im Testprojekt).
   - Der App-Test `Loopback_wire_real` belegt die App-Umsetzung echt: annehmen, senden und lesen (auch über `BlenderBridge.ExecuteAsync`), Abweisung bei geschlossenem Port, freier Port.
   - Der Test QA-019 ist **unverändert**.
2. **T-0764 N3: Entscheidung für eine eng begründete Ausnahme, nicht für `--factory-startup`.**
   - Warum: Mit `--factory-startup` startet Blender ohne Einstellungen und ohne Add-ons der Person. Dann fehlen ihr MCP-Add-on (die Brücke), ihre übrigen Add-ons, Tastenbelegung und Ansicht. Trendo müsste das Add-on dann mit `addon_utils.enable` aus dem Erweiterungsordner der Person nachladen. Das umgeht genau den Schutz, den N3 will (keine Benutzerskripte), und liefert trotzdem kein Blender, wie die Person es kennt.
   - Das sichtbare Blender ist ausdrücklich das der Person (Erwachsenenprofil; das Kinderprofil bekommt keine Blender-Oberfläche). Das Startskript ist wie überall per SHA-256 geprüft.
   - Umsetzung: Die Ausnahme greift nur in `BlenderLink.cs`, nur mit der Markierung `T-0785-AUSNAHME-N3` und nur an der Zeile, die das Brücken-Skript (`bruecke-…`) startet.
   - Gegenprobe im Test: genau eine Ausnahme (`Assert.Single`), mindestens drei weitere Stellen werden weiter geprüft. Ein zweiter Test (`Blender_factory_startup_exception_is_narrow`) zeigt, dass die Markierung in einer anderen Datei oder an einer anderen Zeile nichts freistellt.
3. **V10 (Übersicht Einfach):**
   - Nachweis im Code: `QualityView` (MainWindow.Qualitaet.cs) baut je Programm einen Knopf `ql-open-<n>`. In „Einfach“ kürzt `SimplifyPage` (MainWindow.SimpleMode.cs) die Seite nach den ersten Bereichen; die Liste `ql-list` liegt dann hinter „Mehr anzeigen …“ (`page-more`) und erscheint nach einem Klick vollständig.
   - Statt die eingefrorene Liste zu verlängern, steht in `Replaced` jetzt der Präfix `ql-open-` mit genau diesem Ersatzweg.
   - Belegt durch `Quality_list_behind_more`: In „Einfach“ fehlt `ql-open-0`, „Mehr anzeigen“ zeigt die ganze Liste bis zum letzten Programm, und „Öffnen“ öffnet das Programm.
   - Gegenprobe `Quality_replacement_is_narrow`: andere Elemente der Seite (`ql-filter-…`, `ql-summary`, `ql-list`, ein unbekanntes `ql-neuer-knopf`, `page-more`) fallen weiter auf.

**Ergebnisse (einzeln gelaufen):**
- Build: 0 Fehler.
- QA-019 (`QA019_Offline`) und N3 (`Blender_factory_startup`, `Blender_factory_startup_exception_is_narrow`): „Bestanden! erfolgreich: 3“.
- V10 (`UebersichtEinfach_Ui`, darunter `Simple_keeps_every_function` und die zwei neuen Tests): Total 5, Failed 0.
- BlenderLink im Kern (BlenderLink*, StudioMcp*): „Bestanden! erfolgreich: 45, übersprungen: 1“ (ECHT ohne Umgebungsvariable).
- BlenderLink in der App (`BlenderLink_Ui`, `StudioMcp_Ui`, mit `Loopback_wire_real`): Total 10, Failed 0. Der erste Lauf hatte einen Fehler in meinem neuen Test (der Ersatz-Server nahm nur eine Verbindung an); danach 10 von 10.
- Ein ECHT-Lauf mit Blender fand in dieser Runde nicht statt, denn nur der Transport wurde verlegt. Das Protokoll ist unverändert, die App-Umsetzung ist echt über Loopback geprüft.

## Abnahmekriterien
1. Echtes Ergebnis aus leerer Vorlage: **TEILWEISE**. Echt über die Verbindung (leere `szene.blend`, Würfel angelegt); der Weg über den Klick in der Oberfläche ist nur mit Ersatz-Blender geprüft.
2. Fremdprogramm/Export: **TEILWEISE**. Die echte .blend öffnet sich im echten Blender, Beleg mit SHA-256. Ein Import-Export-Kreislauf gehört nicht zu diesem Paket.
3. Höchstens 30 s Verlust: **VOLLWERTIG** für Blender. Echt geprüft, die Sicherung kommt als eigene Datei, das Original bleibt unverändert.
4. Claude/KI-Fachaufgabe: **TEILWEISE**. Der echte MCP-Server arbeitet über Trendos Verbindung; einen echten Claude-Lauf gab es nicht, das Abo wurde nicht benutzt.
5. Test vom Klick bis zur Datei: **TEILWEISE**. Die Oberfläche ist mit Ersatz geprüft, die Datei echt im Core-Test.
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **NICHT GETESTET**, kein Bedienumbau in diesem Paket.

## Nicht geprüft / offen
- **Echte Blender-Dialoge nicht im Bild geprüft:** Hinweisfenster „Wiederhergestellt nach Absturz“, die geöffneten Einstellungen bei fehlender Online-Freigabe und die Rückfrage-Dialoge in Trendo. Der Ablauf ist getestet, der Dialog-Inhalt nur als Text.
- **M1 und M2 nicht echt geprüft:** Online-Freigabe aus und Add-on aus wurden im ECHT-Lauf nicht nachgestellt (Temp-Profil mit `--online-mode`, Add-on ohne Rückfrage). Diese Fälle sind nur mit Ersatz getestet.
- **Kreatur „In Blender bauen (sichtbar)“** startet weiter ein eigenes Blender mit `--factory-startup`. Die **Prompt-Aktion `modell.blender`** bleibt im Fähigkeitskatalog gesperrt. **Hintergrundläufe** (`-b`) bleiben, wie sie sind.
- **Einzelaufrufe von Claude über 300 s** begrenzt der MCP-Server der Person; er wurde nicht verändert.
- **Absturzbild:** Ein echter Blender-Absturz ist nur über hartes Beenden nachgestellt. Ein Absturz mit Code 0 (in der Praxis selten) würde als „geschlossen“ gelten.
- **N2:** Die Überwachung nach dem Programmstart endet erst mit Trendo, nicht mit dem Studio.
- Kein Volltest (macht die Hauptsitzung).

**Status:** umgesetzt und gezielt getestet, gepusht auf `claude/T-0785-blender-automatisch`. Kein Merge, kein Release.
