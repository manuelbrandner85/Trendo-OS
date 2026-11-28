# Bericht T-0785: Blender automatisch, voller Zugriff, stabile MCP-Verbindung

**Auftrag:** `tasks/T-0785-blender-automatisch.md`. Wunsch des Nutzers vom 28.11.2026.
**Branch:** `claude/T-0785-blender-automatisch`. Basis ist `claude/T-0784-sammel-0.9.113`, Ziel Release 0.9.114. Gearbeitet wurde in einer eigenen Kopie unter `%TEMP%\trendo-auftrag-T-0785`.

## Wie die Verbindung bisher funktionierte (geprüft)
- **Kette:** Claude Code startet den Blender-MCP-Server per stdio. Er kommt aus der `.mcp.json` des Projekts; diese Datei schreibt `StudioMcp.EnsureProjectConfig`, kopiert aus `~/.claude.json` der Person, nur lesend. Auf diesem PC ist das `C:\tools\blender-mcp-1.0.1` (blmcp von Blender Lab, `python -I start.py`). Der Server öffnet **für jeden Werkzeugaufruf eine neue TCP-Verbindung** zum Add-on „MCP“ in Blender (Erweiterung `bl_ext.user_default.mcp`, Version 1.0.3). Standard ist `localhost:9876`, über `BLENDER_MCP_PORT` einstellbar. Das Protokoll ist JSON + Null-Byte, Anfrage `{"type":"execute","code":…,"strict_json":…}`. Zeitgrenze im Server: 300 s je Aufruf.
- **Brücke im Add-on:** Sie startet nur, wenn das Add-on eingeschaltet ist, „Auto Start“ an ist und Blender Online-Zugriff erlaubt. Sie läuft im Hauptfaden von Blender (Timer). Während eines langen Renders antwortet sie also erst danach, der Port nimmt aber weiter an.
- **Trendo bisher:** Trendo schrieb nur die `.mcp.json`; der MCP gab es nur im Modus „Voll“. `StudioLaunchProgram` startete Blender ohne Rücksicht auf ein schon offenes Blender, und es gab keine Prüfung, ob die Brücke läuft. Ohne Trendo-Brücke gab es kein `--allowedTools` für den Blender-Server. Mit der Trendo-Brücke stand nur `mcp__trendo` darin, die Blender-Werkzeuge waren also nicht freigegeben. „In Blender öffnen“ (3D-Studio, Kreatur) startete jedes Mal ein weiteres Blender. Es gab keine Überwachung, keinen Neustart und kein Statusfeld.

## Was jetzt neu ist
- **`BlenderLink` (Core):** eine Verbindung je Trendo-Sitzung.
  - Zuerst ein Lebenszeichen (Code `PingCode`, er liefert die PID). Antwortet Blender, wird es weiterbenutzt, auch ein fremdes, schon offenes Blender.
  - Nimmt der Port an, ohne zu antworten, heißt das „arbeitet“, nicht „Abbruch“.
  - Läuft ein fremdes Blender ohne Brücke, startet Trendo kein zweites. Stattdessen kommt ein klarer Grund und der Hinweis, was zu tun ist.
  - Sonst startet Trendo `blender.exe` direkt (die echte PID, ohne Konsolenfenster, das Blender-Fenster ist sichtbar). Mitgegeben werden die jüngste Projekt-.blend und ein per SHA-256 geprüftes Startskript (`BlenderScript`). Das Skript schaltet das MCP-Add-on bei Bedarf ein und startet dessen Brücke auf dem Port. Die Add-on-Einstellungen bleiben unverändert, `--online-mode` wird nicht gesetzt.
  - Ohne .blend wird die Szene als `szene.blend` im Projekt gespeichert.
  - Alle 30 s legt Blender eine Sicherungskopie an (`.trendo-blender-wiederherstellung.blend`), wenn sich etwas geändert hat.
  - Gleichzeitige Anfragen laufen nacheinander (Semaphore). So gibt es keinen doppelten Start.
- **Herzschlag** (5 s, solange jemand die Verbindung braucht: `Keep()`):
  - Ist das eigene Blender beendet, wird das protokolliert. Dann folgt ein Neustart mit Wartezeit (1, 2, 4 … höchstens 30 s, höchstens 5 Neustarts in 10 min, danach „getrennt“ mit Grund; „Neu verbinden“ setzt den Zähler zurück).
  - Beim Neustart wird das Projekt wieder geöffnet. Ist die Sicherung jünger, wird sie unter dem Projektnamen wiederhergestellt.
  - Fehlt der Port bei laufendem Prozess, heißt das „verbindet neu“, nach 3 Versuchen „getrennt“ mit Grund, und Trendo prüft weiter.
  - Trendo beendet nie ein Blender.
- **Aufträge** (`RunAsync`):
  - Die Zeitgrenze liegt bei 6 h. Lange Läufe gelten nicht als Abbruch.
  - Reißt die Verbindung ab, wird neu verbunden. Wiederholbare Aufträge laufen noch einmal („fortgesetzt“ im Protokoll); andere melden „unterbrochen – bitte noch einmal starten“, nie still.
  - Bei Claude-Läufen erscheint jeder Abbruch und jedes Neuverbinden als Schritt im Claude-Bereich. Zusätzlich sagt der Systemtext `StudioMcp.BlenderRule` Claude: „Bei Verbindungsfehler kurz warten und wiederholen.“
- **Automatischer Start (App):**
  - Vor jedem Claude-Lauf mit Blender-MCP (Claude-Bereich, Studio-Zeile „Los“).
  - Bei `StudioLaunchProgram("blender")`.
  - Bei „In Blender öffnen“ im 3D-Studio und bei der Kreatur. Läuft Blender schon, wird die Datei dort geöffnet, aber nur, wenn nichts Ungespeichertes verloren geht.
  - Kinderprofil: keine Blender-Oberfläche (wie bisher).
- **Voller Werkzeugumfang:** `--allowedTools` enthält jetzt `mcp__trendo-blender`, also alle 26 Werkzeuge des Servers. Die `.mcp.json` bekommt `BLENDER_MCP_PORT` = Port der Verbindung. Die Einstellung der Person bleibt unverändert. Der Blender-MCP gibt es weiterhin nur im Modus „Voll“: `execute_blender_code` ist beliebiges Python, und „Sicher“ verspricht „keine Befehle“. Rückfragen bei Löschen, Senden und Kaufen bleiben Trendo-Regeln.
- **Statusfeld:** Es steht im Claude-Bereich (`claude-blender-status`) und im 3D-Studio neben „Blender ist bereit.“ (`blender-link-status`): Punkt plus Text „Blender verbunden / arbeitet / verbindet neu / getrennt – Grund“ und der Knopf „Neu verbinden“.
- **Protokoll:** Audit-Einträge `blender.link.start|neustart|abbruch|verbunden|wieder-verbunden|fortgesetzt|getrennt|fehler|auftrag|bedarf`.
- **Ehrlichkeit:** Fehlt Blender, kommt „Blender ist auf diesem PC nicht installiert. ‚Blender einrichten‘ im 3D-Studio …“ Es wird nichts geladen. Fehlt das MCP-Add-on in Blender, kommt eine klare Meldung; Trendo lädt nichts.

## Tests
- Core `BlenderLinkTests`: 10 Tests mit **Ersatz-Brücke** (TCP, Add-on-Protokoll) und **nachgebildetem Prozess**. Geprüft werden:
  - einmaliger Start bei 5 gleichzeitigen Anfragen
  - Weiternutzen eines offenen Blender
  - fremdes Blender ohne Brücke: kein Start
  - eigene Instanz neben einem fremden Blender
  - Prozess beendet: Neustart mit demselben Projekt und Protokoll
  - Verbindung getrennt: neu verbunden, kein neuer Prozess
  - 3 s langer Lauf bei 0,4 s Lebenszeichen-Grenze: kein Abbruch
  - Absturz mitten im Auftrag (wiederholbar: fortgesetzt; sonst: klar unterbrochen)
  - Neustart-Grenze und „Neu verbinden“
  - Startskript und Projekt-/Wiederherstellungswahl
  - Port in der `.mcp.json` und Werkzeugfreigabe
- App `BlenderLink_Ui` (3 Tests): zwei Startanfragen führen zu einem Start, Statusfeld „verbunden“, Audit-Einträge; fehlendes Blender führt zu einer ehrlichen Meldung; `--allowedTools` mit und ohne Brücke.
- **ECHT** `BlenderLinkEchtTests` (läuft nur mit `TRENDO_ECHT_BLENDER`, `TRENDO_ECHT_MCP_ADDON`, optional `TRENDO_ECHT_MCP_SERVER`):
  - Blender 5.2.3 LTS mit dem echten Add-on, **eigener Port** (frei gewählt, nicht 9876) und **eigenem Temp-Profil** (`BLENDER_USER_RESOURCES`, das Add-on dorthin nur kopiert).
  - Automatisch gestartet, sichtbar (Fenstertitel `szene [...] - Blender 5.2.3 LTS`).
  - Der echte MCP-Server der Person per stdio (`python -I -B`, keine .pyc in C:\tools) meldet 26 Werkzeuge, `get_objects_summary` sieht den Würfel.
  - Würfel `TrendoWuerfel` angelegt, nach 30 s lag die Sicherungskopie vor.
  - **Eigener Prozess per PID beendet** (vorher geprüft: nicht in der Liste der schon offenen Prozesse). Danach automatischer Neustart aus der Sicherung, wieder verbunden.
  - Die Szene enthält `TrendoWuerfel`, Datei `szene.blend`, nicht verändert.
  - Beim Aufräumen wurde nur der eigene Prozess beendet.
  - Rohlog `reports/T-0785/echt-blender-rohlog.txt`, Testausgabe `echt-dotnet-test.txt`, `SHA256SUMS.txt`.

**Rot-Grün:** Alle neuen Tests nutzen neue Schnittstellen (`BlenderLink`, `blenderPort`, `HasServer`, `BlenderLinkOverride`). Mit dem alten Code lassen sie sich nicht bauen, schlagen also fehl.

**Ergebnisse:**
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core (BlenderLinkTests, StudioMcpTests, KittlConnectTests, PodDesignTests, GameToolsTests, BlenderScript*, Integrations*, NoFixedUserTests): „Bestanden! Fehler: 0, erfolgreich: 71“.
- Core ECHT (BlenderLinkEchtTests): „Bestanden! erfolgreich: 1“ (49 s).
- App: alle 41 Testklassen, die die geänderten Wege berühren (Claude-Läufe, StudioMcp, 3D-Studio, Kreatur, BlenderLink_Ui), „Total: 251, Errors: 0, Failed: 0, Skipped: 8“ (die 8 übersprungenen sind ECHT-Läufe anderer Pakete). Exitcode 0.

## Abnahmekriterien
1. Echtes Ergebnis aus leerer Vorlage: **TEILWEISE**. Im ECHT-Test entsteht eine leere `szene.blend`, Blender startet automatisch, der Würfel wird über die Verbindung angelegt. Der Weg über den Klick in der Oberfläche ist nur mit Ersatz-Blender getestet.
2. Fremdprogramm/Export: **TEILWEISE**. Die echte `.blend` öffnet sich in echtem Blender 5.2.3, Beleg unter `reports/T-0785/` mit SHA-256. Kein Import/Export-Kreislauf; das ist nicht Teil dieses Pakets.
3. Höchstens 30 s Verlust: **VOLLWERTIG** für Blender. Sicherung alle 30 s; ECHT: Prozess beendet, Würfel nach dem Neustart wieder da.
4. Claude/KI-Fachaufgabe: **TEILWEISE**. Der echte MCP-Server der Person sieht über Trendos Verbindung den Würfel (26 Werkzeuge). Kein echter Claude-Lauf, das Abo wurde nicht benutzt.
5. Test vom Klick bis zur Datei: **TEILWEISE**. UI-Test (Klick bis „verbunden“) läuft mit Ersatz-Blender; die echte Datei kommt aus dem Core-ECHT-Test.
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **NICHT GETESTET** (kein Bedienumbau in diesem Paket).

## Nicht geprüft / offen
- **Kreatur „In Blender bauen (sichtbar)“** startet weiterhin ein eigenes Blender mit `--factory-startup` (ohne Add-ons, mit Sicherheitsprüfung aus der 9. Prüfrunde). Diesen Weg über die Brücke zu leiten, wäre ein eigener Umbau.
- **Prompt-Aktion `modell.blender`** (`BlenderPromptAdapter`) bleibt im Fähigkeitskatalog „Blocked“; sie hat einen eigenen Nachweistest. Blender startet stattdessen über den Claude-Lauf bzw. „In Blender öffnen“.
- Hintergrundläufe (`blender -b`: Umwandeln, Rendern, Export) brauchen kein Fenster und keine Brücke. Sie bleiben, wie sie sind.
- **Einzelaufrufe von Claude über 300 s** begrenzt der MCP-Server der Person (blmcp, `_TIMEOUT = 300`). Er wurde nicht verändert, wie gefordert. Lange Renders laufen über die verzögerten Werkzeuge des Servers oder über Trendos `RunAsync` (6 h).
- Ist das MCP-Add-on im Profil der Person ausgeschaltet, schaltet das Startskript es ein. Hat Blender „Einstellungen automatisch speichern“ an, bleibt es danach eingeschaltet. Das ist die einzige Änderung am Profil, und nur in diesem Fall.
- Statusfeld nicht im Render-Bild geprüft (`Trendo.Render`): Im abgeschotteten Rendermodus gibt es keine Verbindung, deshalb erscheint das Feld dort nicht.
- Ein echtes Absturzbild (Blender stürzt von selbst ab) ist nur über „Prozess beenden“ nachgestellt.
- Kein Volltest (macht die Hauptsitzung).

**Status:** umgesetzt und gezielt getestet (siehe Zahlen), gepusht auf `claude/T-0785-blender-automatisch`. Kein Merge, kein Release.
