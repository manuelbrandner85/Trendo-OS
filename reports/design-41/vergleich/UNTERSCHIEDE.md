# Design 41, Phase 1 – verbleibende Unterschiede

Stand: 22.10.2026, Branch `cursor/ui41-sm-01-startmenu-3c36`.
Links in den JPG ist die Referenz (SOLL), rechts die laufende Avalonia-App (IST).
Rohbilder der App: `ist/`. Aufgenommen mit `tools/Trendo.Render` (Skia, headless), 1920×1080 und für mehrere Fenster zusätzlich 1366×768.

Die App übernimmt Aufbau, Glas, Abstände, Farben, Schrift und Symbole. Texte, Zahlen und Namen kommen aus dem Programm, nicht aus den KI-Bildern.

## 01 Desktop

- Die linke App- und Studio-Liste ist weg. Das ist der neue Wunsch des Besitzers: die Tapete bleibt frei, Studios kommen aus dem Startmenü, der Suche oder der Taskleiste. Die Referenz zeigt links noch Start, Studios, KI, Dateien, Einstellungen und erfundene Dateien.
- Mitte: Logo A, „Trendo OS“, ein Suchfeld. Kein Slogan. Der Satz aus dem Bild ist nicht freigegeben.
- Rechts schweben wenige Glaskarten: Wochentag, Datum, Uhr, echte CPU/RAM/GPU/SSD und Netz. Darunter „Zuletzt“ nur mit echten Projekten. Ohne Projekt steht dort „Erstes Projekt anlegen“, keine erfundenen Dateien.
- Angeheftete Studios erscheinen als eigene Karte nur, wenn der Besitzer sie im Startmenü selbst anheftet. Ohne Auswahl bleibt die Fläche leer.
- Kein Wetter auf dem Schreibtisch und kein „Trendo Radio“. Wetter gibt es in „Zeit & Kalender“, erst nach Ort und Klick, ohne erfundene Temperatur. Ein frischer Schreibtisch hat keinen gespeicherten Ort.
- Logo ist Entwurf A (Obsidian-Kachel), nicht der T-Kreis aus dem Bild.
- Wallpaper ist das vorhandene `bergsee-nacht` (Nacht, Lichtkreis), nicht das Sonnenuntergangsbild der Referenz.
- Dock zeigt nur vorhandene Programme. Auf dieser Linux-Box: Dateien, Browser (Chrome liegt im PATH), Papierkorb. Mail, Blender und Unreal fehlen, weil sie nicht installiert sind. Studios stehen nicht im Dock.
- GPU ist auf dieser Box „n. v.“ / „Nicht verfügbar“ (kein nvidia-smi). CPU, RAM, SSD und Netz sind echte Werte.
- Suchplatzhalter: „Studios, Dateien und Einstellungen suchen“. Strg+1 bis Strg+5 öffnen weiterhin Start, Studios, Projekte, Freigaben und System.

## 02 Startmenü

- Großes Glasfenster, Suche, Kategorien mit Symbolen, angeheftete Kacheln, A–Z, Nutzer-Spalte mit Profil, Einstellungen, Startbildschirm, Hilfe, Neu starten, Trendo beenden.
- Kategorien sind die echten Bereiche aus `StudioCatalog` (Arbeiten & Büro, Business & Geld, …), nicht Kreativ / Produktivität / Entwicklung aus dem Bild.
- Namen sind die Katalog-Namen („Briefe schreiben“, „3D-Modelle erstellen“), nicht „Briefstudio“ oder „Game-Studio“.
- Nutzer heißt „Lokaler Besitzer“, wenn kein Name gesetzt ist. Kein „Pro Edition“, kein „Herunterfahren“, kein „Sperren“. „Trendo beenden“ schließt nur das Fenster.
- „Zuletzt verwendet“ zeigt echte Seiten. Im Bild sind erfundene Dateien mit Minutenangaben.
- Angeheftete Studios erscheinen zusätzlich in der Trendo-Taskleiste (Vorgabe UI41-SM-01). Das Bild zeigt sie nur im Menü.
- Das Menü liegt über dem Desktop. Die linke Liste fehlt auch hier. Rechts bleiben Uhr und, in dieser Aufnahme, die Karte „Angeheftet“, weil fünf Studios für das Bild angeheftet sind. Dieselben fünf stehen in der Trendo-Taskleiste. Dock und Uhr bleiben sichtbar.

## 04 Briefe schreiben

- Drei Spalten: Navigation, Formular, weiße DIN-Vorschau. Oben Speichern, Exportieren, Als PDF, Senden.
- Titel ist „Briefe schreiben“, nicht „Briefstudio“. Untertitel: „Briefe, Vorlagen und Akten“.
- Das Studio liegt in einem Trendo-Fenster (Titelleiste, Minimieren, Maximieren, Schließen) über dem Desktop. Die Referenz ist eine Vollfläche ohne Fensterrahmen. Das Dock bleibt sichtbar.
- Keine B/I/U-Leiste. Der Brief ist Klartext nach DIN 5008.
- Absenderfelder stehen im Formular, damit die Daten echt und änderbar sind. In der Vorschau stehen sie rechts oben, mit den echten Feldern.
- Vorlage ist die echte Kategorien-Liste („Freier Brief“ und die übrigen Vorlagen), nicht „Geschäftsbrief (modern)“.
- „Vorlagen“ ist die Auswahlliste im Formular, kein eigener Navigationspunkt. Navigation: Neuer Brief, Meine Briefe, Briefköpfe, Empfänger. „Prüfen und Ausgabe“ erscheint, sobald eine Akte offen ist.
- Senden verschickt nichts. Es verweist auf die Freigabe.
- „Als PDF“ ist nur aktiv, wenn LibreOffice vorhanden ist.
- Vorschau-Logo ist Entwurf A plus „Trendo OS“, nicht „Trendo Solutions“ und nicht die Adresse aus dem Bild.
- Beim ersten Öffnen erscheint der Hilfsstreifen („Verstanden“). Die Vergleichsaufnahme hat ihn geschlossen, damit das Studio selbst sichtbar ist.

## 06 Fenster und mehrere Studios

- Gestapelte Fenster, Schatten, aktive Titelleiste, roter Schließen-Knopf, eigene Taskleisten-Einträge.
- Inhalt der Referenz (Datei / Bearbeiten / Ansicht, Ordner-Symbole, erfundene Dateien) ist der Dateimanager und gehört nicht in diese Phase.
- Die Aufnahme zeigt drei echte Studios gleichzeitig: Briefe schreiben, Spiele bauen, 3D-Modelle erstellen. Blender und Unreal werden nicht gestartet.
- Inaktive Fenster sind leicht durchscheinend. Das aktive Fenster ist deckender.
- Bei 1366×768 bleiben Taskleiste, Dock und die drei Fenster bedienbar. Die linke Liste fehlt. Die Uhr bleibt rechts oben, solange das Fenster sie nicht verdeckt.

## Bewusst nicht in dieser Phase

Die folgenden Punkte galten bis zur Freigabe. Der Besitzer hat JA gesagt. Die Übertragung steht im nächsten Abschnitt. Die älteren JPG `03`, `05`, `07` und `08` direkt in diesem Ordner stammen vom Stand main `0c0525b` und sind nicht die neuen Aufnahmen.

## UI41-7 – Übertragung auf die übrigen Studios

Stand: 22.10.2026, nach dem JA. Neue Bilder liegen in `gruppen/`. Links ist die Referenz, rechts die App. Rohbilder: `gruppen/ist/`. Je Seite 1920×1080 und 1366×768. Seiten ohne eigene Referenz sind nur IST.

Die Fachseiten sind nicht neu geschrieben. Dieselbe Glashülle (Titel mit Katalog-Symbol und Satz, Glas-Reiter, Glas-Knöpfe, eigenes Fenster, Taskleiste, Startmenü) liegt über den bestehenden Arbeitsflächen. Jeder Knopf macht dasselbe wie vorher.

### 03 Alle Studios

- Große Glaskarten, Bereichs-Chips aus `StudioCatalog` (nicht Kreativität/Produktivität aus dem Bild), Suche „Studio suchen“.
- Namen und Sätze kommen aus dem Katalog. Kennungen stehen nur im Tooltip.
- Klick öffnet das Studio. „Alle“ zeigt wieder jede Karte (54).
- Keine Fotos auf den Karten. Das Symbol ist das vorhandene Studio-Symbol, keine Bildkarte aus der Referenz.

### 05 KI

- Drei Spalten: Bereiche, Gespräch, Modell und Status. Eingabe unten, fest am Rand.
- Status in dieser Aufnahme: „Nicht verbunden“. Kein „GPT-4o“, keine Bild-generieren-Karten.
- Die Modelle sind die echten lokalen: Qwen2.5 0.5B und Qwen2.5 3B.

### 07 Einstellungen und 09 Hintergründe

- Linke Leiste schaltet Aussehen, Farbe, Hintergrund, Bedienung, Startbildschirm, Weiteres. Der gewählte Abschnitt ist der einzige sichtbare.
- Themen bleiben Dunkel Premium, Hell, Hoher Kontrast. Keine Namen Obsidian, Aurora, Sunset.
- Hintergründe: Bergsee, Polarlicht, Wellen, Morgen. Das ist `gruppen/settings-hintergrund-*.jpg` gegen Referenz 09.
- „Dunkel Premium“ liegt beim ersten Öffnen im sichtbaren Bereich.

### 08 Leistung

- Drei Karten: Prozessor, Arbeitsspeicher, Grafik. Werte sind die Messung dieser Box. Grafik ist „n. v.“, weil kein nvidia-smi da ist.
- Darunter die echten Prozesse. Trendo beendet sie nicht. Keine zweite `metric-*`-Anzeige; die Schreibtisch-Karten behalten diese Namen.

### 10 Dateien

- Die Seite ist „Kleine Helfer & Musik“, Reiter Dateien: umbenennen, Doppelte, Speicherplatz, ZIP, Fotos, Papierkorb. Jeder Knopf tut das, was er schon tat.
- Das ist kein nachgebauter Explorer mit erfundenen Ordnern. Die Referenz 10 zeigt einen Dateimanager mit Beispieldateien. Den gibt es hier nicht.

### Übrige Bereiche (nur IST, keine eigene Referenz)

- Arbeiten & Büro: `office` (Texte & Tabellen).
- Business & Geld: `business` (Online verkaufen).
- Kreativ & Medien: `kreativ-medien`. 3D und Spiele extra, weil die Arbeitsfläche anders ist: `3d`, `spiele`.
- Technik & Fachgebiete: `architektur` (Wohnung planen). Die Fach-Reiter tragen die Katalog-Namen und umbrechen bei 1366×768.
- Wissen & Lernen: `lernen`.
- KI & Automatik, die Frageseite: `ki-fragen`.
- Entwickeln: `dev` (Webseite bauen).

Solange ein Fenster offen ist, blendet der Schreibtisch die Widgets aus, damit Uhr und Messwerte nicht durch das Glas scheinen. Zurück auf den Startbildschirm sind sie wieder da.

## Offen nach der Übertragung

- DPI 125 % und 150 %, der Windows-PC, QA-017 am Gerät, QA-019 und die Codesignatur sind nicht geprüft. `WinRealTests` (10) sind auf Linux übersprungen.
- `SYS-03-P14` (Seite als PDF, A4) schlägt auf diesem Image fehl, sobald `google-chrome` im PATH liegt. Die App ist dafür unverändert.
- Keine Fotokarten, keine Themennamen aus den Bildern (Obsidian, Aurora, Sunset), kein „GPT-4o“. Die Tapete bleibt `bergsee-nacht`.
- Der Schriftzug „Trendo OS“ kann durch das halbtransparente Glas noch schwach durchscheinen. Kurze Seiten lassen darunter leeres Glas, weil der Inhalt in einem Rollbereich liegt.
- Bei 1366×768 umbrechen die vielen Fach- und Kreativ-Reiter. Sie bleiben klickbar.
- „Briefe schreiben“ aus dem Startmenü fragt zuerst nach der Texterkennung, wenn sie fehlt. „Später“ öffnet den Brief. Die Suche nach „Brief“ öffnet die Seite direkt.
- Wetter steht nur in „Zeit & Kalender“, erst nach Ort und Klick. SPEC-07 ist nicht gebaut. Das Logo bleibt Entwurf A.
