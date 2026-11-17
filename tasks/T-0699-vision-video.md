# T-0699: D6b – „Bilder erkennen“ erkennt wirklich, „Videos mit KI“ erzeugt wirklich Video

**Ziel:** Die beiden Studios aus Audit-Teilbericht 4 (STUDIO-VISION-01, STUDIO-VIDEOAI-01) liefern, was ihr Name verspricht – echte Bilderkennung mit lokalen Werkzeugen und echtes Bewegtbild mit Ton.
**Branch:** claude/T-0699-vision-video (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** Programme `bilder-erkennen-programm` und `video-ki-programm` (MainWindow.KreativStudios3/4 – nur deren Abschnitte – plus neue Teildateien), `VisionKit`/neue `VisionTags`, `VideoAiKit`, Tests, `reports/T-0699/`. Nicht: `MainWindow.ProgramShell.cs`, Film-Studio, `SimpleWords.cs`.
**Fertig, wenn:**
- [x] Bilder erkennen: Objekte/Personen mit Rahmen (YOLOX-S), Bildbeschreibung mit Schlagworten (lokales Bildverstehens-Modell oder – mit Erlaubnis – Claude über das Abo), Text im Bild (Tesseract), Stapel über einen Ordner, Suche „zeig alle Fotos mit Hund“, Schlagworte als Seitendatei (XMP, Foto unverändert), CSV; EXIF/Schärfe/Belichtung bleiben.
- [x] Videos mit KI: Schlüsselbilder mit der lokalen Bild-KI, echte Bewegung (Kamerafahrt/Zoom, Überblendung, je Szene optional echter KI-Clip mit Wan 2.2 aus dem Schlüsselbild), Sprecher (Piper), Musik, Untertitel, MP4; ehrliche Benennung „Bildfolge mit Kamerafahrt“ vs. „KI-Video“.
- [x] Belege mit echten Werkzeugen unter `reports/T-0699/` (Tesseract, YOLOX, Claude-Beschreibung, Bild-KI, Piper, FFmpeg/ffprobe, Wan 2.2).
- [x] Oberfläche: Galerie mit Erkennung bzw. Szenenliste mit Vorschau; eine goldene Hauptaktion, erster Schritt im Leerzustand; im Bild geprüft.
- [ ] Volltest (macht die Hauptsitzung am Schluss).
**Nicht tun:** nichts herunterladen ohne Zustimmung (fehlende Modelle nur über den vorhandenen Einrichten-Weg anbieten), kein Push nach main/Integrationszweig, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0699-vision-video.bericht.md`.
