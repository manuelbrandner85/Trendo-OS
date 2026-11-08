# Rangliste Runden 1–3 (gewichtete Matrix)

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

## CORE-03 – KI-Team einsetzen
Bestand: AgentCenter mit Rollen je Studio (StudioTeams), Claude über den Login der Person, Aufgaben mit Zustand. Projektregel: kein zweites Agenten-Laufzeitsystem.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| langchain-ai/langgraph | 9 | 3 | 8 | 10 | 8 | 8 | 9 | 7.6 | KONZEPT | nur Konzept (clean-room in AgentCenter): Zustandsgraph mit Zwischenständen und Wiederaufnahme nach Abbruch |
| pydantic/pydantic-ai | 8 | 3 | 9 | 10 | 8 | 9 | 8 | 7.55 | KONZEPT | nur Konzept: geprüfte, typisierte Antworten der KI (Schema) |
| crewAIInc/crewAI | 8 | 3 | 7 | 10 | 7 | 8 | 9 | 7.1 | KONZEPT | nur Konzept: Rolle, Ziel, Hintergrund je Agent und Übergaben – in StudioTeams bereits ähnlich |
| openai/openai-agents-python | 8 | 2 | 8 | 10 | 8 | 8 | 8 | 7.1 | VERWORFEN | an einen bezahlten Anbieter gebunden – nicht übernommen |
| microsoft/autogen | 8 | 2 | 7 | 6 | 7 | 8 | 9 | 6.3 | VERWORFEN | Doku CC-BY, zweites Laufzeitsystem – nicht übernommen |

## SYS-05 – Aufgaben automatisieren
Bestand: WorkflowEngine vorhanden; n8n als optionale externe Profi-Ebene (nicht mitgeliefert).

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| node-red/node-red | 8 | 7 | 8 | 10 | 8 | 7 | 9 | 8.05 | AUSWAHL | freie Alternative zu n8n (Apache-2.0) als externes Programm mit Projektübergabe (Flows als JSON) |
| n8n-io/n8n | 10 | 6 | 8 | 4 | 8 | 8 | 10 | 7.6 | VORHANDEN | Sustainable Use License (Fair-Code, nicht Open Source) – bleibt optional, nur Anbindung |
| windmill-labs/windmill | 9 | 4 | 8 | 5 | 9 | 8 | 7 | 7.05 | VERWORFEN | teils AGPL, Server – nicht übernommen |
| activepieces/activepieces | 8 | 4 | 8 | 6 | 8 | 8 | 7 | 6.85 | VERWORFEN | MIT mit kostenpflichtigen Teilen (ee) – nicht übernommen |
| FlowiseAI/Flowise | 8 | 4 | 7 | 5 | 7 | 7 | 3 | 6.15 | VERWORFEN | archiviert (2026-08) und gemischte Lizenz – verworfen |

## X-03 – Apps zusammenklicken
Bestand: Eigener App-Baukasten im Studio vorhanden (126 Tests).

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| appsmithorg/appsmith | 9 | 3 | 8 | 10 | 6 | 8 | 9 | 7.4 | KONZEPT | Apache-2.0, braucht Server/Docker – nur Konzept (Widget-Raster, Datenbindung) |
| baserow/baserow | 8 | 4 | 8 | 6 | 7 | 8 | 7 | 6.75 | VERWORFEN | MIT-Kern mit Premium-Teilen, Server – nicht übernommen |
| nocodb/nocodb | 8 | 4 | 8 | 4 | 7 | 8 | 9 | 6.55 | VERWORFEN | Fair-Code-Lizenz – nicht übernommen |
| Budibase/budibase | 8 | 3 | 8 | 5 | 7 | 8 | 8 | 6.45 | VERWORFEN | GPL-3.0 – nicht übernommen |
| ToolJet/ToolJet | 8 | 3 | 8 | 2 | 7 | 8 | 8 | 6.0 | AUSGESCHLOSSEN | AGPL-3.0 – ausgeschlossen |

## SPEC-01 – Wohnung planen
Bestand: Raumplanung mit Flächen, Möbeln (Assets/moebel) und 3D vorhanden (113 Tests).

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| ThatOpen/engine_components | 8 | 8 | 8 | 10 | 8 | 8 | 6 | 8.2 | AUSWAHL | IFC/BIM-Ansicht im WebView (MIT), wie der Web-Editor gebündelt |
| IfcOpenShell/IfcOpenShell | 9 | 6 | 8 | 7 | 8 | 8 | 7 | 7.65 | AUSWAHL | LGPL-3.0, getrenntes Programm: Grundriss als IFC exportieren/importieren |
| xeokit/xeokit-sdk | 9 | 6 | 8 | 2 | 9 | 8 | 6 | 6.95 | AUSGESCHLOSSEN | AGPL-3.0 – ausgeschlossen |
| cvdlab/react-planner | 7 | 6 | 6 | 10 | 7 | 7 | 3 | 6.9 | KONZEPT | MIT, seit 2024 ruhend – nur Konzept |
| furnishup/blueprint3d | 7 | 6 | 6 | 10 | 7 | 7 | 2 | 6.85 | KONZEPT | MIT, seit 2021 ruhend – nur Konzept (Wände ziehen, Raum in 3D) |

## SPEC-02 – Schaltungen berechnen
Bestand: Eigener Schaltplan mit transienter Simulation (MNA, Kondensator/Spule) und SPICE-Netzliste für ngspice/LTspice vorhanden (88 Tests).

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| SpiceSharp/SpiceSharp | 8 | 9 | 8 | 10 | 8 | 9 | 6 | 8.5 | AUSWAHL | .NET-Bibliothek (MIT): Halbleiter-Modelle (Diode, Transistor, MOSFET), AC- und DC-Analyse ergänzen die eigene Simulation – NuGet, Ergebnisse gegen eigene Simulation prüfen |
| imr/ngspice | 10 | 6 | 7 | 8 | 9 | 7 | 8 | 7.95 | VORHANDEN | modifizierte BSD (+ einzelne LGPL-Teile): bleibt externes Prüfprogramm; Netzliste ist schon vorhanden |
| KiCad/kicad-source-mirror | 10 | 4 | 8 | 5 | 8 | 8 | 10 | 7.35 | EXTERN | GPL-3.0 – externes Programm für Leiterplatten, Übergabe per Netzliste |
| ra3xdh/qucs_s | 9 | 3 | 7 | 5 | 8 | 7 | 7 | 6.5 | VERWORFEN | GPL-2.0 – höchstens externes Programm, nicht eingebaut |
| sharpie7/circuitjs1 | 8 | 3 | 6 | 5 | 7 | 7 | 4 | 5.85 | KONZEPT | GPL-2.0, seit 2024 ruhend – nur Konzept: sichtbar fließender Strom im Schaltplan |

## SPEC-03 – Statik nachrechnen
Bestand: Eigenes Weggrößenverfahren für ebene Rahmen mit Gelenken und Querschnitten vorhanden (90 Tests).

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| JWock82/Pynite | 9 | 5 | 8 | 10 | 7 | 8 | 7 | 7.8 | KONZEPT | MIT, Python: Vergleichsrechnung (Prüfreferenz) für die eigene Statik; Konzept 3D-Rahmen und P-Delta in eigenem Code |
| FreeCAD/FreeCAD | 9 | 4 | 7 | 8 | 7 | 7 | 10 | 7.2 | EXTERN | LGPL-2.1 – externes Programm (FEM-Arbeitsbereich) mit Dateiübergabe |
| KratosMultiphysics/Kratos | 9 | 2 | 8 | 9 | 9 | 7 | 7 | 7.15 | VERWORFEN | BSD, aber großes C++-Multiphysik-Paket – für einfache Statik zu schwer |
| anastruct/anaStruct | 7 | 5 | 7 | 7 | 7 | 8 | 6 | 6.65 | KONZEPT | LGPL-3.0, Python, 2D – doppelt zur eigenen Statik; nur Konzept Einflusslinien |
| OpenSees/OpenSees | 10 | 3 | 7 | 1 | 9 | 7 | 8 | 6.3 | AUSGESCHLOSSEN | kommerzielle Nutzung nur mit Vertrag – ausgeschlossen |

## SPEC-05 – Messwerte auswerten
Bestand: Eigene Messtabelle mit Diagramm, linearer/polynomischer/exponentieller Anpassung und Statistik vorhanden (87 Tests).

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| mathnet/mathnet-numerics | 9 | 9 | 8 | 10 | 9 | 9 | 8 | 8.95 | AUSWAHL | .NET-Bibliothek (MIT): nichtlineare Anpassung (Levenberg-Marquardt), FFT, Interpolation, Verteilungen |
| ScottPlot/ScottPlot | 9 | 8 | 8 | 10 | 9 | 9 | 9 | 8.8 | OPTION | .NET (MIT) mit Avalonia-Steuerelement: Zoom/Verschieben – Option, wenn die eigenen Diagramme nicht reichen |
| oxyplot/oxyplot | 8 | 7 | 7 | 10 | 8 | 9 | 6 | 7.95 | VERWORFEN | MIT, weniger aktiv als ScottPlot – nicht übernommen |
| lmfit/lmfit-py | 9 | 4 | 8 | 10 | 8 | 8 | 8 | 7.75 | KONZEPT | BSD-3, Python – nur Konzept: Fit-Parameter mit Grenzen und Unsicherheiten |
| veusz/veusz | 8 | 3 | 7 | 5 | 7 | 7 | 6 | 6.1 | VERWORFEN | GPL-2.0 – höchstens externes Programm |

## SPEC-06 – Lernen mit Karteikarten
Bestand: Eigene Karteikarten mit SM-2 (abgestufte Antworten) vorhanden (90 Tests).

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| open-spaced-repetition/py-fsrs | 10 | 8 | 9 | 10 | 10 | 9 | 8 | 9.25 | AUSWAHL | MIT: FSRS-Planer als Vorlage für einen eigenen C#-Planer (genauere Abstände als SM-2), Vermerk in THIRD_PARTY.md |
| open-spaced-repetition/fsrs-rs | 10 | 5 | 9 | 10 | 10 | 9 | 8 | 8.65 | OPTION | BSD-3, Rust: Optimierer (Gewichte aus eigener Lerngeschichte) – erst bei genug Daten, Planer selbst braucht kein Rust |
| open-spaced-repetition/fsrs4anki | 8 | 4 | 8 | 10 | 8 | 8 | 8 | 7.5 | KONZEPT | MIT – Konzept: Zielbehaltensrate einstellbar |
| ankitects/anki | 10 | 3 | 8 | 2 | 9 | 8 | 10 | 6.8 | AUSGESCHLOSSEN | AGPL-3.0 – Code ausgeschlossen; nur Konzept: .apkg-Import (SQLite-Format, eigener Leser) |
| mnemosyne-proj/mnemosyne | 7 | 3 | 6 | 5 | 7 | 7 | 4 | 5.6 | VERWORFEN | gemischte Lizenz, wenig aktiv – nicht übernommen |

## SPEC-04 – VR & AR gestalten
Bestand: Eigene Prüfung von glTF/GLB auf Brillen-Tauglichkeit (Dreiecke, Texturen, Zeitbudget je Bild) vorhanden; 3D-Vorschau über f3d und Umwandlung über assimp (Runde 1).

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| donmccurdy/glTF-Transform | 10 | 7 | 9 | 10 | 9 | 8 | 8 | 8.85 | AUSWAHL | MIT, Node-Kommandozeile: GLB für Brillen optimieren (meshopt, Draco, Texturen verkleinern, doppelte Daten entfernen) – passt direkt zur vorhandenen Prüfung |
| KhronosGroup/glTF-Validator | 9 | 8 | 9 | 10 | 9 | 9 | 7 | 8.85 | AUSWAHL | Apache-2.0, offizieller Prüfer der Khronos Group als Kommandozeile – Fehlerbericht zu jeder GLB vor dem Hochladen |
| zeux/meshoptimizer | 9 | 5 | 10 | 10 | 10 | 9 | 9 | 8.6 | KONZEPT | MIT, steckt in glTF-Transform – nicht getrennt einbauen |
| google/model-viewer | 9 | 6 | 9 | 10 | 8 | 9 | 9 | 8.45 | OPTION | Apache-2.0, Web-Komponente: AR-Vorschau auf dem Handy (WebXR/Scene Viewer) – Option für den Web-Export |
| KhronosGroup/KTX-Software | 8 | 5 | 8 | 10 | 9 | 8 | 7 | 7.75 | OPTION | Apache-2.0, KTX2/Basis-Texturen (GPU-komprimiert) – über glTF-Transform nutzbar, Option |

## SPEC-10 – Geräte überwachen
Bestand: Eigener digitaler Zwilling: Anlagen, Sensoren, CSV-Import, Ampel, Trend, Vorhersage bis zum Alarm (Live-Werte noch ohne Protokoll).

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| dotnet/MQTTnet | 10 | 10 | 9 | 10 | 9 | 9 | 9 | 9.6 | AUSWAHL | .NET-Bibliothek (MIT): Live-Messwerte über MQTT (Standard bei Sensoren, Shelly, Tasmota, Home Assistant) direkt in den Zwilling – nur lesen, nur nach Freigabe |
| eclipse-mosquitto/mosquitto | 9 | 6 | 9 | 9 | 10 | 8 | 10 | 8.45 | OPTION | EPL-2.0 oder BSD-3: eigener MQTT-Broker als externes Programm, falls im Haus keiner läuft – Option |
| influxdata/telegraf | 9 | 4 | 8 | 10 | 9 | 8 | 9 | 7.9 | KONZEPT | MIT, Sammel-Dienst – für Trendo zu groß; nur Konzept (Eingangs-Plugins wie Modbus) |
| home-assistant/core | 10 | 3 | 8 | 10 | 7 | 7 | 10 | 7.7 | VERWORFEN | Apache-2.0, eigenes Hausautomations-System – nicht einbauen; Anbindung über MQTT reicht |
| grafana/grafana | 9 | 3 | 8 | 2 | 8 | 8 | 10 | 6.45 | AUSGESCHLOSSEN | AGPL-3.0 – ausgeschlossen |

## SPEC-14 – Fräsen & Fertigen
Bestand: Eigene G-Code-Prüfung (Weg, Zeit, Grenzen, Werkzeuge, Vorschau), Schnittwerte, Taschen, Lochkreise, Konturen, Zerspanleistung.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| xyz-tools/gcode-preview | 8 | 7 | 8 | 10 | 8 | 9 | 5 | 8.05 | OPTION | MIT, 3D-Vorschau der Werkzeugwege im Browserfenster – Option, wenn die eigene Vorschau nicht reicht |
| CauldronDevelopmentLLC/CAMotics | 9 | 5 | 8 | 6 | 8 | 8 | 6 | 7.25 | EXTERN | GPL-2.0: Abtrag-Simulation als externes Programm (G-Code übergeben) – zeigt das fertige Werkstück |
| aewallin/opencamlib | 8 | 4 | 8 | 7 | 9 | 8 | 5 | 7.0 | KONZEPT | LGPL-2.1, Werkzeugweg-Berechnung für 3D-Flächen – nur als getrennter Prozess; Konzept für später |
| gnea/grbl | 8 | 3 | 8 | 5 | 9 | 6 | 7 | 6.4 | VERWORFEN | GPL-3.0 Maschinen-Firmware – nicht einbauen; Senden an echte Maschinen nur nach eigener Freigabe (gefährlicher Aktor) |
| Denvi/Candle | 8 | 3 | 7 | 5 | 8 | 6 | 6 | 6.1 | VERWORFEN | GPL-3.0 Steuerprogramm – nicht übernommen (Maschinensteuerung bleibt außen vor) |

## SPEC-16 – Auto & Fahrten
Bestand: Eigenes Tankbuch (Volltank-Methode), Fahrtenbuch mit Lücken-Prüfung und Festschreiben, Erinnerungen (HU, Service).

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| Project-OSRM/osrm-backend | 9 | 3 | 9 | 10 | 9 | 8 | 9 | 7.85 | VERWORFEN | BSD-2 Routing-Server – braucht große Datenaufbereitung; Routing bleibt im Karten-Studio |
| hargata/lubelog | 9 | 4 | 8 | 10 | 8 | 8 | 8 | 7.75 | KONZEPT | MIT – nur Konzept: Wartungs- und Reparaturhistorie je Fahrzeug, Kosten je km, Anhänge (Rechnungen); eigener Code in Vehicles |
| traccar/traccar | 9 | 3 | 8 | 10 | 8 | 8 | 9 | 7.6 | VERWORFEN | Apache-2.0 GPS-Server – zu groß; Fahrten als GPX importieren reicht (Karten-Studio) |
| brendan-w/python-OBD | 8 | 4 | 7 | 5 | 8 | 7 | 5 | 6.35 | KONZEPT | GPL-2.0 – Code nicht übernehmen; Konzept: Fehlercodes und km-Stand über OBD-II-Adapter (ELM327, eigenes Protokoll) |
| openmaptiles/openmaptiles | 7 | 2 | 8 | 6 | 8 | 8 | 8 | 6.25 | VERWORFEN | Kartenschema – nicht nötig (Vektorkarten nutzen Protomaps) |

## SPEC-11 – Geld & Rechnungen
Bestand: Eigene Rechnungen, Kasse, GiroCode (EPC-QR) und Auswertungen vorhanden; E-Rechnung (XRechnung/ZUGFeRD) fehlt noch.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| stephanstapel/ZUGFeRD-csharp | 10 | 10 | 8 | 10 | 9 | 9 | 8 | 9.4 | AUSWAHL | .NET-Bibliothek (Apache-2.0): E-Rechnungen als ZUGFeRD/Factur-X (PDF mit XML) und XRechnung schreiben und lesen – in Deutschland für Firmen Pflicht (Empfang seit 2025, Versand ab 2027/28) |
| itplr-kosit/validator | 9 | 5 | 9 | 10 | 8 | 9 | 7 | 8.15 | OPTION | Apache-2.0, offizieller Prüfer der KoSIT (Java) – XRechnung gegen die amtlichen Regeln prüfen; Option, braucht Java |
| ZUGFeRD/mustangproject | 9 | 4 | 8 | 10 | 8 | 8 | 7 | 7.7 | KONZEPT | Apache-2.0 (Java) – Konzept: Prüfen und Visualisieren von E-Rechnungen; die .NET-Bibliothek reicht zum Schreiben |
| invoiceninja/invoiceninja | 9 | 2 | 8 | 2 | 8 | 8 | 9 | 6.2 | AUSGESCHLOSSEN | Elastic License 2.0 – nicht Open Source, ausgeschlossen |
| akaunting/akaunting | 8 | 2 | 8 | 2 | 8 | 8 | 9 | 5.95 | AUSGESCHLOSSEN | Business Source License – ausgeschlossen |

## SYS-09 – E-Mails & Termine
Bestand: Eigenes E-Mail-Programm (IMAP/SMTP-Protokoll), Adressbuch und Kalender mit iCal-Export vorhanden.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| jstedfast/MailKit | 10 | 9 | 10 | 10 | 9 | 10 | 9 | 9.65 | OPTION | .NET (MIT), Standard-Bibliothek für IMAP/SMTP/POP3 mit OAuth2 und IDLE – Option, falls das eigene Protokoll an Grenzen stößt (z. B. OAuth bei Microsoft/Google) |
| ical-org/ical.net | 9 | 10 | 8 | 10 | 9 | 9 | 7 | 9.1 | AUSWAHL | .NET (MIT): Kalenderdateien mit Wiederholungen (RRULE), Ausnahmen und Zeitzonen sicher lesen – für den Import fremder .ics |
| thunderbird/thunderbird-desktop | 10 | 3 | 8 | 7 | 8 | 9 | 10 | 7.55 | EXTERN | MPL-2.0 – externes Programm; Übergabe per .eml/.ics reicht |
| Kozea/Radicale | 8 | 4 | 8 | 5 | 9 | 8 | 8 | 6.85 | KONZEPT | GPL-3.0 CalDAV/CardDAV-Server – nur Konzept (Kalender mit Handy abgleichen) |
| nextcloud/calendar | 8 | 2 | 8 | 2 | 8 | 8 | 8 | 5.9 | AUSGESCHLOSSEN | AGPL-3.0 – ausgeschlossen |

## STUDIO-LETTER-01 – Briefe schreiben
Bestand: Eigene Briefe nach DIN 5008 mit PDF, Vorlagen und Claude-Unterstützung vorhanden; typst seit Runde 2 eingebaut.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| Sematre/typst-letter-pro | 9 | 9 | 8 | 10 | 10 | 9 | 5 | 8.9 | AUSWAHL | MIT, typst-Vorlage für Briefe nach DIN 5008 (Faltmarken, Anschriftfeld) – passt zur typst-Anbindung; als Vorlage übernehmen, Stand 2025 |
| languagetool-org/languagetool | 10 | 5 | 9 | 7 | 7 | 9 | 10 | 8.0 | OPTION | LGPL-2.1, Grammatik- und Stilprüfung als lokaler Dienst (Java) – Option zusätzlich zur Rechtschreibung (Hunspell) |
| QuestPDF/QuestPDF | 9 | 8 | 9 | 3 | 9 | 9 | 9 | 7.9 | VERWORFEN | Doppellizenz, für größere Firmen kostenpflichtig – nicht übernommen (PDFsharp ist schon da) |
| komascript/komascript | 9 | 3 | 8 | 7 | 8 | 8 | 7 | 7.05 | KONZEPT | LPPL (LaTeX) – Konzept: Briefklasse scrlttr2 als Vorbild für Layout-Regeln |
| wkhtmltopdf/wkhtmltopdf | 7 | 5 | 6 | 6 | 6 | 4 | 2 | 5.65 | VERWORFEN | LGPL-3.0, archiviert und unsicher – nicht verwenden |

## STUDIO-ONLINE-BUSINESS-01 – Online verkaufen
Bestand: Eigene Verkaufsstatistik, Kasse und Geschäftshilfen vorhanden; kein eigener Shop-Server.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| saleor/saleor | 9 | 2 | 9 | 10 | 8 | 8 | 9 | 7.55 | KONZEPT | BSD-3, aber kompletter Shop-Server (Python, Datenbank) – für Trendo zu groß; nur Konzept (Bestell-Ablauf) |
| medusajs/medusa | 9 | 2 | 9 | 7 | 8 | 8 | 10 | 7.15 | VERWORFEN | MIT mit Enterprise-Teilen, Shop-Server (Node) – nicht übernommen |
| bagisto/bagisto | 8 | 2 | 7 | 10 | 7 | 7 | 9 | 6.8 | VERWORFEN | MIT, Laravel-Shop – Server, nicht übernommen |
| woocommerce/woocommerce | 9 | 3 | 7 | 5 | 7 | 7 | 10 | 6.55 | KONZEPT | GPL-2.0, WordPress-Plugin – nur Anbindung über die offizielle REST-Schnittstelle als Konzept |
| PrestaShop/PrestaShop | 8 | 2 | 7 | 5 | 7 | 7 | 8 | 6.0 | VERWORFEN | OSL-3.0 – nicht übernommen |

## CORE-06 – Spiele bauen
Bestand: Godot/Unreal/Unity-Anbindung, eigener Kachel-Leveleditor, Spielregeln und Geräusch-Werkstatt (Umgebungsgeräusche) vorhanden; typische Spiel-Soundeffekte (Sprung, Münze, Laser) fehlen.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| chr15m/jsfxr | 9 | 10 | 8 | 10 | 10 | 10 | 6 | 9.25 | AUSWAHL | Unlicense (sfxr-Verfahren gemeinfrei): Retro-Soundeffekte (Sprung, Münze, Laser, Explosion, Treffer, Power-up) aus wenigen Reglern – als eigener C#-Code in die Geräusch-Werkstatt, ohne Samples, Rechte eindeutig |
| mapeditor/tiled | 9 | 8 | 8 | 8 | 9 | 9 | 9 | 8.5 | AUSWAHL | Programm GPL-2.0, Kartenformat offen (libtiled BSD-2): Trendo-Level als Tiled-Karte (.tmj) ausgeben – Godot, Phaser, Defold u. a. lesen sie; Tiled selbst nur extern |
| bevyengine/bevy | 9 | 3 | 9 | 10 | 10 | 9 | 10 | 8.1 | OPTION | MIT/Apache-2.0, Rust-Engine – stark, aber Godot ist schon angebunden; Option für Rust-Projekte |
| deepnight/ldtk | 8 | 6 | 7 | 10 | 9 | 9 | 8 | 7.95 | KONZEPT | MIT, Level-Editor mit JSON-Format – Konzept (Auto-Layer-Regeln); Format-Import später |
| MonoGame/MonoGame | 8 | 5 | 8 | 9 | 9 | 8 | 9 | 7.7 | KONZEPT | MS-PL, C#-Framework – Konzept, Godot deckt C# ab |
| raysan5/raylib | 7 | 4 | 9 | 10 | 9 | 8 | 10 | 7.6 | OPTION | zlib – Lern-Engine; nicht nötig neben Godot |
| LibreSprite/LibreSprite | 7 | 3 | 7 | 5 | 8 | 8 | 7 | 6.1 | EXTERN | GPL-2.0, Pixel-Grafik – nur extern mit Dateiübergabe |

## CORE-07 – Webseite bauen
Bestand: Web Design Studio (Puck, R3F, GrapesJS-Import) und eigene Qualitätsprüfung (Handy, Ladezeit, Barrierefreiheit, SEO, Links) ohne Netz vorhanden.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| dequelabs/axe-core | 10 | 9 | 9 | 8 | 9 | 9 | 9 | 9.1 | AUSWAHL | MPL-2.0 unverändert als Datei: echte Barrierefreiheitsprüfung (WCAG 2.2) in der Vorschau-WebView – ergänzt die eigene Prüfung, offline |
| lycheeverse/lychee | 8 | 8 | 9 | 10 | 10 | 9 | 7 | 8.7 | OPTION | Apache-2.0/MIT, Rust-Einzelprogramm: Links einer fertigen Seite schnell prüfen (auch externe, nur auf Wunsch) – Option zur eigenen Linkprüfung |
| gohugoio/hugo | 9 | 6 | 9 | 10 | 10 | 9 | 10 | 8.7 | OPTION | Apache-2.0, Einzelprogramm: statische Seiten/Blogs aus Markdown – Option für große Blogs |
| GoogleChrome/lighthouse | 9 | 4 | 9 | 10 | 6 | 8 | 10 | 7.8 | OPTION | Apache-2.0, braucht Node + Chrome – Option; axe-core ist darin schon der Barrierefreiheitsteil |
| withastro/astro | 9 | 3 | 9 | 10 | 8 | 8 | 10 | 7.8 | VERWORFEN | MIT, Node-Werkzeugkette – Trendo hat einen eigenen Seitenbau; nicht übernommen |
| 11ty/eleventy | 8 | 3 | 8 | 10 | 8 | 8 | 8 | 7.3 | VERWORFEN | MIT (heißt jetzt „buildawesome“), Node – nicht übernommen |

## SPEC-08 – Kleidung & Produkte entwerfen
Bestand: Programm „Kleidung & Produkte“ mit Größen- und Produktrechnern vorhanden; echte Schnittmuster nach Körpermaßen fehlen.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| freesewing/freesewing | 10 | 8 | 8 | 10 | 8 | 9 | 6 | 8.8 | AUSWAHL | MIT (aktiv auf Codeberg): Schnittmuster nach eigenen Körpermaßen als SVG/PDF (Hemd, Hose, Rock …) – als Bündel im WebView oder Node-Prozess, offline |
| CadQuery/cadquery | 8 | 6 | 8 | 10 | 8 | 9 | 8 | 8.0 | OPTION | Apache-2.0, Python – gleiche Rolle wie build123d; eins von beiden genügt |
| gumyr/build123d | 8 | 6 | 8 | 10 | 8 | 9 | 7 | 7.95 | OPTION | Apache-2.0, Python: Produktteile parametrisch als STEP/STL – Option über die KI-Laufzeit |
| FreeCAD/FreeCAD | 10 | 4 | 7 | 7 | 7 | 8 | 10 | 7.4 | EXTERN | LGPL-2.1 – externes Programm, Übergabe STEP |
| openscad/openscad | 8 | 4 | 7 | 5 | 8 | 8 | 9 | 6.65 | EXTERN | GPL-2.0 – externes Programm |
| FashionFreedom/Seamly2D | 9 | 3 | 7 | 5 | 8 | 8 | 6 | 6.55 | EXTERN | GPL-3.0 Schnittprogramm – nur extern mit Dateiübergabe |

## CORE-09 – Kameras & Roboter steuern
Bestand: Webcams, RTSP und ONVIF-Suche, Roboterarm und Fahrroboter mit Sicherheitsfreigabe vorhanden; Objekte erkennen/zählen fehlt.

| Kandidat | Funktion | Integration | Code | Lizenz | Leistung | Sicherheit | Gemeinschaft | Punkte | Entscheidung | Integrationsweg |
|---|---|---|---|---|---|---|---|---|---|---|
| microsoft/onnxruntime | 9 | 9 | 9 | 10 | 9 | 9 | 10 | 9.2 | VORHANDEN | MIT – Laufzeit für YOLOX, in der KI-Laufzeit vorhanden bzw. per NuGet |
| AlexxIT/go2rtc | 9 | 8 | 8 | 10 | 9 | 8 | 9 | 8.7 | OPTION | MIT, Einzelprogramm: viele Kameramarken (RTSP, ONVIF, HomeKit …) verzögerungsarm in die WebView – Option neben mediamtx |
| Megvii-BaseDetection/YOLOX | 9 | 8 | 8 | 10 | 8 | 9 | 7 | 8.6 | AUSWAHL | Apache-2.0: Objekterkennung als ONNX-Modell mit onnxruntime (MIT) – Personen/Dinge im Kamerabild erkennen und zählen, lokal, ohne Cloud |
| ultralytics/ultralytics | 10 | 7 | 8 | 2 | 9 | 8 | 10 | 7.6 | AUSGESCHLOSSEN | AGPL-3.0 – ausgeschlossen (YOLOX ist Apache-2.0) |
| blakeblackshear/frigate | 9 | 2 | 8 | 10 | 8 | 8 | 10 | 7.45 | KONZEPT | MIT, aber kompletter Docker-Dienst – nur Konzept (Ereignis-Zeitleiste, Zonen) |
| ros2/ros2 | 9 | 2 | 8 | 10 | 8 | 8 | 9 | 7.4 | KONZEPT | Apache-2.0, riesiges Robotik-Gerüst – Konzept (Nachrichten/Themen), kein Einbau |
| esphome/esphome | 8 | 4 | 8 | 6 | 8 | 7 | 9 | 6.85 | EXTERN | MIT/GPL-3.0 gemischt, Mikrocontroller-Firmware – nur extern |
