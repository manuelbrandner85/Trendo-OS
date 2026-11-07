# Rangliste Runden 1 und 2 (gewichtete Matrix)

Gewichte: Funktion 25 %, Integration 20 %, Code 15 %, Lizenz 15 %, Leistung 10 %, Sicherheit 10 %, Gemeinschaft 5 %. Lizenzverstöße/unvertretbare Risiken sind Ausschluss unabhängig von Punkten.

## CORE-08 – Videos & Fotos bearbeiten
Bestand: FFmpeg ist bereits Kern (59 Dateien); eigener Schnitt/Bildbearbeitung im Studio vorhanden.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| FFmpeg/FFmpeg | 9 | 9 | 8 | 7 | 9 | 7 | 10 | 8.4 | VORHANDEN | bereits integriert (CLI) – bleibt Kodier-/Export-Kern |
| mltframework/mlt | 9 | 8 | 8 | 8 | 8 | 8 | 7 | 8.2 | AUSWAHL | Prozess-Adapter (melt-CLI, MLT-XML): Mehrspur-Timeline, Übergänge, Filter, Keyframes – als getrennter Prozess LGPL-verträglich |
| darktable-org/darktable | 8 | 6 | 7 | 6 | 7 | 8 | 8 | 7.05 | OPTIONAL | externes Programm (darktable-cli) für RAW-Entwicklung, kontrollierte Übergabe |
| KDE/kdenlive | 9 | 3 | 7 | 5 | 7 | 8 | 8 | 6.55 | ÜBERGABE | GPL-Anwendung, nur Projektübergabe (MLT-XML) – kein Einbau |
| olive-editor/olive | 7 | 3 | 7 | 5 | 7 | 7 | 3 | 5.7 | VERWORFEN | letzter Push 2024-12, Umbau unvollendet – nicht gewählt |

## STUDIO-MUSIK-01 – Trendo Musik
Bestand: ACE-Step (Lieder aus Beschreibung) und Demucs (Spuren trennen) sind integriert; eigene Mischer/Pianorolle vorhanden.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| spotify/basic-pitch | 8 | 9 | 8 | 10 | 8 | 8 | 7 | 8.45 | AUSWAHL | Python-Adapter: Audio → MIDI (Melodie/Akkorde erkennen) für die vorhandene Pianorolle |
| ace-step/ACE-Step | 9 | 8 | 7 | 10 | 6 | 8 | 7 | 8.15 | VORHANDEN | bereits integriert |
| facebookresearch/demucs | 8 | 8 | 7 | 10 | 6 | 8 | 5 | 7.8 | BEOBACHTEN | archiviert seit 2024-04 – im Einsatz, Ersatz beobachten |
| surge-synthesizer/surge | 8 | 4 | 8 | 5 | 8 | 8 | 7 | 6.7 | OPTIONAL | GPL-Synthesizer, nur als externes Instrument/Klangquelle |
| Tracktion/tracktion_engine | 9 | 5 | 8 | 2 | 8 | 8 | 5 | 6.6 | AUSGESCHLOSSEN | GPL-3 oder kostenpflichtig – ausgeschlossen |
| audacity/audacity | 8 | 3 | 7 | 5 | 7 | 8 | 9 | 6.35 | ÜBERGABE | GPL-Anwendung, nur Dateiübergabe |

## SPEC-13 – Routen & Karten
Bestand: Karten-Dokument (MapDoc), Offline-MBTiles und OSRM-Routing über einen selbst eingetragenen Server vorhanden.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| maplibre/maplibre-gl-js | 9 | 8 | 9 | 10 | 8 | 8 | 9 | 8.75 | AUSWAHL | Vektorkarten-Darstellung im WebView (BSD-3), passt zu PMTiles |
| protomaps/PMTiles | 8 | 9 | 8 | 10 | 9 | 9 | 7 | 8.65 | AUSWAHL | Offline-Vektorkarten aus einer einzigen Datei (BSD-3), lesbar ohne Server |
| graphhopper/graphhopper | 8 | 6 | 8 | 10 | 7 | 8 | 7 | 7.75 | OPTIONAL | lokales Routing (Apache-2.0, braucht Java) als Alternative zum OSRM-Server |
| valhalla/valhalla | 8 | 4 | 8 | 10 | 8 | 8 | 7 | 7.45 | SPÄTER | MIT, Windows-Bau aufwendig – später prüfen |
| qgis/QGIS | 10 | 3 | 8 | 5 | 7 | 8 | 9 | 7.0 | ÜBERGABE | GPL-Anwendung, nur Projekt-/Dateiübergabe (GeoPackage) |

## CORE-05 – 3D-Modelle erstellen
Bestand: Blender-Anbindung (58 Dateien), eigener 3D-Betrachter, TripoSR (3D aus Bild) vorhanden.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| assimp/assimp | 9 | 7 | 8 | 10 | 8 | 7 | 9 | 8.3 | AUSWAHL | Import von 40+ 3D-Formaten (BSD-3) über native Bibliothek/Prozess |
| f3d-app/f3d | 7 | 9 | 8 | 10 | 9 | 8 | 7 | 8.3 | AUSWAHL | Vorschaubilder/Ansicht beliebiger 3D-Dateien per CLI (BSD-3) |
| blender/blender | 10 | 7 | 8 | 6 | 8 | 8 | 10 | 8.1 | VORHANDEN | bereits integriert (externes Programm, GPL – getrennter Prozess) |
| isl-org/Open3D | 8 | 6 | 8 | 10 | 8 | 8 | 8 | 7.9 | OPTIONAL | Netz-/Punktwolken-Verarbeitung (MIT), auch für SPEC-09 Scannen |
| MeshInspector/MeshLib | 8 | 6 | 8 | 0 | 8 | 8 | 4 | 6.2 | AUSGESCHLOSSEN | nur nicht-kommerziell – ausgeschlossen |

## SPEC-09 – Gegenstände einscannen
Bestand: COLMAP (BSD-3, mit Prüfsumme im Katalog) und Meshroom (Anleitung) sind angebunden; Abdeckungsansicht vorhanden.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| colmap/colmap | 9 | 8 | 8 | 10 | 7 | 8 | 9 | 8.5 | VORHANDEN | bereits integriert |
| isl-org/Open3D | 8 | 7 | 8 | 10 | 8 | 8 | 8 | 8.1 | AUSWAHL | Python-Adapter: Punktwolke/Netz aufräumen, Löcher schließen, Maße und Volumen messen (MIT) |
| nerfstudio-project/gsplat | 8 | 5 | 8 | 10 | 7 | 8 | 7 | 7.55 | OPTIONAL | Gaussian Splatting aus COLMAP-Ergebnis (Apache-2.0), nur mit NVIDIA-GPU – optional |
| openMVG/openMVG | 8 | 6 | 8 | 7 | 7 | 8 | 7 | 7.3 | VERWORFEN | MPL-2.0, Überschneidung mit COLMAP – nicht nötig |
| cdcseacave/openMVS | 9 | 5 | 8 | 2 | 7 | 8 | 7 | 6.6 | AUSGESCHLOSSEN | AGPL-3.0 – als Einbau ausgeschlossen |
| graphdeco-inria/gaussian-splatting | 9 | 5 | 7 | 0 | 7 | 8 | 9 | 6.25 | AUSGESCHLOSSEN | nur nicht-kommerziell – ausgeschlossen |

## SPEC-15 – Live streamen
Bestand: FFmpeg als Kodier-Kern vorhanden, OBS als externes Programm erwähnt.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| bluenviron/mediamtx | 8 | 9 | 8 | 10 | 9 | 8 | 9 | 8.65 | AUSWAHL | Einzelprogramm (MIT): lokaler RTMP/SRT/WebRTC-Server für Vorschau, Weiterleitung und Aufnahme |
| ossrs/srs | 8 | 6 | 8 | 10 | 9 | 8 | 8 | 8.0 | OPTIONAL | MIT, mächtiger als nötig – Alternative |
| obsproject/obs-studio | 10 | 4 | 8 | 5 | 8 | 8 | 10 | 7.35 | ÜBERGABE | GPL-Anwendung, Steuerung über obs-websocket – externe Übergabe |
| Haivision/srt | 7 | 6 | 8 | 7 | 9 | 8 | 6 | 7.2 | VORHANDEN | Protokoll-Bibliothek, über FFmpeg bereits nutzbar |
| arut/nginx-rtmp-module | 6 | 4 | 7 | 10 | 8 | 6 | 3 | 6.4 | VERWORFEN | letzter Push 2024-12, nur RTMP – verworfen |

## STUDIO-TV-01 – TV & Live
Bestand: Senderlisten, Programmführer, Aufnahmen; mpv als Abspieler (24 eigene Tests vorhanden). Nur legale Quellen (Mediatheken, frei empfangbar).

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| streamlink/streamlink | 8 | 8 | 8 | 10 | 8 | 8 | 8 | 8.3 | AUSWAHL | Python-Adapter (BSD-2): Live-Seiten öffentlich-rechtlicher Mediatheken in einen Abspielstrom wandeln – nur legale Quellen |
| mpv-player/mpv | 9 | 8 | 8 | 6 | 9 | 8 | 9 | 8.1 | VORHANDEN | bereits integriert (externes Programm, GPL) |
| iptv-org/iptv | 7 | 8 | 6 | 9 | 9 | 3 | 9 | 7.25 | AUSGESCHLOSSEN | Sammelliste mit ungeprüften Quellen – widerspricht „nur legale Quellen“; nicht übernommen |
| jellyfin/jellyfin | 9 | 4 | 8 | 5 | 7 | 8 | 9 | 6.95 | ÜBERGABE | GPL-Medienserver, nur als externe Anbindung |
| tvheadend/tvheadend | 8 | 3 | 7 | 5 | 8 | 7 | 6 | 6.2 | SPÄTER | für DVB-Hardware, Linux-lastig – später |

## SYS-10 – Texte & Tabellen
Bestand: Eigene Text-, Tabellen- und Präsentationsprogramme vorhanden (154 Tests), PDF über qpdf.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| typst/typst | 9 | 9 | 9 | 10 | 9 | 9 | 9 | 9.15 | AUSWAHL | Einzelprogramm (Apache-2.0): professioneller Schriftsatz und PDF aus Vorlagen |
| excalidraw/excalidraw | 8 | 8 | 9 | 10 | 8 | 8 | 9 | 8.5 | OPTIONAL | Whiteboard/Skizzen im WebView (MIT), wie der Web-Editor gebündelt |
| jgm/pandoc | 9 | 7 | 9 | 5 | 9 | 8 | 9 | 7.9 | OPTIONAL | GPL, als getrenntes Programm für Formatwandlung (DOCX/ODT/Markdown) |
| LibreOffice/core | 10 | 4 | 7 | 5 | 6 | 8 | 9 | 6.95 | ÜBERGABE | GPL/MPL-Anwendung, Umwandlung per soffice --headless als externe Übergabe |
| ONLYOFFICE/DocumentServer | 9 | 3 | 7 | 2 | 6 | 7 | 8 | 5.9 | AUSGESCHLOSSEN | AGPL-3.0 und Server – ausgeschlossen |
