# T-0752 Beleg – Konsolenspiele: „Freies Spiel portieren“

Stand 23.11.2026, Zweig `claude/T-0752-konsolenspiele`, Basis main (Release 0.9.107).

## Echter Portierlauf auf diesem PC: BLOCKIERT

Der Lauf vom 09.11.2026 (`cubes_demo.elf` aus glampert/ps2-homebrew, MIT, per PS2Recomp als Windows-Programm) lässt sich heute nicht wiederholen. Die Bestandsaufnahme (`bestandsaufnahme.log`, nur gelesen) zeigt:

| Voraussetzung | Stand heute |
|---|---|
| Arbeitsordner `%TEMP%\trendo-ps2` vom 09.11. | fehlt (aufgeräumt) |
| PS2Recomp (GPL-3.0) | nicht vorhanden |
| `cubes_demo.elf` | nicht vorhanden |
| CMake (damals 4.4.5 portabel) | nicht vorhanden, auch nicht als Visual-Studio-Komponente |
| C++-Compiler | vorhanden (MSVC 14.50/14.51, clang-cl in Visual Studio 18) |

Die Vorgabe lautet „nichts installieren, Werkzeuge nur nutzen, wenn sie schon da sind“. Ohne CMake und PS2Recomp lässt sich nichts bauen. Ein Download der ELF-Datei allein hätte nichts belegt. Deshalb wurde nichts geladen und kein Bau versucht.

**Damit der echte Lauf möglich wird:** PS2Recomp selbst nach `C:\ps2recomp` holen (kurzer Pfad), CMake 3.20 oder neuer in den Suchpfad legen, `cubes_demo.elf` und die `LICENSE` aus https://github.com/glampert/ps2-homebrew. Danach im Studio unter „Freies Spiel portieren“ Lizenzdatei, Programm und Ordner wählen und „Portieren starten“ drücken. Das Studio schreibt `protokoll.txt`, `config.toml`, `ergebnis.json` (mit SHA-256) und nach dem Probelauf `probelauf.txt` in den Projektordner.

## Was echt geprüft ist

- **Prüfung der Voraussetzungen auf diesem PC, echt** (`test-core.log`, Test „echte Prüfung auf diesem PC …“): Das Studio meldet hier FEHLT für PS2Recomp und CMake und OK für den Compiler. Die Lizenzdatei und die ELF-Datei in diesem Test sind Testdateien: ein MIT-Text und ein ELF-Kopf (MIPS, 32 Bit), nicht die echte `cubes_demo.elf`.
- **Probelauf mit echtem Prozess** (Test „Probelauf startet einen echten Prozess …“): Ein echter Python-Prozess gibt `[run:tick]` mit stehender pc aus. `Cli.RunStreamAsync` beendet ihn nach 3 s samt Unterprozessen, und der Lauf wird als „hängt“ erkannt. Statt des gebauten Spiels läuft hier ein Python-Skript.
- **Oberfläche**: `freies-spiel-einfach.png` und `freies-spiel-profi.png` (Trendo.Render, abgeschotteter Testmodus, findet keine Werkzeuge). Zu sehen sind der Leerzustand mit „BLOCKIERT“, die gesperrte goldene Hauptaktion „Portieren starten“, die Liste der Voraussetzungen und in Profi die Bindungen (Stubs).

## Was ersetzt ist (kein Ende-zu-Ende-Nachweis)

- CMake, PS2Recomp und das gebaute `ps2EntryRunner.exe` sind in `FreePortTests.Run_to_result` und `ConsolePort_Ui.Free_port_click_to_result` durch `FakeTools` bzw. `FreePortRunnerOverride` ersetzt. Diese legen die Dateien an, die die echten Werkzeuge erzeugen würden. Geprüft wird damit die Abfolge der Schritte, `config.toml`, das Einsortieren der `.cpp`/`.h`, `ergebnis.json` mit SHA-256, das Protokoll, der Abbruch und der Probelauf-Knopf – nicht aber, dass PS2Recomp das Spiel wirklich übersetzt.
- Die Befehlsfolge (`cmake -S/-B`, `cmake --build … --config Release`, `ps2recomp config.toml`, `--target ps2EntryRunner`) und das Format von `config.toml` (`[general] input/output/single_file_output/stubs`) stammen aus den Notizen zum Lauf vom 09.11.2026. In diesem Paket sind sie nicht gegen PS2Recomp geprüft.

## Dateien und SHA-256

Siehe `sha256.txt`.
