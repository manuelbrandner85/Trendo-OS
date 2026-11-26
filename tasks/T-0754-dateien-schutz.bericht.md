# Bericht T-0754: Dateiaktionen rückgängig (CORE-01) und Hinweis „Sicherungen unverschlüsselt“ (CORE-11)

**Auftrag:** Restliste Rang 31 und 32 (`docs/audit/2026-11-16-profi-audit/d7-restliste.md`), freigegeben vom Nutzer am 23.11.2026.
**Branch:** `claude/T-0754-dateien-schutz` (Basis `main`, Release 0.9.107), eigene Kopie im Temp-Ordner.

## (1) CORE-01 Dateien – Rückgängig und Wiederholen

**Vorher (Beleg audit-5, Kriterium 6 „Rückgängig: FEHLT (Dateien)“):** `MainWindow.Dateien.cs` führte Verschieben, Kopieren, Umbenennen, Neuer Ordner und Löschen aus, merkte sich aber nichts. Löschen ging in den Windows-Papierkorb (`FileSystem.DeleteFile … SendToRecycleBin`), `FileBrowser.Trash` gab unter Windows nur das Wort „Papierkorb“ zurück – die Lage im Papierkorb war unbekannt.

**Jetzt:**
- Neu `src/Trendo.Core/Shell/FileUndo.cs`: Liste der Sitzung (bis 100 Schritte), ein Schritt je Aktion (Mehrfachauswahl = ein Schritt), `Undo`, `UndoMany`, `Redo`, `UndoList`/`RedoList`.
  - Verschieben/Umbenennen: zurückschieben. Kopieren: die Kopie geht in den Papierkorb (nicht endgültig). Neuer Ordner: wird nur entfernt, wenn er leer ist. Löschen: aus dem Papierkorb an den alten Platz.
  - Vor jedem Schritt werden alle Teile geprüft (Fingerabdruck Größe/Änderungszeit/Anzahl); erst danach wird gehandelt. Extern geändert, alter Platz belegt, Ordner nicht leer, Papierkorb geleert → ehrliche Meldung, es wird nichts geändert und nichts überschrieben; der Schritt kommt aus der Liste.
- Bestehendes genutzt, nichts neu erfunden: Windows-Papierkorb über den vorhandenen Weg (jetzt `FileBrowser.SystemRecycle` im Kern, vorher Lambda in der App); die Lage im Papierkorb findet `FileBrowser.FindInWindowsRecycleBin` über die vorhandene `$I`-Lesehilfe `Trash.List`/`ParseWindowsInfo` (`Sys/Sys02.cs`). `FileBrowser.Restore` legt zurück (Windows `$R`/`$I`, freedesktop `files`/`info`, Testordner `.Papierkorb`) und überschreibt nie.
- Bedienung: Strg+Z, Strg+Y (Explorer), Strg+Umschalt+Z (GNOME), ⌘Z/⌘⇧Z (Finder) in `OsBehavior.FileKey`; Knöpfe „Rückgängig“/„Wiederholen“ in der Befehlsleiste (Tooltip nennt den Schritt); Rechtsklick auf die freie Fläche mit „Rückgängig: …“, „Wiederholen: …“ und „Letzte Dateiaktionen“ (Liste, bis zu 10, mehrere Schritte auf einmal); Menüs „⋯“ (Finder) und „☰“ (GNOME); Mac-Menüleiste „Bearbeiten“ mit „Dateiaktion widerrufen/wiederholen“.

## (2) CORE-11 Daten schützen – ehrlicher Hinweis statt Verschlüsselung

**Vorher (audit-5, Kriterium 10–11):** ZIP-Sicherung unverschlüsselt im Datenordner `backups`, nirgends gesagt; Ampel „Sicherung“ grün; Studio-Satz „Schützt deine Daten, Passwörter und Sicherungen.“ (keine Passwortverwaltung vorhanden, audit-5 Punkt 16).

**Jetzt (keine Verschlüsselung, Entscheidung des Nutzers):**
- Seite „Sicherung“: gelber Kasten oben „Achtung: nicht verschlüsselt – jeder mit Zugriff auf den Ordner kann die Sicherungen lesen. Besser auf ein verschlüsseltes Laufwerk legen (z. B. BitLocker).“ mit Ordnerpfad und Knopf „Ordner öffnen“; unten der ganze Text mit Empfehlung (nur Text, Trendo ändert nichts am System; Hinweis auf restic für verschlüsseltes Sichern).
- Ergebnis jeder Sicherung: „Gesichert: … (n Dateien, geprüft, nicht verschlüsselt).“
- In der Sicherungsdatei selbst: der Vermerk (ZIP-Kommentar, T-0681) enthält jetzt den Hinweis lesbar (Umlaute nicht maskiert); `Backup.Describe` liefert ihn als `Note.Hinweis`. Ältere Sicherungen bleiben lesbar.
- Schutz-Ampel: neue gelbe Karte „Sicherungen unverschlüsselt“ mit Ordner (nie grün), Knopf „Ansehen“ führt zur Sicherung. „Jetzt sichern“ auf der Schutzseite meldet das echte Ergebnis statt pauschal „Sicherung erstellt.“; Abschnitt „Wiederherstellen“ sagt „nicht verschlüsselt“.
- Studio-Satz CORE-11: „Prüft deinen Schutz, sichert deine Daten und findet offen liegende Passwörter.“
- Prüflesen aus T-0681 unverändert (T-0681-Tests grün).
- Geprüft und gelassen: „Verschlüsselte Sicherung mit Versionen (restic)“ ist wirklich verschlüsselt (restic); „Sicher gespeicherte Schlüssel“ betrifft den Schlüsselspeicher, nicht die Sicherungen.

## Tests (Rot-Grün)

**Rot** (alter Code, Probe-Tests nur mit alten Schnittstellen, Log `reports/T-0754/rot-core.log`): `Fehler: 6, erfolgreich: 0` – Strg+Z/Strg+Y ohne Befehl, Ergebnis ohne „nicht verschlüsselt“, ZIP-Kommentar ohne „BitLocker“, keine Ampel-Karte, echter Windows-Papierkorb: „Lage im Papierkorb unbekannt: Papierkorb“. Die eigentlichen neuen Testdateien bauten auf altem Code nicht (fehlende `FileUndo`, `Restore`, `Note.Hinweis`).

**Grün** (Logs `reports/T-0754/gruen-core.log`, `gruen-app.log`, Prüfsummen `SHA256SUMS.txt`):
- Core gezielt (T0754*, OsBehaviorTests, T0681, CORE11_Security, SystemPagesTests, BackupRestore, ResticBackupTests, NoFixedUserTests): `Bestanden! Fehler: 0, erfolgreich: 44, gesamt: 44`.
- App gezielt (OsStyles_Ui, CORE11_Ui, BackupUiTests, BackupTimeline_Ui, T0681_Ui, SchutzRestic_Ui, Modern_Ui, StudiosAlle_Ui, StudioClickTests, DeadButtonTests, StudioHeadingTests, T0754DateienSchutzUi): `Total: 226, Errors: 0, Failed: 0`.
- Build `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Zwischendurch rot: `BackupTimeline_Ui` (T-0299) – der erste, große Hinweiskasten schob die Dateiliste aus dem Fenster; Kasten verkleinert (ganzer Text jetzt unten auf der Seite), danach grün. Oberfläche im Bild geprüft (Render `sicherung`, `dateien`).
- Core `T0754DateiRueckgaengigTests`: Tasten; Rundreise mit echten Dateien (5 Schritte zurück, 5 vor, UndoMany); Mehrfachauswahl + extern geändert; nie überschreiben (alter Platz belegt, Ordner nicht leer, Papierkorb geleert, belegtes Ziel beim Wiederholen); **echter Windows-Papierkorb** (Datei in den Papierkorb, Rückgängig holt sie mit Inhalt zurück, `$I` aufgeräumt, Wiederholen, dann eigene Testdatei aus dem Papierkorb entfernt → ehrliche Meldung).
- Core `T0754SicherungHinweisTests`: Hinweis in Ergebnis und ZIP-Vermerk, Prüflesen bleibt; Ampel gelb mit Ordner.
- App `T0754DateienSchutzUi`: Klickweg mit echten Dateien im Testordner (Strg+Z/Strg+Y, Knopf, Rechtsklick-Eintrag, Neuer Ordner gefüllt, extern geändert inkl. Statuszeile); Sicherungsseite (Kasten, Ordner öffnen, Meldung, Ampel, Studio-Satz).

## Abnahmekriterien
1. Aus leerer Vorlage ein echtes Ergebnis: NICHT BETROFFEN (Dateiverwaltung/Sicherung, kein Dokument-Fach).
2. Fremddatei importieren/exportieren: NICHT BETROFFEN. Der Hinweis im ZIP-Kommentar wurde nicht in 7-Zip angesehen (NICHT GETESTET). **Nachtrag T-0776:** 7-Zip 26.05 prüft die Sicherung („Everything is Ok“), entpackt bitgleich und zeigt den Vermerk; die Umlaute darin stellt 7-Zip falsch dar (`reports/T-0776/7zip-zip/`).
3. Prozessabbruch: TEILWEISE – die Liste gilt nur für die Sitzung (wie im Explorer), nach einem Neustart ist Rückgängig leer; Dateien selbst gehen nicht verloren.
4. KI-Fachaufgabe rückgängig: FEHLT – die KI hat weiter nur „desktop.zeigen“; nicht Teil dieses Auftrags.
5. Test vom Klick bis zur echten Datei: VOLLWERTIG (UI-Test mit echten Dateien im Testordner; Windows-Papierkorb echt im Core-Test).
6. Tastenkürzel, Rechtsklick: VOLLWERTIG für Rückgängig/Wiederholen (Strg+Z/Y, ⌘Z, Rechtsklick, Menüs). Ziehen und Ablegen ist rückgängig machbar.

## Nicht geprüft
- Mac- und Linux-Papierkorb echt (nur Windows-PC); freedesktop-`info`-Aufräumen ist ungeprüft.
- Windows-Papierkorb auf anderen Laufwerken als C: und bei zu großen Dateien (Windows löscht dann endgültig → Rückgängig meldet „nicht mehr im Papierkorb“; nicht nachgestellt).
- Rechtsklick-Menü und Befehlsleiste nur in Windows-/Trendo-Anordnung im Bild geprüft; Finder-„⋯“ und GNOME-„☰“ nur im Code.
- Kein Volltest (macht die Hauptsitzung).

## Offene Punkte
- In der Befehlsleiste (Trendo-Stil, Fensterbreite 1420 px) wird der letzte Knopf „Im Dateimanager des Systems öffnen“ jetzt stärker abgeschnitten.
- `docs/STUDIO_REBUILD_4_0/studios/CORE-11/SPEZIFIKATION.md` nennt noch den alten Studio-Satz (Doku, nicht angefasst).
- Andere ZIP-Sicherungen (z. B. `BusinessLaunch.Backup`) wurden nicht auf Hinweise geprüft.

## Gegenprüfung behoben (zweiter Durchgang, gleicher Zweig)

| Befund | Lösung | Test (Rot auf 12470d1af → Grün) |
|---|---|---|
| 1 MITTEL: Rückgängig/Wiederholen verschob einen fremden Ordner „Papierkorb“ aus dem Arbeitsordner, weil `Trash` das Wort „Papierkorb“ lieferte | `FileBrowser.Trash` liefert `null`, wenn das Element nicht im Papierkorb liegt. `FileUndo.Record` und `DoRedo` nehmen nur noch einen absoluten Pfad in einem echten Papierkorb an (`FileBrowser.IsTrashLocation`: `$Recycle.Bin\$R…`, `.Trash`, `Trash/files`, `.Papierkorb`). Rückgängig meldet dann „endgültig gelöscht – zurückholen geht nicht“ | Core `Fremder_Ordner_Papierkorb_bleibt` (fremder Ordner „Papierkorb“ im Arbeitsordner, Aufzeichnen mit dem Wort und Wiederholen mit endgültigem Löschen). Rot: „fremder Ordner verschoben, Done=True“ |
| 2 MITTEL: Löschen auf Laufwerk ohne Papierkorb war still endgültig, Meldung sagte „liegt im Papierkorb“ | `FileBrowser.HasTrash` (Windows: kein Papierkorb für Netzpfade, Netz-/DVD-/RAM-Laufwerke, SUBST-Laufwerke per `QueryDosDevice`; Wechseldatenträger nur mit `$Recycle.Bin`). Ohne Papierkorb wirft `Trash` `NoRecycleBinException`, es wird nichts gelöscht. Der Dateimanager fragt dann ausdrücklich („wird endgültig gelöscht und lässt sich nicht rückgängig machen – fortfahren?“, Abbrechen ist die Hauptaktion). `SystemRecycle` nutzt jetzt `SHFileOperation` mit `FOF_ALLOWUNDO | FOF_WANTNUKEWARNING` statt `UIOption.OnlyErrorDialogs`. **Korrektur (Zweitprüfung): Windows fragt dabei NICHT nach** – zusammen mit `FOF_SILENT | FOF_NOCONFIRMATION` bewirkt `FOF_WANTNUKEWARNING` nichts; der Schutz kommt erst aus Trendos eigener Vorprüfung (siehe „Zweitprüfung behoben“). Findet sich das Element danach nicht im Papierkorb, sagt die Meldung „endgültig gelöscht“; Rückgängig sagt dasselbe. Wiederholen auf Laufwerk ohne Papierkorb wird abgelehnt | Core `Kein_Papierkorb_wird_erkannt` (nachgebildet und **echt mit SUBST** auf eigenem Temp-Ordner, danach `subst /D`). Rot: „ohne Rückfrage endgültig gelöscht“ und `Trash` lieferte nicht null. App `Kein_Papierkorb_fragt_nach` (Entf → Rückfrage, Abbrechen lässt die Datei stehen, Ja löscht und meldet „endgültig gelöscht“). Echter Windows-Papierkorb mit dem neuen `SHFileOperation`-Weg: `Windows_Papierkorb_echt` grün |
| 3 NIEDRIG: Teilfehler bei Mehrfachauswahl ließ den Schritt aus beiden Listen verschwinden | Der Rest bleibt als eigener Rückgängig-Schritt, das Erledigte wird ein Wiederholen-Schritt (gleich für Wiederholen) | Core `Teilfehler_bleibt_in_der_Liste` (eine Datei gesperrt). Rot: „collection was empty“ |
| 4 NIEDRIG: alter Satz in `StudioKnowledge.cs`, Schutzseite „Gut geschützt“ trotz unverschlüsselter Sicherungen | Satz jetzt „Schutz prüfen, Daten sichern und offen liegende Passwörter finden.“; bei unverschlüsselten Sicherungen heißt die Bewertung „Brauchbar – aber deine Sicherungen sind nicht verschlüsselt (gelbe Karte unten) …“. Spezifikation unter `docs/STUDIO_REBUILD_4_0` nicht angefasst | Core `Studienwissen_ehrlich` (Rot: Satz gefunden); App `Kein_Papierkorb_fragt_nach` prüft, dass „Gut geschützt“ nicht erscheint |
| 5 optional: Befehlsleiste abgeschnitten | Der Knopf wurde einzeln gekürzt (Beschriftung aus dem Tooltip): jetzt „Im System öffnen“; zusätzlich ist `fb-commands` ein `WrapPanel` und bricht bei schmalen Fenstern um | im Bild geprüft (Render `dateien`) |

Zahlen zweiter Durchgang: Core gezielt (T0754*, OsBehavior, T0681, CORE11_Security, SystemPages, BackupRestore, Restic, NoFixedUser) `Bestanden! Fehler: 0, erfolgreich: 48`; App (T0754DateienSchutzUi, BackupTimeline_Ui, BackupUiTests, OsStyles_Ui, CORE11_Ui, T0681_Ui, StudiosAlle_Ui) `Total: 125, Failed: 0`; Build 0 Fehler. Zwischendurch rot: `OsStyles_Ui.Multi_select` erwartete „5 Elemente“ – Meldung auf „n Elemente liegen im Papierkorb“ korrigiert. Bild geprüft: Dateien (Befehlsleiste, Knopf jetzt „Im System öffnen“ statt abgeschnittenem Text), Schutz (Bewertung).

Belege: `reports/T-0754/rot2-core.log` (5 von 5 rot auf 12470d1af, Probe nur mit alten Schnittstellen), `gruen2-core.log`, `gruen2-app.log`, `SHA256SUMS.txt`. Eigene Testdateien: der echte Papierkorb-Test holt seine Datei zurück bzw. entfernt nur die eigene Testdatei; SUBST-Laufwerke sind wieder abgemeldet (`subst` leer).

## Zweitprüfung behoben (dritter Durchgang, gleicher Zweig)

| Befund | Lösung | Test (Rot auf f7b6eb43a → Grün) |
|---|---|---|
| 1 MITTEL: `SystemRecycle` löschte auf SUBST still endgültig; nicht erkannt waren auch „Dateien sofort löschen“, Element größer als der Papierkorb und Wechseldatenträger mit `$Recycle.Bin` | Neue Vorprüfung `FileBrowser.TrashProblem` / `WindowsTrashProblem` (nur lesend): kein Papierkorb (Netz, SUBST, DVD/RAM, **jetzt alle Wechseldatenträger**), `NoRecycleFiles`-Richtlinie, `NukeOnDelete` und `MaxCapacity` je Laufwerk aus `HKCU\…\Explorer\BitBucket\Volume\{GUID}` (Volume-GUID über `GetVolumeNameForVolumeMountPoint`, sonst die allgemeine Einstellung; ohne `MaxCapacity` die Windows-Voreinstellung 5 % des Laufwerks), Größe des Elements (Ordner rekursiv) gegen die Papierkorbgröße. `SystemRecycle` selbst prüft das vor dem Löschen und wirft `NoRecycleBinException` mit Grund – es löscht dann nichts. Dieselbe Trendo-Rückfrage wie bei fehlendem Papierkorb, mit Grund. Kein Windows-Dialog, keine Einstellung geändert | Core `SystemRecycle_auf_Subst_loescht_nicht` (echt: SUBST, `SystemRecycle` direkt; Rot: „SystemRecycle hat still endgültig gelöscht“), `Wuerde_endgueltig_loeschen_nachgebildet` (einstellbare Abfrage `RecycleBinPolicy`: „sofort löschen“, zu groß, Ordnergröße, Wiederholen wird abgelehnt; echte Lese-Abfrage für C: ergibt „Papierkorb vorhanden“) |
| 2 NIEDRIG: Langer Pfad (> 260) lag im Papierkorb, Trendo meldete „endgültig gelöscht“ | `FileBrowser.StripLongPrefix` schneidet `\\?\` bzw. `\\?\UNC\` vor dem Vergleich mit dem `$I`-Vermerk ab | Core `Langer_Pfad_echt` (328 Zeichen, echt in den Papierkorb, erkannt, Rückgängig holt zurück). Rot: „liegt im Papierkorb, aber nicht erkannt (null)“ |
| 3 NIEDRIG: `IsTrashLocation` zu großzügig | Jetzt Instanzmethode: `$Recycle.Bin\<SID>\$R…` nur direkt unter der Laufwerkswurzel; `.Trash` nur unter Home bzw. `.Trashes` (Mac); `Trash/files` nur unter `$XDG_DATA_HOME` bzw. `.Trash-<uid>/files` (Linux); `.Papierkorb` nur unter der Trendo-Root | Core in `Fremder_Ordner_Papierkorb_bleibt` (tiefer `$Recycle.Bin`, `.Papierkorb` ohne Root, Name ohne `$R`, `.Trash` im Temp-Ordner). Rot: zwei Probe-Fälle wurden als Papierkorb erkannt |
| 4 NIEDRIG: Rückfrage bei gemischter Auswahl unklar | `FilesNukeText`: nennt, wie viele und welche Elemente endgültig gelöscht werden (mit Grund) und wie viele normal in den Papierkorb gehen | App `Gemischte_Auswahl_wird_genannt` (Rückfrage, Ja, Meldung, Rückgängig holt nur das Abgelegte zurück) |

Zahlen dritter Durchgang: Core gezielt (T0754*, OsBehavior, T0681, CORE11_Security, SystemPages, BackupRestore, Restic, NoFixedUser) `Bestanden! Fehler: 0, erfolgreich: 51`; App (T0754DateienSchutzUi, BackupTimeline_Ui, BackupUiTests, OsStyles_Ui, CORE11_Ui, T0681_Ui, StudiosAlle_Ui) `Total: 126, Failed: 0`; Build 0 Fehler; Rot-Probe 4 von 4 rot. Bild: Dateien geprüft (unverändert, Rückfrage nur im Test geprüft). Belege `reports/T-0754/rot3-core.log`, `gruen3-core.log`, `gruen3-app.log`, `SHA256SUMS.txt`. Aufgeräumt: SUBST-Laufwerke abgemeldet, eigene Papierkorb-Einträge entfernt (ein Eintrag aus der Rot-Probe mit langem Pfad, die ihn auf altem Code nicht wiederfand), Prüfung danach: 0 eigene Einträge.

**Weiter nicht geprüft:** echte Netzlaufwerke und USB-Sticks; ein echter Lauf mit „Dateien sofort löschen“ bzw. zu großer Datei (Einstellungen werden nur gelesen, nicht verändert – die Fälle sind nachgebildet); Gruppenrichtlinien unter HKLM werden nicht gelesen.
