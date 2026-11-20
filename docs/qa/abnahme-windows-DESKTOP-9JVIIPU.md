# Abnahme TRENDO OS auf DESKTOP-9JVIIPU

Checkliste für den Windows-PC **DESKTOP-9JVIIPU**. Die Schritte sind aus dem aktuellen Code abgeleitet. Diese Datei führt die Prüfung nicht aus.

Felder **Ergebnis**, **Datum** und **Prüfer** bleiben leer, bis jemand die Schritte auf diesem PC macht.

Nicht als bestanden eintragen: QA-017, QA-019 und die Codesignatur. `TrendoOS-Setup.exe` ist nicht signiert (`installer/README.md`).

Pfade nur mit Umgebungsvariablen notieren, keine Benutzernamen ins Protokoll kopieren.

| Ort | Pfad laut Code |
|---|---|
| Programm (Velopack) | `%LOCALAPPDATA%\TrendoOS` |
| Benutzerdaten | `%APPDATA%\TrendoOS` (`DataLocation.Default`) |
| Audit | `%APPDATA%\TrendoOS\audit\audit.jsonl` |
| Absturzprotokoll | `%APPDATA%\TrendoOS\logs\absturz-*.txt` |
| Gemerkte Programmpfade | `%APPDATA%\TrendoOS\tools.json` |
| Lizenz-Zustimmung | `%APPDATA%\TrendoOS\deps-consent.txt` |

Gemeinsames Fehlerprotokoll für alle Punkte: die letzten Zeilen in `audit.jsonl` (Aktionen unten) und, falls das Fenster fehlt oder die App abbricht, die neueste Datei `absturz-*.txt`.

---

## 1. Installation und Start (Velopack, lokal)

Quelle: `installer/README.md`, `installer/build-setup.sh`, `src/Trendo.App/Program.cs`, `src/Trendo.App/MainWindow.axaml.cs` (`Reveal`), `src/Trendo.App/MainWindow.Setup.cs`.

Das Setup für diese Abnahme ist ein **lokal** gebautes `TrendoOS-Setup.exe` (Ausgabe `artifacts/setup/` von `installer/build-setup.sh`). Nicht das Setup aus einem GitHub-Release verwenden und hier nichts veröffentlichen.

1. Auf DESKTOP-9JVIIPU `TrendoOS-Setup.exe` per Doppelklick starten. Erwartung: Installation nur für den aktuellen Benutzer nach `%LOCALAPPDATA%\TrendoOS`, ohne UAC.
2. SmartScreen kann warnen, weil die Datei nicht signiert ist. Nur „Weitere Informationen → Trotzdem ausführen“ ist der vorgesehene Weg. Die Warnung nicht als bestandene Signatur werten.
3. Nach dem Setup muss ein Fenster erscheinen. Velopack startet den Prozess oft ohne sichtbares Fenster; `Reveal` holt es nach vorn und maximiert es. Fehlt das Fenster: Taskleiste prüfen, Prozess `TrendoOS` prüfen, `absturz-*.txt` lesen.
4. Verknüpfung im Startmenü und auf dem Desktop starten (zweiter Start, nicht nur der Installer-Start). Dieselbe Oberfläche, kein zweites Datenverzeichnis.
5. Ersteinrichtung: fünf Schritte „Willkommen“, „Deine Ordner“, „Aussehen“, „Was möchtest du nutzen?“, „Fertig“. „Büro & Briefe“ ist vorausgewählt und enthält LibreOffice. Blender und Unreal stehen nicht in diesem Assistenten.
6. Unter **Einstellungen** das Kontrollkästchen „Updates automatisch laden“ **ausschalten**, bevor die Integrationen geprüft werden. Sonst lädt die installierte Fassung beim Start ein GitHub-Release (`CheckUpdatesAsync`) und die lokale Fassung ist nicht mehr die geprüfte.
7. In `audit.jsonl` müssen mindestens `core.start` und, beim allerersten Start, `app.first_start` stehen. Die Kette in der Statuszeile soll „intakt“ sein, nicht „FEHLER“.

Rückfall, wenn der Start fehlschlägt: Trendo schließen. Die vorige Fassung nur nach `docs/RUECKFALL.md` (Sicherung, älteres Setup, Prüfsumme, Updates aus). Daten liegen in `%APPDATA%\TrendoOS` und sollen bei einer Neuinstallation des Programmordners erhalten bleiben. Stille Deinstallation, falls der Installer selbst kaputt ist: `%LOCALAPPDATA%\TrendoOS\Update.exe --uninstall --silent`.

| Feld | Eintrag |
|---|---|
| Ergebnis | |
| Datum | |
| Prüfer | |

---

## 2. LibreOffice

Kennung `libreoffice`. Katalog: `%ProgramFiles%\LibreOffice\program\soffice.exe`, winget `TheDocumentFoundation.LibreOffice`, Modus `AutoElevated` (Windows fragt nach Admin). Erkennung: gespeicherter Pfad in `tools.json`, sonst die Katalogpfade, sonst PATH und Registry „App Paths“ für `soffice.exe`. Die Prüfung startet LibreOffice **nicht** (`--version` hängt unter Windows oft); sie liest nur die Dateiversion.

### Erkannt

1. Seitenleiste **Texte & Tabellen** öffnet die Dokumentenseite direkt, auch wenn LibreOffice fehlt.
2. Studio-Karte „Texte & Tabellen“ (SYS-10) prüft vorher. Gefunden: „alle benötigten Programme gefunden“. Fehlt es: „LibreOffice fehlt“ und die Frage „Jetzt installieren?“.
3. **Alle Studios → Programme starten → Externe Programme**. Zeile „LibreOffice“: `✓` plus Pfad, oder `✗ … nicht gefunden`.

### Pfad

Den angezeigten Pfad mit `soffice.exe` vergleichen. Nach einer erfolgreichen Prüfung steht derselbe Pfad in `tools.json` unter `libreoffice`. Audit-Aktion `deps.integrated` bzw. `deps.check` mit `libreoffice=Working`.

### Start

Auf **Externe Programme** „Starten“ drücken. Erwartung: LibreOffice öffnet sich ohne Datei. Audit: `user.direct_launch` mit `soffice.exe`.

### Dokument öffnen

1. Auf **Texte & Tabellen** „Neuer Privatbrief (Vorlage)“ drücken. Es entsteht eine DOCX unter dem Projektordner `Dokumente`. Audit: `office.new`.
2. „In LibreOffice öffnen“ (nur sichtbar, wenn der Pfad erkannt ist). Erwartung: die zuletzt geänderte Office-Datei öffnet sich. Meldung: `LibreOffice öffnet „…“`.
3. An der Dateizeile „PDF“ drücken. Erwartung: LibreOffice läuft ohne Fenster (`--headless --norestore --convert-to pdf`, eigenes Profil, höchstens 90 Sekunden). Meldung enthält „PDF erstellt“ oder „LibreOffice meldet Fehler (Exit …)“ bzw. „LibreOffice hat zu lange gebraucht und wurde beendet“. Audit: `office.pdf` mit `ok=true` oder `ok=false`.

### Fehlerprotokoll

| Lage | Was prüfen |
|---|---|
| Seite | Meldung unter der Knopfleiste (`office-msg`) |
| Studio-Prüfung | „ist installiert, startet aber nicht“ nur, wenn ein Startbefehl läuft; bei LibreOffice ist das nicht der Normalfall |
| Audit | `deps.check`, `deps.install.fail` (exit), `office.pdf`, `user.direct_launch` |
| Absturz | `logs\absturz-*.txt` |

### Rückfall

- Nicht installiert, Seitenleiste **Texte & Tabellen**: Text „LibreOffice ist nicht installiert – Öffnen nutzt dein Standardprogramm, PDF-Export ist aus.“ „Öffnen“ startet die Datei mit dem Windows-Standardprogramm. „Speichern + PDF“ im Briefstudio bleibt aus, Tooltip „Braucht LibreOffice (Zusatzprogramme)“.
- „LibreOffice einrichten …“ fragt zuerst. „Abbrechen“ schreibt `deps.declined` und ändert nichts. „Ja, einrichten“ braucht die Lizenz-Zustimmung und Admin (winget). Schlägt das fehl: Meldung „konnte nicht eingerichtet werden“, Audit `deps.install.fail`. Danach unter **Programme starten** erneut versuchen.
- „Später“ auf der Prüffläche schließt die Installation und geht zurück, ohne LibreOffice zu starten.

| Feld | Eintrag |
|---|---|
| Ergebnis | |
| Datum | |
| Prüfer | |

---

## 3. Blender

Kennung `blender`. Katalog: `%ProgramFiles%\Blender Foundation\*\blender.exe`, winget `BlenderFoundation.Blender`, Modus `AutoElevated`. Zusätzlich PATH und Registry „App Paths“ für `blender.exe`. Die Funktionsprüfung startet `blender.exe --version` und bricht nach 25 Sekunden ab. Exit ungleich 0: Zustand „Broken“, Text „Start fehlgeschlagen (Exit …)“. Ein Treffer unter `WindowsApps` gilt als fehlend (Store-Platzhalter).

### Erkannt

1. Seitenleiste **3D & Spiele**, Karte **3D-Modelle erstellen**. Die Seite öffnet sich **ohne** Blender. Blender wird nicht beim Betreten verlangt.
2. **Programme starten → Externe Programme**, Zeile „Blender“: `✓` plus Pfad oder `✗ … nicht gefunden`.

### Pfad

Angezeigten Pfad notieren. Er muss `blender.exe` sein. Steht er in `tools.json` unter `blender`, wurde er bei der Prüfung gemerkt. Audit: `deps.check` mit `blender=Working` oder `Missing` oder `Broken`.

### Start

„Starten“ bei Blender auf **Externe Programme**. Erwartung: Blender öffnet eine leere Sitzung. Audit: `user.direct_launch`.

### Dokument / Projekt öffnen

1. Im 3D-Studio ein Teil hinzufügen (Würfel oder eine andere Form). Die Vorschau kommt von Trendo selbst, nicht von Blender.
2. „In Blender öffnen“ bzw. den Blender-Export der aktuellen Szene auslösen. Trendo schreibt eine `.blend` und startet `blender.exe` mit dieser Datei. Meldung: `Blender öffnet ….blend`. Audit: `user.direct_launch` mit der Blend-Datei.
3. Liegt schon eine `.blend` vor, öffnet „In Blender öffnen“ genau diese Datei. Andere Formate (`.glb`, `.gltf`, `.obj`, `.stl`, `.fbx`, `.ply`) werden per `--python-expr` in eine leere Szene importiert.

### Fehlerprotokoll

| Lage | Was prüfen |
|---|---|
| Seite | „Blender konnte das Objekt nicht öffnen: …“ oder „Blender konnte die Datei nicht schreiben: …“ |
| Prüffläche | „Blender fehlt. Trendo fragt einmal, bevor etwas installiert wird.“ oder „ist installiert, startet aber nicht“ |
| Audit | `deps.check`, `deps.install.fail`, `user.direct_launch` |
| Absturz | `logs\absturz-*.txt` |

### Rückfall

- Ohne Blender bleiben Bauen, Maße und die eingebaute Vorschau nutzbar. Erst Export oder „In Blender öffnen“ öffnet die Rückfrage.
- „Später“ bricht die Installation ab (`deps.declined`) und kehrt zur vorherigen Seite zurück. Die Szene in Trendo bleibt.
- „Ja, installieren (Windows fragt nach Admin)“ nur nach Freigabe im Protokoll (`approval.requested` / `approval.approved`, Aktion `deps.install`, Ziel `blender`). Schlägt winget fehl, bleibt die Vorschau in Trendo der Rückfall. Meldung „konnte nicht eingerichtet werden“.
- „Ordner“ öffnet den 3D-Ordner im Explorer, auch wenn Blender fehlt.

| Feld | Eintrag |
|---|---|
| Ergebnis | |
| Datum | |
| Prüfer | |

---

## 4. Unreal

Kennung `unreal`. Modus `Guided`: keine automatische Installation, Epic-Konto, nicht weitergebbar. Katalogpfad: `%ProgramFiles%\Epic Games\UE_*\Engine\Binaries\Win64\UnrealEditor.exe`. Die Prüfung startet den Editor nicht, sie liest nur die Dateiversion.

Zusätzlich liest das Spiele-Studio `%ProgramData%\Epic\UnrealEngineLauncher\LauncherInstalled.dat` (`InstallationList`, Einträge `UE_…`) und baut den Editorpfad als `InstallLocation\Engine\Binaries\Win64\UnrealEditor.exe`. Beide Quellen können auseinanderlaufen. Beide notieren.

### Erkannt

1. Seitenleiste **3D & Spiele**, Karte **Spiele bauen**.
2. Fehlt `UnrealEditor.exe`: die Studio-Seite öffnet sich nicht. Stattdessen die Prüffläche „Spiele bauen braucht Unreal Engine“ und der Knopf „Offiziellen Installer öffnen“ (Adresse aus dem Katalog: `https://www.unrealengine.com/download`).
3. Ist der Editor erkannt: Seite **Spiele**. Steht in `LauncherInstalled.dat` eine Engine, zeigt die Seite „Gefunden: Unreal …“. Sonst: „Unreal Engine ist nicht installiert“ und „Epic Launcher holen“ (`https://store.epicgames.com/download`). Diesen Unterschied im Ergebnis festhalten.
4. **Programme starten → Externe Programme**, Zeile „Unreal Engine“: `✓` plus Pfad von `UnrealEditor.exe`, oder `✗ … nicht gefunden`. Der Pfad kommt zuerst aus `LauncherInstalled.dat`, sonst aus `tools.json` / Katalog / PATH.

### Pfad

`UnrealEditor.exe` vollständig notieren (ohne Benutzerordner auszuschreiben, wenn er einen Benutzernamen enthält). Version aus der Prüffläche oder aus `tools.json` (`unreal`). Audit: `deps.check` mit `unreal=Working` oder `Missing`.

### Start

„Starten“ auf **Externe Programme**, nur wenn die Zeile `✓` ist. Erwartung: der Editor startet ohne Projekt. Das dauert. Audit: `user.direct_launch` mit `UnrealEditor.exe`.

### Projekt öffnen

1. Auf der Spiele-Seite, wenn eine Engine aus `LauncherInstalled.dat` kommt: Namen eintragen, „Unreal-Projekt anlegen“. Es entsteht ein Ordner mit `Content`, `Config` und einer `.uproject` (Blueprint, ohne C++). Audit: `game.unreal_project` mit der Engine-Version.
2. Existiert `UnrealEditor.exe` am abgeleiteten Pfad, startet der Editor mit dieser `.uproject`. Meldung: „Der Editor startet (das dauert).“
3. In „Deine Spiele“ bei einem Unreal-Eintrag „In Unreal Engine öffnen“. Das übergibt die `.uproject` an den Editor.

### Fehlerprotokoll

| Lage | Was prüfen |
|---|---|
| Seite | „Hat nicht geklappt: …“ unter der Spiele-Seite |
| Prüffläche | „Unreal Engine fehlt“ bzw. „Offiziellen Installer öffnen“ ist bei Offline aus |
| Audit | `deps.check`, `deps.declined`, `game.unreal_project`, `user.direct_launch` |
| Absturz | `logs\absturz-*.txt` (der Editor selbst schreibt sein Log nicht hierher) |

### Rückfall

- „Später“ auf der Prüffläche schreibt `deps.declined` für `unreal` und geht zur Studio-Liste zurück. Die Spiele-Seite mit den Browser-Vorlagen wird dabei nicht geöffnet.
- Ist die Spiele-Seite offen und keine Engine in `LauncherInstalled.dat`: „Epic Launcher holen“ öffnet nur die Epic-Seite. Anlegen eines Unreal-Projekts gibt es dann nicht.
- Browser-Spiel „Anlegen und spielen“ (Schlange, Mauerbrecher, Hüpfspiel) läuft ohne Unreal, sobald die Spiele-Seite offen ist. Es öffnet `index.html` im Browser. Audit: `game.create`. Das ist der Rückfall fürs Spielen, nicht ein Unreal-Projekt.
- Trendo installiert Unreal nicht per winget. Ein fehlender Editor bleibt fehlend, bis der Epic Launcher ihn legt und Trendo den Pfad neu prüft (Studio erneut öffnen oder **Externe Programme** neu laden).

| Feld | Eintrag |
|---|---|
| Ergebnis | |
| Datum | |
| Prüfer | |

---

## Kurz zum Abhaken

| Punkt | Ergebnis | Datum | Prüfer |
|---|---|---|---|
| 1 Installation und Start | | | |
| 2 LibreOffice erkannt | | | |
| 2 Pfad | | | |
| 2 Start | | | |
| 2 Dokument öffnen und PDF | | | |
| 2 Fehlerprotokoll gelesen | | | |
| 2 Rückfall ohne LibreOffice | | | |
| 3 Blender erkannt | | | |
| 3 Pfad | | | |
| 3 Start | | | |
| 3 Blend-Datei öffnen | | | |
| 3 Fehlerprotokoll gelesen | | | |
| 3 Rückfall ohne Blender (Vorschau in Trendo) | | | |
| 4 Unreal erkannt (Katalogpfad und LauncherInstalled.dat) | | | |
| 4 Pfad | | | |
| 4 Start | | | |
| 4 uproject öffnen | | | |
| 4 Fehlerprotokoll gelesen | | | |
| 4 Rückfall (Später / Browser-Spiel) | | | |
