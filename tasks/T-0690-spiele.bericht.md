# Bericht T-0690: Spiele-Studio produziert echte spielbare Spiele

Zweig `claude/T-0690-spiele` (Basis `claude/T-0685-b1-integration`). Belege mit Rohlog und SHA-256: `reports/T-0690/`.

## Was war → was ist jetzt

| Punkt | Vorher (Audit 3, CORE-06) | Jetzt | Beweis |
|---|---|---|---|
| 1 Frei gestaltbares Spiel (HTML5) | Vier feste Spielarten. Nur der Plattformer hatte einen Editor. Kein Browser-Lauf mit Tasten belegt. | Das Kachelspiel hat zwei Blickrichtungen: **von der Seite** (Schwerkraft, springen) und **von oben** (vier Richtungen, ohne Schwerkraft; Labyrinth, Abenteuer, Sammelspiel). Gegner laufen hin und her oder **verfolgen** die Figur. Neue Regeln: „alle Münzen“, „alle Gegner“ sowie die Wirkung „verloren“. **Eigene Bilder je Objektart** (Boden, Münze, Gegner, Stacheln, Ziel, Schlüssel, Tür, Gegenstand, Figur). Dazu wie bisher Figur-Animationen, Töne, mehrere Level, Speichern, Menü. Die Spielbarkeitsprüfung kann jetzt auch „von oben“. Die Vorlage „Abenteuer von oben“ ist sofort spielbar (vorher nur eine Spielbibel ohne Spiel). | **ECHT:** `GameFree_Ui.Browser_top_down_win` und `Browser_side_win_and_lose` bauen das Spiel über die Oberfläche und spielen es in echtem Chrome mit echten Tastendrücken (CDP `Input.dispatchKeyEvent`) durch: laufen, Münze, Wand hält auf, Sprung, Regel zählt, Ziel = „Gewonnen“, Stacheln = „Vorbei“, Leertaste = neu. Rohlog, Bilder und SHA-256: `reports/T-0690/browser-*`. Ohne Chrome/Edge wird der Test übersprungen. |
| 2 Godot als zweiter Weg | Nur der Plattformer. Der echte Lauf war nur ein Einzelbeleg vom 06.11. Im Test war Godot ersetzt. | Jedes Kachelspiel (beide Blickrichtungen) wird ein Godot-4-Projekt (GDScript, `main.tscn`). Von oben schweben die Figur und die Gegner, die Gegner laufen bzw. verfolgen, die neuen Regeln gelten. Neue **Durchspiel-Prüfung** `tests/selfplay_check.gd`: Sie steuert die Figur über Eingabe-Aktionen (laufen, Wand, Münze, Sprung, Ziel; von oben: Wand, Münze, Ziel; Stacheln = verloren). Sie gehört auch zu „Godot prüfen“ in der Oberfläche. | **ECHT** mit Godot 4.7.2 auf diesem PC: `GameFreeTests.Godot_real_run` (Import und 6 Prüfungen je Blickrichtung; Gegenprobe: ein kaputtes Skript wird erkannt) und `GameFree_Ui.Godot_real_from_ui` (Klick-Weg bis zum Bericht, echte Prozesse). `reports/T-0690/godot-*.txt`. Ohne Godot wird der Test übersprungen. |
| 3 Unreal ehrlich | „AAA+“ und „Open-World AAA+ mit Lumen, Nanite, World Partition“ versprachen mehr, als fünf Render-Schalter leisten. `spiel.unreal` hieß „Unreal-Projekt anlegen“. | Der Umfang heißt jetzt **„Großprojekt 3D“**, die Vorlage **„Offene Welt (Unreal)“**. Der Bauen-Text sagt ehrlich, dass nur fünf Bild-Einstellungen gesetzt werden. Der Satz des Programms lautet nicht mehr „bis AAA+“. Die KI-Aktion heißt jetzt „Leeres Unreal-Projekt anlegen (nur Projektdatei, noch kein Spiel)“, der Knopf in der Werkstatt „Leeres Unreal-Projekt anlegen“. | UE 5.8 ist installiert. Ein neuer Lauf: **NICHT GETESTET**. Der Unreal-Weg kompiliert ein C++-Spiel (UBT/Cook), das dauert lange und belegt den Unreal-Editor des Nutzers. Laut Auftrag: kein Großbau, Unreal nicht umkonfigurieren. Es gelten weiter nur die Einzelbelege `reports/paket01/unreal-*-e2e.md` (07.11.). |
| 4 Spiel aus einem Satz | Nach „Übernehmen“ zeigte die Übersicht nur „Bauen macht es spielbar“. Ein Level ohne Start oder Ziel blieb stehen. | Die KI kennt das Feld `view`. Fehlen Start oder Ziel, setzt Trendo ein spielbares Startlevel der Blickrichtung ein; ein gültiges eigenes Level bleibt. „Übernehmen“ öffnet bei einem Kachelspiel direkt die **Mal-Fläche** mit „▶ Spielen“. Der ganze Auftrag ist ein Rückgängig-Schritt. Claude bekommt die Blickrichtung im Fokus-Text und kann von oben Level ohne Boden setzen. | `GameFree_Ui.Prompt_playable` (KI-Antwort im Test ersetzt; Spielen bis zum Sieg, malen, zweimal Rückgängig), `GameFreeTests.Director_playable` |
| 5 Oberfläche wie ein Spiele-Editor | „Im Fenster spielen“ war eine von mehreren goldenen Schaltflächen, „Als Godot/Unity“ waren ebenfalls gold. | „▶ Spielen“ ist die goldene Hauptaktion der Level-Ansicht. Neu sind die Zeile „Blick: Von der Seite / Von oben“, die Rubrik „Eigene Bilder“ und das Feld „Verfolgt die Spielfigur“ am Gegner. Von oben zeichnet die Mal-Fläche graue Mauern ohne Grasrand. Ins Probespiel kommen die Tasten hoch und runter. Im Bereich Bauen gibt es „Im echten Browser prüfen“. | Bilder `reports/T-0690/editor-von-oben.png`, `editor-von-der-seite.png` (Render geprüft) |

## Was „spielbar“ jetzt wirklich heißt
- **Geht:** 2D-Kachelspiele, selbst gemalt (bis 20 Level, 16 × 200 Kacheln). Von der Seite: Jump'n'Run, Plattformer mit Sprungfedern, Schlüsseln, Türen, Speicherpunkten, Fahrzeugen, Schwert oder Wurf, Bossen. Von oben: Labyrinth, Sammelspiel, einfaches Action-Abenteuer (Schwert, verfolgende Gegner, Schlüssel und Türen, Gegenstände und sprechende Figuren). Sieg über Ziel, Punkte oder Regeln; Niederlage über Leben, Zeitlimit oder Regel. Dazu weiter Schlange, Mauerbrecher und Hüpfspiel als feste Arcade-Spiele.
- **Geht nicht:** freie Physik (Schrägen, Seile, Kisten schieben), Schießen in acht Richtungen, Rennspiele, Rundenkampf/RPG-Werte, Mehrspieler, eigene Skripte/Ereignisse jenseits der „Wenn … dann …“-Liste, Gegner mit Wegfindung durch Labyrinthe (der Verfolger geht gerade auf die Figur zu und bleibt an Wänden hängen), Fallen von oben (die Sprungfeder ist dort nur eine Wand). 3D gibt es nur über Unreal und ist hier nicht neu geprüft.
- **Godot-Prüfung:** Die Durchspiel-Prüfung spielt eigene, kleine Prüflevel mit der Spiel-Logik des Projekts. Sie spielt **nicht** das vom Nutzer gemalte Level bis zum Ziel. Das Nutzer-Level wird in Godot mit der Level- und der Spiel-Prüfung geprüft (Start, Ziel, Figur steht).
- **Unity:** Der Blick von oben fehlt dort; Trendo sagt das beim Export ehrlich. Unity ist auf diesem PC nicht installiert, nichts wurde neu geprüft.

## Abnahmekriterien
1. Aus leerer Vorlage ein echtes Ergebnis über die Oberfläche: **VOLLWERTIG.** Vorlage → Blick → malen → Bauen → im echten Chrome gewonnen (Test).
2. Fremddatei importieren/bearbeiten/exportieren, im Fremdprogramm geöffnet: **TEILWEISE.** Eigene Bilder (PNG …) werden ins Spiel übernommen und im Chrome geladen. Der Export öffnet sich in Chrome und in Godot 4.7.2 (Belege mit SHA-256). Einen Import fremder Spielprojekte (Tiled/Godot zurück) gibt es nicht.
3. Prozessabbruch verliert höchstens 30 s: **NICHT GETESTET** in diesem Paket (unverändert: Programm-Hülle mit Autosave).
4. Claude/KI führt eine Fachaufgabe aus, Ergebnis im offenen Dokument, rückgängig: **TEILWEISE.** „Spiel aus einem Satz“ legt ein spielbares, bearbeitbares Dokument an (ein Rückgängig-Schritt). Die KI ist im Test ersetzt (`GamePromptOverride`); ein Lauf mit echter KI oder echtem Claude ist nicht geprüft.
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG.** Klick-Weg → gebauter Spielordner → echter Chrome bzw. echtes Godot.
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **TEILWEISE.** Malen mit links (auch ziehen), Radieren mit rechts, Tasten im Probespiel. In „Einfach“ ist die Mal-Fläche erst nach „Alle Bereiche zeigen“ sichtbar (unverändert). Ziehen und Ablegen von Bildern auf die Fläche fehlt.

## Tests (gezielt, kein Volltest)
- Core: alle Klassen mit Game*/EngineTargets/PlatformSim/Unreal*/Mixamo/StudioRules/W4Game/PromptAction/WebsiteStudio: **341/341** grün (nach der letzten Änderung erneut 304/304 im engeren Filter). Neu: `GameFreeTests` 6/6, davon 1 mit echtem Godot.
- App: 30 Spiele-Testklassen, 193 Tests. Davon 3 rot:
  - 2 × `GameLibrary_Ui` (Konsolen-Studio, nicht mein Bereich): **rot schon auf dem unveränderten Integrationsstand**, nachgeprüft.
  - 1 × `GameAssets_Ui` E7: im Sammellauf rot, einzeln grün (Reihenfolge-abhängig, keine Berührung mit meinen Änderungen erkennbar).
  Neu: `GameFree_Ui` 5/5, davon 3 mit echtem Chrome bzw. Godot; dreimal hintereinander grün. `W4_Core06_Ui`, `T0203_Programme_Ui` grün.
- Rot-Grün: Die neuen Tests nutzen neue Schnittstellen (`View`, `TopDown`, `PlayAsync`, `GodotSelfPlayArgs` …). Auf dem alten Code lassen sie sich nicht bauen.

## Offene Punkte
- Wegfindung für verfolgende Gegner; Unity-Blick von oben; Godot-Durchspiel mit dem Nutzer-Level selbst.
- Unreal-Lauf mit Beleg in eigener Sitzung, wenn der Nutzer Unreal dafür freigibt.
- „Einfach“: eine Mal-Fläche direkt zeigen, ohne „Alle Bereiche“.
- `GameLibrary_Ui` ist auf dem Integrationszweig rot (Konsolen-Studio): Klick landet auf `prog-workspace`.

## Berührte Dateien
src/Trendo.Core: `GameStudio.cs`, `Programs/{GameDesign,GameDirector,GameMenus,GamePro,PlatformSim,EngineTargets}.cs`, `PromptAction/GamePromptAdapter.cs`
src/Trendo.App: `MainWindow.ProgGame.cs`, `MainWindow.ProgGameFree.cs` (neu), `MainWindow.ProgGameEngines.cs`, `MainWindow.ProgGamePro.cs`, `MainWindow.ProgGamePrompt.cs`, `MainWindow.ClaudeGame.cs`, `MainWindow.W4Game.cs`, `LevelCanvas.cs`, `PlaytestCanvas.cs`, `WebsitePreview.cs`
tools: `Trendo.Render/Program.cs` · tests: `Trendo.Core.Tests/GameFreeTests.cs`, `Trendo.App.Tests/GameFreeUiTests.cs` (neu) · `reports/T-0690/*`, `tasks/T-0690-spiele*.md`

Rust: nicht eingesetzt. Das Spiel ist JavaScript im Browser bzw. GDScript in Godot, das Studio C#. Rust brächte hier keinen messbaren Vorteil.
