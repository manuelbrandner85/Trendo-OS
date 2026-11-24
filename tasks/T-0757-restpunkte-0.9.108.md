# T-0757: Niedrige Restbefunde aus den Prüfungen von 0.9.108

**Ziel:** Die niedrigen Restbefunde aus T-0752 (Konsolenspiele), T-0754 (Dateien und Schutz) und T-0755 (Gefahren prüfen) beheben, jeweils mit Rot-Grün-Test – vom Nutzer am 23.11.2026 freigegeben.
**Branch:** claude/T-0757-restpunkte-0.9.108
**Dateien/Bereich:** `src/Trendo.Core/ConsolePort/FreePort.cs`, `src/Trendo.App/MainWindow.FreePort.cs`, `src/Trendo.Core/Shell/FileBrowser.cs`, `src/Trendo.Core/Sys/Gefahren.cs`, Vorschau-Schalter in `tools/Trendo.Render/Program.cs`, Tests, `reports/T-0757/`.
**Fertig, wenn:**
- [x] R1: MIT-Satz in unfreier Datei (nicht weitergegeben, confidential, vertraulich, all copying prohibited, kopieren verboten) wird abgewiesen; echte MIT/BSD/Apache/zlib-Texte aus dem NuGet-Cache gelten weiter; „Eigenes Homebrew“ verlangt „Ich habe die Rechte an diesem Programm“, festgehalten in ergebnis.json.
- [x] R2: eigene SYSTEM.CNF (`BOOT2 = cdrom0:\MEINSPIEL.ELF;1`) erlaubt, nur Disc-Kennungen abgewiesen, Meldung neutral.
- [x] R3: Liste `trendo-einsortiert.txt` zusätzlich im WorkDir; gelöscht wird nur, was in beiden Listen steht.
- [x] B: Windows-Formel statt 5 %; Reparse-Point als Element = Größe 0; Laufwerk über GetVolumePathName, sonst allgemeines NukeOnDelete; nur lesen.
- [x] C: `^` bei cmd entfernen; innere PowerShell-Prüfung nur bei echtem Start durch cmd/conhost/forfiles/explorer/wmic; echte Systemprüfung ohne neue Fehlalarme.
- [x] berührte Testklassen grün, Rotlauf auf main belegt.
**Nicht tun:** winget/msiexec/choco starten, Registry oder Windows-Einstellungen ändern, Fremdes löschen, nach main pushen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0757-restpunkte-0.9.108.bericht.md`.
