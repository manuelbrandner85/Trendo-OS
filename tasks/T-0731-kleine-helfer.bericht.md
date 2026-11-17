# Bericht T-0731: SYS-02 Kleine Helfer & Musik

**Auftrag:** Restliste Rang 1 – Musik ohne Zusatzprogramm (sonst ehrlich BLOCKIERT mit Einrichten-Knopf), Bildschirmfoto mit Bereichswahl per Maus ohne ffmpeg.
**Branch/Arbeitskopie:** `claude/T-0731-kleine-helfer`, frische Kopie von `main` in `%TEMP%\trendo-auftrag-T-0731` (nach dem Push gelöscht).

## Was war (Beleg) → was ist jetzt
| Punkt | Vorher (audit-1 SYS-02) | Jetzt | Nachweis |
|---|---|---|---|
| Musik abspielen | nur mit nachinstalliertem mpv (`MusicPlayer.cs:49,66`); ohne mpv nur Hinweis | MP3/WAV spielt Trendo selbst: Dekodieren mit NLayer (war schon im Projekt, MIT), Ausgabe über die Windows-Tonausgabe `waveOut` (winmm.dll, gehört zu Windows, kein Admin). Andere Formate über mpv, wenn eingerichtet. Länge, Fortschritt, Springen, Pause, Lautstärke, Titelende genau einmal | `T0731HelferUiTests.Real_builtin_playback` (echt, echtes Tongerät, kein Ersatz): `reports/T-0731/wiedergabe-echt.log` – Länge 2,038 s, Gerät meldet 89 856 gespielte Werte je Kanal, Ende nach 2 789 ms bei 2 s Ton + 0,7 s Pause, Gerätezähler steht in der Pause |
| Ohne passenden Weg | stiller Hinweis bzw. Abbruch | `AudioBlockedException` → Leiste zeigt „BLOCKIERT: „.flac“ spielt Trendo nicht selbst ab … „Player einrichten“ antippen“, Knopf „Player einrichten“ daneben (vorhandene Freigabe-Einrichtung B4) | `Player_without_mpv` (UI), `reports/T-0731/oberflaeche-musik-blockiert.png` |
| Player-Leiste | rechte Spalte 300 px fest, Einrichten-Knopf schob „Warteschlange“ aus dem Bild | rechte Spalte `Auto`; Einrichten-Knopf nicht mehr golden (einzige Goldaktion bleibt „Abspielen“); Beschriftung „Mehr Formate (Player)“, solange nichts gesperrt ist | Render `oberflaeche-musik-spielt.png`, `oberflaeche-musik-blockiert.png` |
| Bildschirmfoto | „Trendo-Fenster“ = nur eigenes Fenster; ganzer Bildschirm nur mit ffmpeg; kein Bereich | neu „Bereich wählen“: Trendo klappt sich weg, nimmt den Desktop per GDI (BitBlt + CAPTUREBLT, alle Bildschirme, echte Pixel auch bei 300 % Skalierung) auf, zeigt ihn eingefroren randlos über allen Bildschirmen, Maus zieht den Bereich (Größe in Pixeln wird angezeigt), Loslassen speichert PNG in `bildschirmfotos/`, Esc bricht ab. „Ganzen Bildschirm aufnehmen“ unter Windows ohne ffmpeg (Linux weiter ffmpeg, sonst „BLOCKIERT“) | `Area_pick_with_mouse` (UI, echte Mausereignisse auf dem Auswahlfenster, echte Umrechnung, echtes PNG; nur der Desktop-Inhalt ist ein Testbild); `Real_screen_grab_foreign_window` (echt): `reports/T-0731/bildschirmfoto-fremdes-fenster.png` – Fenster eines fremden PowerShell-Prozesses, 100 % Goldpixel, 6 012 Schriftpixel, `bildschirmfoto-echt.log` |

## Geänderte/neue Dateien
- neu `src/Trendo.Core/Media/PcmSource.cs` – Ton als Strom (MP3 über NLayer, WAV über `AudioFile`), Springen.
- neu `src/Trendo.App/WinAudio.cs` – `WinAudio` (waveOut, eigener Strang, 4×100 ms Puffer), `AutoAudio` (Wegwahl eingebaut/mpv/BLOCKIERT), `AudioBlockedException`.
- neu `src/Trendo.App/ScreenGrab.cs` – `ScreenGrab` (GDI, DPI-sicher je Strang), `ScreenAreaPicker` (Auswahlfenster).
- `src/Trendo.App/MainWindow.MusicPlayer.cs` – `Audio()` nutzt `AutoAudio`; BLOCKIERT-Anzeige; Leiste.
- `src/Trendo.App/MainWindow.Sys02.cs` – Karte „Bildschirmfoto und Schwärzen“: „Bereich wählen“, Bildschirm ohne ffmpeg, `ShotAsync`.
- `tools/Trendo.Render/Program.cs` – kleiner Vorschau-Modus `TRENDO_MUSIK=gesperrt` (FLAC ohne Player) für die Bildprüfung.
- neu `tests/Trendo.App.Tests/T0731HelferUiTests.cs`, `tests/Trendo.App.Tests/Daten/ton-440hz-2s.mp3` (16 KB, mit ffmpeg/LAME erzeugt = Fremddatei).
- `reports/T-0731/*` (Rohlogs, PNG, `SHA256SUMS.txt`).
- Gemeinsame Dateien (MenuBar, ProgramShell, StudioCatalog, requirements.yaml, installer/neuerungen): **nicht** angefasst.

## Kommandos und Testergebnisse (Zahlen aus der Ausgabe)
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- `Trendo.App.Tests.exe -class …T0731HelferUiTests` (Lauf 1, mit `TRENDO_BELEG_T0731`): Total 5, Failed 0, Skipped 0 → `reports/T-0731/testlauf-1-T0731HelferUiTests.log`.
- Gezielt `MusicPlayer_Ui, AudioEditor_Ui, MediaBrowser_Ui, MpvAudioTests, MpvWindowTests, Sys02_Alltag_Ui, DeadButtonTests, T0731HelferUiTests`: Total 98, Failed 1 (Bildschirmfoto-Echtlauf, s. u.), Skipped 1 (mpv-Echtlauf: mpv nicht installiert).
- Nach den letzten Änderungen `T0731HelferUiTests, Sys02_Alltag_Ui, MusicPlayer_Ui`: Total 8, Failed 0, Skipped 1 → `testlauf-2-gezielt.log`. Der Überspringer ist der Bildschirmfoto-Echtlauf: Auf dem Bildschirm lag während dieser Läufe ein **Windows-Firewall-Dialog mit Abdunklung** (ausgelöst von einem fremden „testhost“, nicht von diesem Paket) über dem Testfenster. Die Aufnahme zeigte ihn korrekt; der Test erkennt das jetzt (`WindowFromPoint`) und überspringt ehrlich statt rot. Der Dialog wurde nicht angeklickt (Sicherheitseinstellung).
- Nachlauf `DeadButtonTests, AudioEditor_Ui, MediaBrowser_Ui, MpvWindowTests, MpvAudioTests` nach den letzten Änderungen: Total 90, Failed 0, Skipped 1 (mpv-Echtlauf, mpv nicht installiert).
- Core: `dotnet test tests/Trendo.Core.Tests --filter NoFixedUser` → Bestanden 1/1.
- Kein Volltest (macht die Hauptsitzung).

## Abnahmekriterien
1. Ergebnis aus leerer Vorlage: **TEILWEISE** – Bildschirmfoto entsteht über die Oberfläche als echte PNG; der Player ist kein „Erstellen“-Programm.
2. Fremddatei importieren/exportieren: **TEILWEISE** – fremde MP3 (ffmpeg/LAME) wird gelesen und gespielt; PNG ist Standard-PNG (mit SkiaSharp zurückgelesen); Öffnen im Fremdprogramm nicht belegt.
3. Prozessabbruch ≤ 30 s: **NICHT GETESTET** (Bildschirmfoto wird atomar über `.tmp` geschrieben; Wiedergabestand nach Absturz weiterhin weg).
4. Claude/KI-Fachaufgabe: **FEHLT** (nicht Teil des Pakets; Musik-KI-Aktion weiterhin nicht angeschlossen).
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** für „Bereich wählen“ (Knopf → Mausziehen → PNG geprüft, Desktop-Inhalt ersetzt) und echter GDI-Lauf separat; Musik: **VOLLWERTIG** (UI ohne `AudioOverride`, echtes Tongerät bis Titelende).
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **TEILWEISE** – Auswahl per Maus ziehen und Esc; keine neuen Tastenkürzel/Rechtsklick im Player.

## Nicht geprüft
- Hörbarkeit nur über den Gerätezähler belegt (Gerät meldet gespielte Werte in Echtzeit), nicht per Mitschnitt/Ohr.
- Bereichsauswahl im echten (nicht Headless-)Fenster mit echter Maus auf dem echten Desktop – absichtlich nicht, um keinen privaten Desktop abzulegen; Umrechnung ist skalierungsunabhängig, gemischte DPI über mehrere Bildschirme ungeprüft.
- Linux: eingebaute Tonausgabe und Bildschirmfoto nur Windows; unter Linux bleibt mpv/ffmpeg (mit BLOCKIERT-Meldung).
- Seek in MP3 mit variabler Bitrate (NLayer schätzt die Länge).

## Offene Punkte / Risiken
- Video weiter im fremden Fenster (mpv/Standardprogramm) – eingebettete Videowiedergabe ohne Zusatzprogramm bleibt offen.
- FLAC/OGG/M4A brauchen weiter mpv (ehrlich gemeldet).
- Wecker (`MainWindow.Helfer.cs:433`) klingelt weiter nur mit mpv – könnte `WinAudio` nutzen (Bereich Kleine Helfer, hier nicht angefasst).
- Mediathek-Klick (`Sys02.cs` `Play`) startet weiter mpv/VLC/Standardprogramm.
- Schwärzen weiterhin per eingetippter Koordinaten.

**Status:** umgesetzt, gezielt getestet, gepusht auf `claude/T-0731-kleine-helfer` (kein Merge, kein Release).
