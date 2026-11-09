# Pilot 4.0: Videoschnitt

> Entwurf vom 09.11.2026, aus dem Code geprüft. Pilot vom Nutzer freigegeben; die Umsetzung wartet auf die Ja/Nein-Liste unten. Programm `schnitt` im Studio CORE-08.

## Forderungen aus Abschnitt 4.3 gegen den Code

| Forderung | Stand | Beleg im Code |
|---|---|---|
| Mehrspur-Zeitleiste | teilweise: eine Bildspur, eine Überlagerungsspur, eine Musikspur | `VideoEdit.Clips(p, track)` mit `clips` und `music`, `VideoPro.AddOverlay` |
| Framegenauer Schnitt | teilweise: Pfeiltasten springen fest um 1/30 s | `MainWindow.VideoSchnitt.cs`, Tasten Links und Rechts |
| Übergänge | vorhanden | `VideoEdit` mit `xfade` |
| Keyframe-Animation | fehlt für Stücke, nur im Abspann | `Credits.Keyframe` |
| Effekte | teilweise: je Stück feste Farb- und Tonregler, kein Effektstapel | `VideoPro.Fx` |
| Audiobearbeitung | teilweise: Pegel je Stück, Ducking; kein Mischpult | `VideoExportTests`, `VideoSchnittPro` |
| Farbkorrektur | teilweise: Looks, vier Regler, LUT-Datei; keine Farbräder, keine Messinstrumente | `MainWindow.VideoSchnittPro.cs` |
| Titelgenerator | vorhanden, Abspann mit Keyframes | `Credits`, `VideoPro` |
| Vorschau | **schwach: läuft mit 6 Bildern pro Sekunde** | `const int PlayFps = 6` |
| Proxy-Medien | vorhanden | `VideoProxy` |
| Renderwarteschlange | vorhanden | `MainWindow.VideoSchnitt.cs`, S3-18 |
| Exportprofile | vorhanden, auch NVENC, EDL, FCPXML, MLT | `VideoExport`, `MltProject` |

Außerdem fehlen im Videoschnitt: Einrasten, Ripple-, Slip- und Rolling-Werkzeuge, Rechtsklick-Menüs, ziehbare Bereiche und Andock-Panels.

Die Vorbild-Analyse `docs/claude/vorbilder/video-kdenlive-resolve.md` ist veraltet: Sie nennt als nächsten Schritt noch Übergänge und Titel, die längst gebaut sind.

## Funktionssicherung

- **Direkte Tests heute:** 27 in `VideoSchnitt_Ui`, `VideoSchnittPlus_Ui`, `VideoSchnittPro_Ui`, `VideoEditTests`, `VideoEditPlusTests`, `VideoExportTests`, `VideoSubtitleTests`, `VideoProTests`, `BeatDetectTests`.
- **Datenformat:** `.tdoc` mit den Schlüsseln `clips`, `music` und Überlagerungen. Mehrspur darf alte Dateien nicht umschreiben, bevor sie gespeichert werden; alte Projekte müssen unverändert öffnen.
- **Vor dem ersten Eingriff:** Funktionsliste nur für `schnitt` anlegen, denn der Katalog mischt heute Audio und Bild hinein. Eine alte Projektdatei als Testvorlage ablegen und Exporte mit festen Merkmalen sichern.

## Vorschläge zum Entscheiden

| Nr | Vorschlag |
|---|---|
| V1 | Flüssige Vorschau statt 6 Bildern pro Sekunde |
| V2 | Beliebig viele Bild- und Tonspuren; alte Projekte öffnen unverändert |
| V3 | Framegenau: Schritt und Raster nach Bildrate des Projekts, Zeitcode-Anzeige |
| V4 | Schnitt-Werkzeuge: Einrasten, Ripple, Slip, Rolling |
| V5 | Keyframes für Lage, Größe, Deckkraft und Tonpegel |
| V6 | Effektstapel je Stück: Reihenfolge, an und aus, Vorlagen |
| V7 | Farbe: Farbräder, Waveform und Vektorskop |
| V8 | Ton: Mischpult mit Pegeln, Stumm und Solo je Spur |
| V9 | Rechtsklick-Menüs und ziehbare Bereiche |
| V10 | Abnahme nach 4.0 mit mindestens 20 eigenen Testfällen |
