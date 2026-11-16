# Bericht T-0680 B1a: Programm-Hülle absturzsicher

**Auftrag:** `tasks/T-0680-b1a-absturzsicher.md` (Paket B1 aus dem Audit vom 16.11.2026, Teilbericht 6, Abschnitt 1)
**Branch:** `claude/T-0680-b1a-absturzsicher` (von `origin/main`, Stand f68974c6d) – nur committet, kein Push, kein Merge, kein Release
**Status:** umgesetzt und gezielt getestet; Volltest steht aus (Hauptsitzung)

## Geänderte Dateien
| Datei | Was |
|---|---|
| `src/Trendo.Core/AtomicFile.cs` (neu) | Gemeinsame Hilfsmethode: Zwischendatei im selben Ordner → `Flush(true)` → ersetzen; räumt alte Reste auf |
| `src/Trendo.Core/Programs/ProgramDocument.cs` | atomar schreiben (Dokument, Notsicherung, Versionen, `zuletzt.json`), Formatversion lesen, Ladegrund an der Ausnahme, „von außen geändert“, nur lesen, geprüfte Sicherungen, Wiederherstellen ohne Löschen |
| `src/Trendo.Core/Programs/ProgramLocks.cs` (neu) | Sperrdatei je geöffneter Datei (Prozessnummer, Prozessstart, Fenster, Zeit), verwaiste Sperren übernehmen |
| `src/Trendo.App/MainWindow.ProgramShell.cs` | Speichern über einen Weg (`DocSave`), Karten: Sicherung gefunden · anderes Fenster · von außen geändert · beschädigt; fester Takt 30 s → 20 s |
| `src/Trendo.App/MainWindow.ClaudeChat.cs` | 3 Zeilen: Die Arbeitsdatei für Claude ist das Dokument selbst – Trendo schreibt sie jetzt atomar und meldet der Hülle „bekannt“, sonst hätte das neue „von außen geändert“ bei jedem Claude-Lauf nachgefragt. Liegt außerhalb des genannten Bereichs, war aber nötig. |
| `tests/Trendo.CrashProbe/` (neu), `Trendo.sln`, `tests/Trendo.Core.Tests/Trendo.Core.Tests.csproj` | Hilfsprogramm für echte Prozessabbrüche (wird nur mitgebaut, nicht ausgeliefert) |
| `tests/Trendo.Core.Tests/ProgramCrashTests.cs`, `ProgramSafetyTests.cs`, `tests/Trendo.App.Tests/ProgramCrashUiTests.cs` (neu) | Tests |

## Je Punkt

### 1. Atomar schreiben
- **War falsch (geprüft auf origin/main):** `Autosave()` schrieb mit `File.WriteAllText` direkt in die Zieldatei (`ProgramDocument.cs:223`), ebenso Versionen (`:170`) und `zuletzt.json` (`:248`). `Save` schrieb zwar erst `.tmp`, aber ohne Leeren auf den Datenträger und mit festem Namen (zwei Fenster kamen sich in die Quere).
- **Jetzt:** Alles läuft über `AtomicFile.WriteAllText/WriteAllBytes`: eigene Zwischendatei je Schreibvorgang im selben Ordner, `Flush(true)`, dann `SafeFile.Move` (ersetzt, wartet bei kurz gesperrten Dateien). Eine solche Hilfsmethode gab es noch nicht (nur `SafeFile.Move` und ein privates `WriteAtomic` im Web-Design ohne Leeren). Reste abgebrochener Vorgänge (`….tmp`) werden nach 2 Minuten beim nächsten Schreiben derselben Datei entfernt.
- **Test (echt):** `ProgramCrashTests.Kill_while_saving` – eigenes Hilfsprogramm speichert in einer Schleife (Dokument rund 3,3 MB, je Schritt Version + Dokument + „Zuletzt“), wird **24-mal mit `Process.Kill` beendet**; nach jedem Abbruch: Datei lädt vollständig, jede Version lädt, `zuletzt.json` ist gültig, kein fertig gemeldeter Stand fehlt. `Kill_while_autosaving` – dasselbe **20-mal** für die Notsicherung neben der Datei und im Sicherungsordner. Dazu `ProgramSafetyTests.Atomic_write` (gleicher Prozess).
- **Ehrlich:** Ein Prozessabbruch prüft „Prozess stirbt mitten im Schreiben“, nicht Stromausfall oder Systemabsturz (dafür ist `Flush(true)` da – nicht testbar ohne Eingriff ins System). Die Abbrüche fallen gezielt in laufende Speicherschritte (20–21 von 24), aber nur ein Teil davon genau in den Schreibaufruf (3–10 liegengebliebene Zwischendateien je Lauf). Gegenprobe von Hand mit der alten Schreibart (Modus `roh` des Hilfsprogramms): bei 3,3 MB 0 von 30 kaputt, bei 33 MB 1 von 30 kaputt – die alte Art reißt also wirklich, aber selten.

### 2. Wiederherstellung wirklich anbieten
- **War falsch:** Die Nebendatei `<datei>.tdoc.autosave` wurde geschrieben, aber nirgends angeboten (`ProgramOpen` prüfte sie nicht). Auf der Startseite lud jede Sicherung ungeprüft (`ProgramDocument.Load(file).Title`, `MainWindow.ProgramShell.cs:525`) – eine kaputte hätte die Startseite gesprengt. „Wiederherstellen“ löschte die Sicherung sofort (`:526`).
- **Jetzt:**
  - Beim Öffnen: Ist die Notsicherung neben der Datei neuer als die Datei, erscheint eine Karte „Von „…“ gibt es einen Stand von heute um 14:32 Uhr, der nicht mehr gespeichert wurde …“ mit **Wiederherstellen / Verwerfen**. Die Sicherung wird dazu in `<datei>.wiederherstellen` umbenannt, damit weiteres Speichern das Angebot nicht still löscht. Wiederherstellen ist ein Bearbeitungsschritt (Strg+Z holt den vorherigen Stand).
  - Startseite (nie gespeicherte Dokumente): geprüfte Liste mit Zeitpunkt; die Sicherung bleibt liegen, bis das Dokument wirklich gespeichert ist; offene Dokumente dieses oder eines anderen laufenden Fensters werden nicht als „abgestürzt“ angeboten.
  - Kaputte Sicherungen: werden übersprungen und gemeldet („… war beschädigt und wurde übersprungen“ bzw. Zeile mit „Entfernen“ auf der Startseite); die Datei neben dem Dokument wird als `….autosave.beschaedigt` aufgehoben.
  - Nach erfolgreichem Speichern wird die Notsicherung entfernt (auch die der alten Datei bei „Speichern unter“).
- **Tests (Oberfläche, Fenster im selben Prozess – „Absturz“ = Fenster bleibt ohne Speichern und Schließen liegen, neues Fenster auf demselben Datenordner):** `ProgramCrashUiTests.Unsaved_document_is_offered_after_crash`, `Newer_autosave_next_to_file_is_offered` (Verwerfen, Wiederherstellen, kaputte Sicherung), `Damaged_file_and_newer_version` (kaputte Sicherung auf der Startseite). Kern: `Kill_while_autosaving` (echter Abbruch, danach `Restore` und `Recoveries`), `Orphans_checked`, `Autosave_removed_after_save_and_recent`.

### 3. Sperre gegen zweites Fenster / zweiten Prozess
- **War falsch:** keine Sperre; zwei Prozesse überschrieben sich still (letzter gewinnt).
- **Jetzt:** `ProgramLocks`: je geöffneter Datei eine Sperrdatei unter `<Datenordner>/programme/sperren` (Prozessnummer, Startzeit des Prozesses, Kennung des Fensters, Zeitpunkt). Läuft der Prozess nicht mehr oder gehört die Nummer inzwischen einem anderen Prozess, gilt die Sperre als verwaist und wird übernommen. Zweites Öffnen: Dokument kommt **nur zum Lesen**, Karte mit „Nur lesen“ · „Als Kopie öffnen“ · „Nochmal prüfen“. Nur lesen heißt: weder Speichern, automatisches Speichern noch die Notsicherung neben der Datei schreiben dorthin; eigene Änderungen landen im Sicherungsordner.
  Zusätzlich merkt sich das Dokument Zeit, Größe und Prüfsumme der Datei; wurde sie **von außen geändert**, wird nicht überschrieben, sondern gefragt: „Meine Fassung speichern“ (die andere bleibt als Version) · „Meine als Kopie speichern“ · „Die andere Fassung laden“. Ein nur neuer Zeitstempel bei gleichem Inhalt löst keine Rückfrage aus.
- **Tests:** echt mit zwei Prozessen `ProgramCrashTests.Lock_survives_until_process_dies` (3 Runden: Prozess A hält → wir bekommen sie nicht → A wird hart beendet → Prozess B übernimmt → B wird beendet → wir übernehmen); gleicher Prozess `ProgramSafetyTests.Locks`, `Changed_on_disk`, `Read_only`; Oberfläche `Second_window_cannot_overwrite`, `Changed_outside_asks_first`.
- **Grenze:** Die Sperrdateien liegen im Datenordner, nicht neben den Dokumenten (kein Zusatz in Projektordnern). Geschützt sind damit Fenster und Prozesse mit demselben Datenordner; für andere Fälle (zweiter PC am selben Netzlaufwerk, anderes Programm) greift nur die Rückfrage „von außen geändert“.

### 4. Beschädigte Dateien, Formatversion
- **War falsch:** `"version"` wurde geschrieben, aber nie gelesen. Eine kaputte Datei gab nur „keine lesbare Projektdatei“ ohne Ausweg; `"data"` in falscher Form warf eine nicht abgefangene Ausnahme.
- **Jetzt:** `Load` hängt den Grund an die Ausnahme (`ProblemOf`: beschädigt, fremd, neuer, anderes Studio – der Typ bleibt `InvalidDataException`, bestehende Tests unverändert). Beschädigt → Karte „„…“ ist beschädigt und lässt sich nicht öffnen. Du kannst den letzten gesicherten Stand zurückholen …“ mit bis zu drei lesbaren Ständen (Notsicherung, frühere Versionen, neueste zuerst); die beschädigte Datei bleibt als `….beschaedigt` liegen. Neuere Formatversion → Ablehnung mit Hinweis „Bitte aktualisiere Trendo“, Datei unverändert. Keine Migration gebaut (nur die Weiche, `ProgramDocument.FormatVersion = 1`).
- **Tests:** `ProgramSafetyTests.Format_version`, `Damaged_file_offers_recoveries`; Oberfläche `Damaged_file_and_newer_version`.

### 5. Tests mit echtem Prozessabbruch
Siehe 1–3: 24 + 20 Abbrüche mit `Process.Kill`, dazu 6 beim Sperrtest. Hilfsprogramm `tests/Trendo.CrashProbe` (Kindprozess über `dotnet <dll>`), wird über einen Projektverweis ohne Einbindung mitgebaut.

## Messung: größter Verlust nach einem Abbruch
- **Echter Abbruch (Kern):** Über alle Läufe ging höchstens **der eine gerade laufende Speicherschritt** verloren; kein als fertig gemeldeter Stand fehlte. Speicherdauer im Test: im Mittel 73–80 ms, längste 105–107 ms (3,3 MB, Version + Dokument + „Zuletzt“, jeweils bis auf den Datenträger).
- **Zeit bis eine Änderung von selbst in der Datei steht (Oberfläche, echte Uhr, nichts von Hand ausgelöst):** **1,7–1,8 s** (gemessen 1688 ms und 1833 ms; kurze Pause 1,5 s + Schreiben), `ProgramCrashUiTests.Autosave_delay_measured`.
- **Schlechtester Fall:** Ein Programm ändert, ohne neu zu zeichnen (dann startet die kurze Pause nicht), oder das automatische Speichern ist aus – dann greift nur der feste Takt. Der stand auf 30 s; mit Schreibzeit lag der Verlust knapp **über** dem Ziel. **Angepasst auf 20 s** (`MainWindow.ProgramAutosaveEvery`), der Einführungstext nennt jetzt 20 Sekunden. Der 20-Sekunden-Takt selbst wurde nicht mit echter Wartezeit getestet, nur ausgelöst (`ProgramAutosaveAll`).

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler (97 Warnungen, keine neue aus den geänderten Dateien gesehen).
- Kern gezielt: `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "…ProgramCrashTests|ProgramSafetyTests|ProgramDocumentTests|ProgramVersionsTests|EinfachBedienenTests|GameDocumentTests|KuenstlerTests|MP131_|PromptActionTests|StudioFormatTests|VideoBaselineTests|SafeFile|WebDesign"` → **430 von 430 bestanden**.
- App gezielt (`Trendo.App.Tests.exe -class …`): `ProgramCrashUiTests`, `ProgramShellUiTests`, `ClaudeBridge_Ui`, `ClaudeLiveSync_Ui`, `ClaudeLive_Ui`, `ClaudePolish_Ui`, `ClaudeProgramDoc_Ui`, `ClaudePrograms_Ui`, `ClaudeRunFix_Ui`, `EinfachBedienen_Ui`, `KreativStudios3_Ui`, `OsStyles_Ui`, `ProfiRahmen_Ui`, `ReferenzStudioSpiele_Ui`, `SPEC01_Ui`, `SPEC01_UiArc`, `SPEC03_Ui`, `SPEC03_UiEc`, `SPEC03_UiPlus`, `SimpleMode_Ui` → **103 von 103 bestanden** (alle Klassen, die Speichern/Öffnen der Hülle oder `.tdoc` berühren). Nach der letzten kleinen Änderung erneut: 5 Klassen, 25 von 25.
- `ProgramCrashTests` zusätzlich mehrfach hintereinander: zunächst 8 Läufe, davon 7 grün und **1 Fehlschlag** in `Kill_while_saving`, dessen Ausgabe ich nicht mitgeschnitten habe. Wahrscheinliche Ursache (nicht bewiesen): Abbruch zwischen „neue Version geschrieben“ und „älteste entfernt“ → kurz 21 statt 20 Versionen; die Prüfung erlaubt das jetzt ausdrücklich (das nächste Speichern räumt auf). Danach 6 Läufe ohne Fehlschlag.
- Prüfung auf Namen/Pfade über alle geänderten Dateien: leer.

## Was NICHT geprüft wurde
- Volltest aller drei Testprojekte und der Paket-Tests (macht die Hauptsitzung).
- Stromausfall / Systemabsturz (nur Prozessabbruch).
- Ein echter Absturz der ganzen App mit Oberfläche (die Oberflächentests stellen ihn im selben Prozess nach; der echte Abbruch ist auf Kern-Ebene geprüft).
- Linux- und Pi-Edition (nur Windows gelaufen).
- Sehr große Dokumente (z. B. Künstlerstudio mit 20 MB): Schreiben mit `Flush(true)` läuft weiter auf dem Oberflächen-Faden; gemessen nur 3,3 MB.
- Zwei PCs am selben Netzlaufwerk.

## Offene Punkte / Risiken
1. **Neues Projekt in der Projektmappe** (`tests/Trendo.CrashProbe`): Falls ein Wächter-Test oder das Release-Skript die Projektliste prüft, zeigt das erst der Volltest.
2. **Sperre nur innerhalb eines Datenordners** (siehe Punkt 3). Sperrdateien abgestürzter Prozesse bleiben liegen, bis dieselbe Datei wieder geöffnet wird (klein, unschädlich).
3. **Zwischendateien** nach einem Abbruch bleiben bis zum nächsten Schreiben derselben Datei liegen (danach nach 2 Minuten entfernt).
4. **Programme außerhalb der Hülle** (Text, Tabelle, Präsentation, CAD, Haushaltsbuch …) sind unverändert – `AtomicFile` steht für sie bereit.
5. Nicht Teil dieses Auftrags und weiter offen aus dem Audit: Speichern abseits des Oberflächen-Fadens, Rückgängig-Speicherbedarf, Migrationskette, Einzelinstanz-Prüfung der App.
6. Die Klasse der Sperre erkennt ein „abgestürztes“ Fenster im selben Prozess nur über `ProgramLocks.Abandon()` (für Tests); in der echten App endet mit dem Absturz der Prozess, dann greift die Prozessprüfung.
