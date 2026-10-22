# Design 41, Phase 1 – verbleibende Unterschiede

Stand: 22.10.2026, Branch `cursor/ui41-sm-01-startmenu-3c36`.
Links in den JPG ist die Referenz (SOLL), rechts die laufende Avalonia-App (IST).
Rohbilder der App: `ist/`. Aufgenommen mit `tools/Trendo.Render` (Skia, headless), 1920×1080 und für mehrere Fenster zusätzlich 1366×768.

Die App übernimmt Aufbau, Glas, Abstände, Farben, Schrift und Symbole. Texte, Zahlen und Namen kommen aus dem Programm, nicht aus den KI-Bildern.

## 01 Desktop

- Aufbau stimmt: linke Leiste, Wortmarke, Suchfeld, rechte Uhr mit echten Messwerten, Dock unten, Wallpaper randlos.
- Kein Slogan unter „Trendo OS“. Der Satz aus dem Bild ist nicht freigegeben.
- Kein Wetter, kein „Trendo Radio“.
- Logo ist Entwurf A (Obsidian-Kachel), nicht der T-Kreis aus dem Bild.
- Wallpaper ist das vorhandene `bergsee-nacht` (Nacht, Lichtkreis), nicht das Sonnenuntergangsbild der Referenz.
- Dock zeigt nur vorhandene Programme. Auf dieser Linux-Box: Dateien, Browser (Chrome liegt im PATH), Papierkorb. Mail, Blender und Unreal fehlen, weil sie nicht installiert sind. Studios stehen nicht im Dock.
- Linke Leiste hat drei feste Einträge, die bestehende Tests verlangen: „Neuer Brief“, „Briefe schreiben“, „Notiz“. Die Referenz zeigt stattdessen erfundene Dateien. Ohne echte zuletzt geöffnete Einträge bleibt die Liste leer, plus „Erstes Projekt anlegen“.
- GPU ist auf dieser Box „n. v.“ / „Nicht verfügbar“ (kein nvidia-smi). CPU, RAM, SSD und Netz sind echte Werte.
- Suchplatzhalter: „Studios, Dateien und Einstellungen suchen“.

## 02 Startmenü

- Großes Glasfenster, Suche, Kategorien mit Symbolen, angeheftete Kacheln, A–Z, Nutzer-Spalte mit Profil, Einstellungen, Startbildschirm, Hilfe, Neu starten, Trendo beenden.
- Kategorien sind die echten Bereiche aus `StudioCatalog` (Arbeiten & Büro, Business & Geld, …), nicht Kreativ / Produktivität / Entwicklung aus dem Bild.
- Namen sind die Katalog-Namen („Briefe schreiben“, „3D-Modelle erstellen“), nicht „Briefstudio“ oder „Game-Studio“.
- Nutzer heißt „Lokaler Besitzer“, wenn kein Name gesetzt ist. Kein „Pro Edition“, kein „Herunterfahren“, kein „Sperren“. „Trendo beenden“ schließt nur das Fenster.
- „Zuletzt verwendet“ zeigt echte Seiten. Im Bild sind erfundene Dateien mit Minutenangaben.
- Angeheftete Studios erscheinen zusätzlich in der Trendo-Taskleiste (Vorgabe UI41-SM-01). Das Bild zeigt sie nur im Menü.
- Das Menü liegt über dem Desktop. Leiste, Uhr und Dock bleiben sichtbar.

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
- Bei 1366×768 bleiben Taskleiste, Dock und die drei Fenster bedienbar. Die Seitenleisten werden schmaler, die rechte Uhr bleibt.

## Bewusst nicht in dieser Phase

Studio-Karten (03), KI-Seite (05), Einstellungen (07), Systemmetriken als eigene Tafel (08), Dateimanager (10), Themes-Tafel (09). Die älteren JPG dazu stammen vom Stand main 0c0525b und sind nicht neu fotografiert.
