# Fehler „Klick auf Studio-Kachel tut nichts“ – Nachweis auf dem PC (0.9.4 → 0.9.5)

Geprüft am installierten Programm mit **echter Maus** (`klick-test.ps1`: UI Automation findet die Stelle, `SetCursorPos` + `mouse_event` klicken, danach Bildschirmfoto und neue audit.jsonl-Zeilen).

**Vorher (0.9.4, `vorher-0.9.4.txt`):** In „Alle Studios“ und in jedem Seitenleisten-Bereich gab es **0 klickbare Studio-Karten**. Die Karten waren reine Anzeige. Ein Klick erzeugte weder einen Protokolleintrag noch einen Seitenwechsel. Die 9 Kacheln der Startseite funktionierten.

**Ursache:** Die Karten in `StudiosView` waren seit P1 `Border` ohne Klick-Handler. Dazu kam: Studios ohne eigene Seite sprangen in `OpenStudio` auf genau diese Liste zurück. Mit ReadyToRun, den verzögerten Prüfungen oder der Fehlerseite hatte das nichts zu tun: Im Protokoll gab es dazu keine Ausnahme und keinen `page.error`.

**Nachher (0.9.5+8b9609a):**
- Startseite 9/9 Kacheln OK (`nachher-0.9.5.txt`)
- Bereiche Online Business 1/1, 3D & Games 2/2, Kreativ & Medien 2/2, Tools & Entwickler 3/3, System & Sicherheit 3/3, KI Assistent 3/3, Kommunikation 1/1
- „Alle Studios“ **54/54** Karten öffnen ihr Studio (`alle-0.9.5.txt` 50 OK; 4 Karten lagen hinter dem Dock-Bereich und wurden im `alle-0.9.5-nachlauf.txt` nach dem Scrollen geklickt: 4/4 OK)
- Bildschirmfotos (Taskleiste abgeschnitten): `bilder/` (24 Stück)
