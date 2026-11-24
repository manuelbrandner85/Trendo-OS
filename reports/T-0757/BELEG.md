# T-0757 – Belege

Basis main 24bb4f70e (Release 0.9.108). Alle Pfade mit Benutzernamen sind durch `C:\Users\<user>` ersetzt, Laufwerkskennungen durch `{<guid>}`.

| Datei | Inhalt |
|---|---|
| `rot-auf-main-24bb4f70e-core.log` | `T0757RestpunkteTests` auf unverändertem main: 8 Tests, 6 rot (R1, R2, R3, B-Junction, C-^, C-Fehlalarm), 2 grün (echte NuGet-Lizenzen = Regressionsschutz; Formel-Echttest übersprungen, siehe unten) |
| `gruen-core.log` | Core nach der Korrektur: T0757*, FreePortTests, ConsolePortTests, T0754*, T0755*, NoFixedUser, OsBehaviorTests, W7Tests – 100 Tests, 96 bestanden, 4 übersprungen (nur unter Linux), 0 Fehler |
| `gruen-app.log` | App: ConsolePort_Ui, T0754DateienSchutzUi, OsStyles_Ui, T0755_Ui, SPEC12_Ui – 22/22 |
| `gruen-app-weitere.log` | App: W7_Ui, Sys03_Netz_Ui, PhoneEasyTests (nutzen FileBrowser/SystemScan) – 14, 13 bestanden, 1 übersprungen (nur unter Linux) |
| `spec12-systempruefung-echt.txt` | Echte Systemprüfung dieses PCs vor/nach (nur Zählungen): 174 Einträge, Rot 15 · Gelb 58 · Grün 101 – vorher und nachher gleich |
| `eigen-einfach.png`, `eigen-profi.png` | Trendo.Render: „Eigenes Homebrew-Programm“ mit eigener SYSTEM.CNF (`BOOT2 = cdrom0:\MEINSPIEL.ELF;1`) – Programm ✓, Haken „Ich habe die Rechte an diesem Programm“, BLOCKIERT bis zum Haken |

## Echte Werkzeuge und Dateien

- **Lizenztexte (R1):** 10 echte Texte aus dem NuGet-Cache dieses PCs (nur gelesen, Kopie im eigenen Temp-Ordner): MIT 6 (Newtonsoft.Json, CommunityToolkit.Mvvm, SkiaSharp, Microsoft.Win32.Registry, MQTTnet, Unicode.net), BSD 1 (ANGLE aus Avalonia.Angle.Windows.Natives), Apache-2.0 2 (piex, sfntly aus THIRD-PARTY-NOTICES von HarfBuzzSharp), zlib 1 (SDL aus derselben Datei). Alle gelten weiter als frei.
- **Junction (B):** echte Junction per `mklink /J` im eigenen Temp-Ordner; `TrashProblem` mit nachgebildeter Grenze 100 Bytes: Ziel (1000 Bytes) „größer“, Junction nicht. Nichts wurde gelöscht, kein Papierkorb-Eintrag entstanden.
- **Windows-Einstellungen (B):** nur gelesen. Auf diesem PC steht für C: ein MaxCapacity-Wert in der Registry (49 233 MB). Er entspricht genau der Windows-Formel (10 % von 40 960 MB + 5 % vom Rest = 49 233 MB), nicht 5 % (47 185 MB) – Windows hat die Voreinstellung also selbst nach dieser Formel gespeichert. Weil MaxCapacity gesetzt ist, greift auf diesem PC der Zweig „ohne MaxCapacity“ nicht; der Echttest mit einer sparse-Datei zwischen 5 % und Formelwert wurde deshalb übersprungen (Meldung im Log). Die Formel selbst prüft `T0757NeuTests.B_formel`.
- **Mount-Point (B):** auf diesem PC gibt es kein als Ordner eingehängtes Laufwerk, und anlegen geht nur mit Admin-Rechten. Geprüft ist `VolumePath` (GetVolumePathName) für Datei, nicht vorhandenen Pfad und Junction – jeweils `C:\`. Ein echter Mount-Point ist NICHT GETESTET.
- **Systemprüfung (C):** echter Lauf mit Trendo.Core aus main und aus diesem Zweig (eigenes kleines Zählprogramm im Temp-Ordner, nicht im Repo). Kein winget/msiexec/choco gestartet.
