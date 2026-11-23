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
2. Fremddatei importieren/exportieren: NICHT BETROFFEN. Der Hinweis im ZIP-Kommentar wurde nicht in 7-Zip angesehen (NICHT GETESTET).
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
