# STUDIO 3 – Film, Video & VFX – genehmigte Verbesserungen

Trendo-Katalog: `CORE-08` · **20 Nutzer-JA** · Stand: **APPROVED_SPEC** (Status der einzelnen Funktionen per Live-Audit verifizieren)

## Bewährte Steuerung
Reihenfolge für jede Funktion: vorhandenes Trendo prüfen → REA-Clean-Room/Native Integration bewerten → Schnittstellen/Dateien/Versionierung definieren → echten vertikalen Slice bauen → positive/negative Tests → Performance/Security/License/QC → Berichte/Revision. Unity-Relevanz je Studio steht in `03_GLOBALE_REGELN_REA_UNITY.md`.

| Nr. | Verbindliche Verbesserung | Konkreter Funktionsumfang | Claude-Agent | Ziel-Artefakt |
|---|---|---|---|---|
| 01 | **AI Film Director** | Claude plant Drehbuch, Szenen, Einstellungen, Storyboards, Dreharbeiten und Produktion. | `14-media-vfx` | Szenenliste, Storyboard und Shot-Plan |
| 02 | **Professional Video Editor** | Mehrspur-Timeline, präziser Schnitt, Keyframes, Übergänge, Multicam, Proxy-Dateien und Undo. | `14-media-vfx` | Editierbare Sequenz, EDL/OTIO-Ausgabe |
| 03 | **AI Storyboard & Previsualization** | Aus Drehbüchern automatisch Storyboards, Kamerapläne und animierte Szenenvorschauen erstellen. | `14-media-vfx` | Storyboard mit Scene-/Shot-IDs |
| 04 | **Cinematic Camera Director** | Virtuelle Objektive, Tiefenschärfe, Kamerafahrten, Fokus, Brennweiten und filmische Bildkomposition. | `14-media-vfx` | Keyframe-Track, reproduzierbare Kamera |
| 05 | **Ultimate VFX Compositing** | Node-basierte Effekte, Greenscreen, Rotoscoping, Masken, Tracking und digitale Bildkomposition. | `14-media-vfx` | Nodegraph, Maske, Rendervergleich |
| 06 | **Realistic Effects & Simulation** | Rauch, Feuer, Wasser, Wetter, Partikel, Explosionen und Zerstörung in Blender/Unreal. | `12-blender-assets` | Simulationsprojekt und Render-Preview |
| 07 | **Virtual Film Production** | Digitale Sets, virtuelle Studios, kameragesteuerte Welten und optionale LED-/Kamera-Anbindungen. | `13-unreal-games` | Stage-Szene und Synchronisationscheck |
| 08 | **Photorealistic Digital Actors** | Digitale Schauspieler, Gesichtsausdrücke, Körperbewegungen, Kleidung und realistische Charakteranimation. | `12-blender-assets` | Charakterasset, Rig, Lizenzen/Einwilligung |
| 09 | **AI Lip Sync & Facial Acting** | Lippensynchronisation, Gesichtsmimik, Blickrichtung und emotionale Animation für digitale Figuren. | `14-media-vfx` | Audio/Viseme-Spur mit Vergleichsvorschau |
| 10 | **Film Animation Studio** | 2D-/3D-Animationen, Motion Capture, Keyframes, Retargeting und komplexe Bewegungsabläufe. | `12-blender-assets` | Editierbare Bewegungskurven |
| 11 | **Professional Color Grading** | Farbkorrektur pro Clip, LUTs, HDR, ACES/OpenColorIO, Scopes und einheitliche Filmlooks. | `14-media-vfx` | Farb-Pipeline und Scopes-Ausgabe |
| 12 | **AI Video Enhancement** | Stabilisierung, Rauschreduzierung, Schärfe, Upscaling und Bildrestaurierung mit Qualitätsvergleich. | `14-media-vfx` | Vorher/Nachher-Testclip, Qualitätswerte |
| 13 | **Generative AI Video Studio** | Videoszenen aus Text oder Bildern mit verfügbaren lokalen oder optional angebundenen KI-Modellen erzeugen. | `07-ai-local-models` | Clip, Prompt/Seed/Provenienz |
| 14 | **Cinematic Sound Design** | Dialogbearbeitung, Geräusche, Foley, Raumklang, Mischung, Lautheitskontrolle und Soundeffekte. | `14-media-vfx` | Synchronisierter Filmmix mit Stems |
| 15 | **Film Music Integration** | Dynamische Filmmusik, Szenensynchronisierung, Übergänge und Zusammenarbeit mit Trendo Musik. | `14-media-vfx` | Cue-Liste, Timing, Audiodateien |
| 16 | **Dubbing & Multilingual Studio** | Deutsche, italienische und englische Untertitel, Transkripte und Synchronfassungen; Stimmen nur mit erforderlichen Rechten. | `14-media-vfx` | SRT/VTT und autorisierte Sprachspuren |
| 17 | **Motion Graphics & Titles** | Animierte Titel, Abspann, Logos, Infografiken, Partikelgrafiken und visuelle Übergänge. | `14-media-vfx` | Editierbare Titel-/Animationsvorlagen |
| 18 | **Ultimate Rendering & Export** | Render-Warteschlange, Bildsequenzen, 4K/8K je nach Hardware, HDR, ProRes sofern verfügbar, EXR und Streaming-Exporte. | `14-media-vfx` | Container, Codecinfo, Log, Hash |
| 19 | **Autonomous Film Production** | Claude koordiniert Aufgaben zwischen Film, Blender, Unreal, Audio, 3D und Renderdiensten. | `08-agent-workflow` | Versionierter Workflow mit Freigabepunkten |
| 20 | **Film Quality & Release Director** | Bild-/Ton-Synchronität, fehlende Medien, Schwarzbilder, Codec-/Lizenzprüfung, Versionskontrolle und Freigabe. | `19-qa-verification` | QC-Report und Review-Freigabe |

## Technische Grenzen und QA-Trigger
- `CORE-08`, `MainWindow.VideoSchnitt.cs`, `VideoEdit` zuerst prüfen. OpenTimelineIO, FFmpeg, MLT, OpenColorIO nur bei realer/erlaubter Verfügbarkeit.
- Keine generativen Film-Inhalte ohne Herkunft/Model-Lizenz; Schauspieler-Stimme/Gesicht nur mit Zustimmung; VFX-Export reproduzierbar.
- Unity nur für Cinematics/Timeline-Kamera als gewähltes Ziel; ansonsten native/Blender/Unreal-Pipeline.
- Negativtest: fehlendes Footage, unpassende Framerates, Farbmanagementfehler, nicht synchroner Ton, Codec nicht lizenziert, Streaming ohne Freigabe.

## Spezifische Testfälle
Zu allen 20 Verbesserungen stehen in `qa/TESTFAELLE.csv` jeweils ein überprüfbarer Positiv- und Negativfall (insgesamt 40). Die Tests sind **nicht** durchgeführt; Claude muss automatisierte Testprogramme nach Implementierung hinterlegen.

## Ergebnisbericht
Jede Teilaufgabe erzeugt: veränderte echte Projektdatei, Vorschaubild oder Audio, Build/Test-Log mit Exitcode, Performance-/Lizenzbericht, Git-Diff, verbleibende Einschränkungen. Kein TESTED ohne Beleg.
