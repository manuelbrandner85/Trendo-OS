# T-0695 – Bericht: Videoschnitt, Vorschau zeigt, was exportiert wird

Zweig `claude/T-0695-video-vorschau` (von `claude/T-0685-b1-integration`, Stand `1cba1f74d`). Beleg: `reports/T-0695/README.md`.

## Was war → was ist jetzt

**1. Vorschau = Export (Pipeline)**
- **War:** Die Vorschau holte das rohe Bild aus der Quelldatei (`MainWindow.VideoSchnitt.cs` `EditFrame`, `MainWindow.VideoPreview.cs` ~69 `new PreviewStream(..., c.File, ...)`); der Titel war ein Avalonia-Text darüber in fester Lage. Farbe, Looks, Farbräder, Effektstapel, Keyframes, Übergänge, Einblendungen sah man erst im Export.
- **Jetzt:** Der Bildteil des Export-Graphen ist in `VideoEdit.PictureGraph` herausgelöst; Export (`ExportArgs`) und Vorschau (`Core/Media/VideoLook.cs`, neu) bauen ihn gemeinsam. Für die Vorschau wird er auf einen **Ausschnitt** begrenzt: nur die Stücke an der Stelle, das erste beginnt später, behält aber seine Zeit (`setpts`) – Keyframes, Deckkraft, Bewegung, Übergangsfortschritt rechnen wie im Export. Einblendungen (zweite Bildspur, Bild im Bild, Logo) springen ebenso an die Stelle (`OverlayGraph(from, to)`), eingebrannte Untertitel kommen aus denselben Export-Einstellungen. Der Export-Aufruf selbst ist **byte-gleich** geblieben (Goldtest `VideoBaselineTests.Export_args_unchanged` grün), der T-0693-Fix (harter Schnitt + Überblendung) ist unverändert drin.
- **Standbild am Abspielkopf:** `VideoLook.RenderFrameAsync` – im Hintergrund, abbrechbar (beendet nur den eigenen FFmpeg-Prozess, keine halbe Datei), Zwischenspeicher je Stelle (`<Medien>/.vorschau/bild/<schlüssel>.png`, Schlüssel = Projekt ohne Abspielkopf + Stand der Quelldateien + Bild + Größe), 960 px breit (gerechnet in Projektgröße, verkleinert am Ende), Proxy-Dateien werden für die Vorschau genutzt. Beim Ziehen bricht jede neue Stelle die vorige ab; bis das Bild da ist, bleibt das letzte fertige Bild stehen mit „Vorschau wird berechnet …“, bei Fehler „Vorschau nicht möglich: …“.
- **Abspielen:** ein FFmpeg-Strom aus demselben Graphen ab der Startstelle bis zum Ende (`VideoLook.StreamArgs` → `PreviewStream` mit neuem Konstruktor), 640×360; kommen die Bilder nicht nach, steht ehrlich „Vorschau wird berechnet …“ da. Ton wie bisher als fertige Mischung über mpv.
- **Zwei echte Fehlerquellen gefunden und behoben (nur Vorschau):** `trim` rundet den Start auf die Zeitbasis der Verbindung (1/Bildrate) – mit einem halben Bild Abstand landete die Vorschau in Überblendungen ein Bild zu früh; jetzt Viertelbild. In Überblendungen werden die Bilder zwischen Blendenbeginn und Stelle früh verworfen (`select` nach `fps`, vor Farbe/Effekten, auch im Hintergrund der Keyframe-Bewegung) – gleiches Bild, halbe Zeit.

**2. Nachweis Gleichheit (echtes FFmpeg 9.0.2)** – `VideoLookTests.Preview_equals_export_real`, 1080p, Schwarzweiß-Look, Titel, Übergang „Schieben“, Keyframes (Größe, Lage), Vignette, Logo mit bewegter Lage, harter Schnitt, Standbild; 5 Stellen. Mittlere Abweichung Vorschau↔MP4: 0,28 / 0,80 / 1,26 / 0,82 / 0,20 von 255 (1,0 / 3,5 / 5,2 / 6,6 / 8,0 s), Schwelle 2,5 (begründet mit Codec-Verlust und Gegenproben Nachbarbild 2,3–25,8 und rohes Stück 7,8–111,6). Bilder nebeneinander, Rohlog, Projekt, SHA-256 unter `reports/T-0695/`.

**3. Bedienung**
- Abspielkopf ziehen/klicken → fertiges Bild nach dem Loslassen (Zwischenstellen werden abgebrochen). Leertaste, J/K/L, ←/→ bildweise (Umschalt: Sekunde), I/O, M, Pos1/Ende wie bisher.
- **Neu: Vollbild-Vorschau** – Taste F auf der Zeitleiste oder Knopf „⛶ Vollbild“ oben rechts in der Vorschau; im Vollbild: F/Esc zurück, Leertaste, ←/→. Das Vollbild folgt Standbild und Wiedergabe.
- **Neu: Dateien aus dem Explorer auf die Zeitleiste ziehen** – oben (Bildspur) vor das Stück unter der Abwurfstelle, unten (Musikspur) ab der Stelle; Länge/Ton per ffprobe; ein Schritt für Rückgängig (`EditDropFiles`).
- Rechtsklick auf Stücke: unverändert vorhanden (V9).
- **Gold-Regel (T-0686):** golden war „Teilen (S)“ (erster `Primary` im Menüband). Jetzt ist „Teilen“ normal und **„Video exportieren“ die eine goldene Hauptaktion** (im Bild und im Test geprüft). Hinweistexte „Sichtbar wird … im Export“ (Keyframes, Effekte, Farbräder) korrigiert.

**4. Im Bild nachgeprüft** – Render-Werkzeug mit eigenem `TRENDO_RENDER_ROOT`, neuer Schalter `TRENDO_SCHNITT=look` (wartet auf das fertige Bild mit echtem FFmpeg): `reports/T-0695/oberflaeche-schnitt-vorschau.png`. Erster Entwurf hatte den Vollbild-Knopf in der Abspielleiste – in „Einfach“ (Knöpfe mit Wort) schob er die Zeitanzeige über „Zoom“; deshalb sitzt er jetzt oben rechts in der Vorschau.

## Messwerte
- Standbild, 1080p-Projekt, 960 px, kalter Zwischenspeicher (Median/Höchstwert aus 5 Läufen): 173/190 ms (Look+Titel), 240/257 ms (Keyframes+Vignette+Logo), 239/246 ms, 164/166 ms (Standbild+Logo), **416/503 ms in einer 1-s-Überblendung**. In der Oberfläche gemessen (Wunsch bis Bild): 254–304 ms. Zwischenspeicher-Treffer 0–1 ms.
- Wiedergabe mit Effekten 640×360 aus 1080p: 63–81 Bilder/s (Ziel 30), erstes Bild nach 0,26–0,66 s.
- Export des Testprojekts (10 s, 1080p): 4,1–4,9 s.

## Tests
- Kern (gezielt, Release): `VideoLookTests|VideoBaseline|VideoEdit|VideoPro|VideoAnim|VideoExport|PreviewStream|FilmCut|ProExport|VideoLanes|VideoGrade|VideoProxy|VideoTools` → **45/45 bestanden**.
- App (gezielt): 29 Klassen, die Videoschnitt/„schnitt“ oder die Gold-Regel berühren (inkl. `VideoPreview_Ui`, `VideoSchnitt*_Ui`, `ProgramHeadDesign_Ui`, `OsStyles_Ui`, `ProfiRahmen_Ui`, `FilmSchnitt_Ui`, neu `VideoLook_Ui`) → siehe unten „Testlauf“.
- Neu, echt (kein `*Override`): `VideoLookTests.Preview_equals_export_real`, `VideoLook_Ui.Look_click_to_file_real` (Ablegen → Look/Titel → Vorschau → Abspielen → Vollbild → Klick „Video exportieren“ → MP4 per ffprobe → Vorschau = MP4-Bild). Neu, ohne FFmpeg: `VideoLookTests.Window_args`, `VideoLook_Ui.Drop_and_gold`.
- Rot-Grün: die neuen Tests kompilieren auf dem alten Stand nicht (es gibt weder `VideoLook`, `EditDropFiles`, `EditToggleFullscreen` noch einen goldenen Export-Knopf); `Window_args` prüft genau die Ausschnitt-Umrechnung.
- Kein Volltest (macht die Hauptsitzung).

## Abnahmekriterien
1. Aus leerer Vorlage ein echtes Ergebnis über die Oberfläche: **VOLLWERTIG** – Vorlage Full HD, Dateien ablegen, Look/Titel, „Video exportieren“ → MP4 1920×1080 mit Ton (UI-Test, echtes FFmpeg). Look und Titel im Test über das Datenmodell gesetzt (dieselben Aufrufe wie die Knöpfe), nicht per Klick.
2. Fremddatei importieren/bearbeiten/exportieren, öffnet im Fremdprogramm: **TEILWEISE** – MP4/PNG rein, MP4 raus, ffprobe liest es; in einem Fremdprogramm (VLC o. ä.) nicht geöffnet.
3. Prozessabbruch ≤ 30 s Verlust: **NICHT GETESTET in diesem Paket** – unverändert die Programm-Hülle (Autosave/Wiederherstellen); der Vorschau-Zwischenspeicher ist nur Cache.
4. Claude/KI-Fachaufgabe: **FEHLT in diesem Paket** – nicht Teil von D3.
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** – Klick auf „Video exportieren“ mit echtem FFmpeg bis zur geprüften MP4.
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen und Ablegen: **TEILWEISE** – F, Leertaste, J/K/L, Pfeile, Rechtsklick vorhanden; Ablegen auf die Zeitleiste gebaut und über `EditDropFiles` getestet, das echte Ziehen aus dem Explorer (DragDrop-Ereignis) **nicht getestet** (im Testmodus ohne Fenster nicht auslösbar).

## Nicht geprüft / ehrlich
- In **Überblendungen** dauert das Standbild 0,4–0,5 s (Ziel 300 ms verfehlt). Ursache: `xfade` braucht das erste Bild der Blende. Weiterer Gewinn nur mit eigenem Übergangsfilter.
- **Scrubben in einem echten Fenster** nicht gemessen; jeder Abspielkopf-Schritt baut wie bisher die ganze Arbeitsfläche neu (Testmodus: 76–358 ms je Schritt) – das ist älter als dieses Paket, bremst aber das Ziehen.
- **J/L mit 2×/4×** zeigen weiter die rohen Einzelbilder (alter Weg); nur Tempo 1 zeigt das fertige Bild.
- Effekt **„Körnung“** ist Zufall je Bild – Vorschau und Export zeigen nicht dasselbe Rauschmuster. Quellen mit **anderer Bildrate als das Projekt** und Stücklängen, die nicht auf ganze Bilder fallen, können an Schnittkanten um ein Bild abweichen (nicht gemessen; die Oberfläche rastet Schnitte auf Bilder ein).
- 4K-Projekte nicht gemessen; der Graph rechnet in Projektgröße.
- Gezielter App-Lauf: einmal 1 von 140 rot (`FilmSchnitt_Ui.Render_click_to_file_real`, echter Film-Export unter Last), allein und zusammen mit den anderen FFmpeg-Tests zweimal grün – Ergebnis des Wiederholungslaufs unten.

## Berührte Dateien
`src/Trendo.Core/Media/VideoLook.cs` (neu), `src/Trendo.Core/Media/VideoEdit.cs`, `src/Trendo.Core/Media/VideoPro.cs`, `src/Trendo.Core/Media/VideoAnim.cs`, `src/Trendo.Core/Media/PreviewStream.cs`,
`src/Trendo.App/MainWindow.VideoPreview.cs`, `src/Trendo.App/MainWindow.VideoSchnitt.cs`, `src/Trendo.App/MainWindow.VideoKeys.cs` (nur Hinweistext), `src/Trendo.App/MainWindow.VideoMixer.cs` (nur Hinweistext),
`tools/Trendo.Render/Program.cs` (Schalter `TRENDO_SCHNITT=look`), `tests/Trendo.Core.Tests/VideoLookTests.cs` (neu), `tests/Trendo.App.Tests/VideoLookUiTests.cs` (neu), `reports/T-0695/*`, `tasks/T-0695-*`.
`EditTimeline.cs` blieb unverändert (Ablegen hängt an der vorhandenen Zeitachse `T(x)`).

## Testlauf
- Kern gezielt: 45/45 bestanden.
- App gezielt (29 Klassen, 140 Tests): erster Lauf 139/140 (FilmSchnitt_Ui.Render_click_to_file_real rot unter Last), Wiederholung allein 5/5 und mit den anderen FFmpeg-Klassen 9/9 grün, zweiter Gesamtlauf **140/140 bestanden** (208 s).
- Build `dotnet build Trendo.sln -c Release`: 0 Fehler.
