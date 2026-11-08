# Echter Lauf: MLT – Trendo-Schnitt als MLT-Projekt, gerendert mit melt (Videos schneiden) – 08.11.2026

Freigabe des Nutzers: „Ja, alle testen“. Für Windows gibt es bei MLT 7.42 kein offizielles Paket. **melt 7.43.0 liegt aber im mitgelieferten Kdenlive** (`TrendoWerkzeuge\kdenlive\bin\melt.exe`, MLT LGPL-2.1, eigenes Programm). Nichts neu geladen.

## Umsetzung (nur Kern, `src/Trendo.Core/Media/MltProject.cs`)
- Trendo-Schnitt → MLT-XML: Profil (Größe, Bildrate), Bildspur (Schnittpunkte in Bildern, Bilder mit fester Länge, Clips ohne Ton ohne Tonkanal), Musikspur (Lücke bis zum Start, Lautstärke als Filter, nur Ton), Mischung der Spuren.
- Nicht übernommen und benannt (`Limits`): Überblendungen und eingeblendete Texte – die rendert Trendo weiter selbst mit FFmpeg.

## Befund beim echten Lauf
- `melt <projekt> -consumer avformat:…` blieb am Ende stehen und wartete auf Tasten. Ein Minimalprojekt hing nach 40 s noch.
- Mit `terminate_on_pause=1` endet melt sauber (Exit 0). Mit `real_time=-1` werden keine Bilder ausgelassen. Beides ist in `RenderArgs` übernommen.

## Lauf (Test `MltProjectTests.Real_render`, nur mit `TRENDO_MELT_EXE` und `TRENDO_FFMPEG_DIR`)
- Testclips mit FFmpeg (Trendo-Werkzeuge):
  - Testbild mit Ton, 0,5–2,0 s
  - blaues Standbild, 1 s
  - roter Clip ohne Ton, 3 s
  - Musik ab 1 s mit Lautstärke 0,5
- melt rendert. ffprobe: **video 1280×720, audio, Dauer 5,482 s** (Soll 5,5 s). Bild bei 3,5 s: **rot** (der dritte Clip).
- Alle 10 MLT-Tests grün: Profil, Bildspur, Bild/Stumm, Musikspur, ohne Musik, Sonderzeichen in Dateinamen, Ablehnung, Grenzen, Argumente und echter Lauf.

## Offen
- Oberfläche im Video-Studio (Sitzung A, Bereich Film): Export „MLT-Projekt (Shotcut/melt)“ und „Mit melt rendern“ über `MltProject.ToXml` und `RenderArgs`. NOT RUN.
- Öffnen des MLT-Projekts in Shotcut oder Kdenlive: NOT RUN.
