# Bericht T-0605: Spiele-Bibliothek im Konsolen-Studio

**Zweig:** `claude/T-0605-spiele-bibliothek`, Worktree `spiele-bibliothek`. Nach `main` übernommen am 09.11.2026 nach ausdrücklichem OK.

## Inhalt

- Bibliothek als Startseite des Konsolen-Studios: 15 Einträge mit eigenem Cover und Beschreibung, Filter nach Art. Der Disc-Weg bleibt erreichbar.
- 8 eigene Spielideen, die Claude mit Godot baut, darunter eine Open-World-Stadt. Wunschfeld für frei beschriebene Spiele.
- 5 Godot-Demoprojekte (MIT, fester Stand `6ad6167e`), geholt per Git-Teilabruf.
- 2 Homebrew-Spiele (Libbet, µCity) im Emulator mGBA 0.10.5, alle Downloads mit fester SHA-256.
- Ändern per Text und Export-Auftrag für Windows, Linux, Mac, Android und Browser.
- Geänderte Dateien: `GameLibrary.cs`, `MainWindow.GameLibrary.cs`, `MainWindow.ConsolePort.cs`, `StudioProfiles.cs`, `THIRD_PARTY.md`, `tools/spiele_cover.py`, `Assets/bibliothek/`, zwei Testdateien.

## Grenze

Bestehende kommerzielle Spiele werden nicht nachgebaut, auch nicht umbenannt oder leicht verändert. Aus einem Wunsch übernimmt Claude nur Genre und Spielmechaniken, die Regel steht in jedem Auftrag und im Profil des Studios. PlayStation-Homebrew fehlt, weil es ein Konsolen-BIOS bräuchte. SuperTuxKart und OpenTTD fehlen, weil ihre Quellpakete ohne getrennte Grafikdaten nicht laufen.

## Testergebnis

Auf `7473ac0eb`: Api 16 bestanden; Core 2151 bestanden, 5 übersprungen; Paket-Tests 9 OK. App im ersten Lauf 949 bestanden, 4 übersprungen, 1 Fehler (`CORE-07 (UI)`, Webseiten-Oberfläche, nicht von dieser Änderung berührt, Ursache im Protokoll nicht sichtbar, einzeln dreimal grün). App danach komplett wiederholt: 950 bestanden, 4 übersprungen. Ein vorheriger Lauf war rot: `QA-019`, weil der Download `HttpClient` im Kern nutzte. Behoben über `IWebFetch`.

## Echte Läufe (09.11.2026, nach Freigabe)

- Godot-Demo `3d/truck_town` über dieselben Git-Schritte wie im Studio geholt (11 MB), mit Godot 4.7.1 importiert und ohne Fenster gestartet: Exitcode 0.
- Export derselben Demo mit den offiziellen Vorlagen 4.7.1 (SHA-512 geprüft, nur im Temp-Ordner): Windows-Programm startet ohne Fenster mit Exitcode 0. Browser-Fassung lief im eingebauten Browser bis auf die fahrbare Strecke, keine Fehler in der Konsole. Export von Hand, nicht über den Studio-Auftrag an Claude.
- mGBA 0.10.5 mit Libbet gestartet: Fenster lief mit 57 Bildern pro Sekunde, danach beendet.
- Claude hat „Hafenstadt Morvane“ aus dem Studio-Auftrag gebaut. Nach dem selbst gesetzten Limit von 25 Minuten abgebrochen. Ergebnis: 5417 Zeilen GDScript, Import und Start ohne Fehler, Sichtprüfung zeigt Stadt, Verkehr, Karte, Auftragsmarkierung und Anzeigen. Rohbau-Grafik mit Figur aus Grundformen und flimmernden Fassaden. `CREDITS.md` fehlt und das eigene Prüfskript kompiliert nicht, beides wegen des Abbruchs nicht fertig.

## Nicht geprüft

- Die Knöpfe in der echten App: Die Volltests bilden Git, Godot und Emulator nach, die echten Läufe oben liefen außerhalb der App. Liegt Godot anderswo, wählt die Person es einmal mit „Godot wählen“; der Dateidialog selbst ist nur nachgebildet getestet.
- µCity im Emulator.
- Spielen von Hand: Fahren, Aufträge und Fahndung wurden nicht durchgespielt.
- Export für Android, Mac und Linux sowie der Export-Knopf im Studio.
- Keine Abnahme durch eine Person. Die Cover wurden lokal erzeugt und per Sichtprüfung kontrolliert, zwei wegen Emblem und Schriftzug ersetzt.

## Status

Auf `main`. Keine Abnahme durch eine Person, kein Release.
