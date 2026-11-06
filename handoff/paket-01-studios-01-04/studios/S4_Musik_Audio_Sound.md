# STUDIO 4 – Musik, Audio & Sound – genehmigte Verbesserungen

Trendo-Katalog: `STUDIO-MUSIK-01 + CORE-08 Audioeditor` · **20 Nutzer-JA** · Stand: **APPROVED_SPEC** (Status der einzelnen Funktionen per Live-Audit verifizieren)

## Bewährte Steuerung
Reihenfolge für jede Funktion: vorhandenes Trendo prüfen → REA-Clean-Room/Native Integration bewerten → Schnittstellen/Dateien/Versionierung definieren → echten vertikalen Slice bauen → positive/negative Tests → Performance/Security/License/QC → Berichte/Revision. Unity-Relevanz je Studio steht in `03_GLOBALE_REGELN_REA_UNITY.md`.

| Nr. | Verbindliche Verbesserung | Konkreter Funktionsumfang | Claude-Agent | Ziel-Artefakt |
|---|---|---|---|---|
| 01 | **Ultimate DAW Studio** | Professionelle Mehrspur-Arbeitsfläche mit Aufnahme, Clips, Timeline, Schnitt, Automationen, Mixer und verlustfreier Bearbeitung. | `14-media-vfx` | Editierbare Session mit Audio-Assets |
| 02 | **AI Music Producer** | Claude entwickelt Songstruktur, Arrangement, Stil, Instrumentierung, Übergänge und Produktionsschritte. | `14-media-vfx` | Arrangement-Plan, Marker, Versionsvergleich |
| 03 | **Next-Gen AI Music Generator** | Lokale KI erzeugt komplette Songs, Instrumentals, Varianten, neue Abschnitte und passende Begleitungen. | `07-ai-local-models` | Audio, Prompts, Seeds, Modellangaben |
| 04 | **Advanced MIDI & Composition** | Piano-Roll, Noteneditor, Akkorde, Tonarten, Harmonien, Quantisierung und MIDI-Instrumente. | `14-media-vfx` | MIDI-Datei und editierbarer Pianoroll |
| 05 | **Virtual Instruments & Synthesizers** | Software-Synthesizer, Sampler, Drumcomputer, Soundfonts und virtuelle Instrumentenbibliotheken. | `14-media-vfx` | Instrumentenpreset und gerenderter Klang |
| 06 | **Professional Plugin Host** | VST3, CLAP und weitere geeignete Pluginformate mit Plugin-Prüfung, Presets und abgesicherter Ausführung. | `14-media-vfx` | Plugin-Scan, Sandbox/Crash-Report |
| 07 | **Ultimate Vocal Studio** | Gesangsaufnahme, Harmonien, Vocal-Layers, Intonation, Ausdruck, Sprachbearbeitung und autorisierte Stimmmodelle. | `14-media-vfx` | Aufnahmesession und Einwilligungsnachweis |
| 08 | **AI Stem Separation** | Gesang, Schlagzeug, Bass, Instrumente und weitere Spuren möglichst sauber voneinander trennen. | `07-ai-local-models` | Synchronisierte Stem-Dateien |
| 09 | **Professional Mixing Console** | EQ, Kompressor, Reverb, Delay, Sidechain, Busse, Sends, Routing und Mischautomation. | `14-media-vfx` | Mixsession und Routing-Test |
| 10 | **AI Mastering Director** | Automatisches und manuelles Mastering mit LUFS, True Peak, Frequenzanalyse und Vergleichsversionen. | `14-media-vfx` | Masterdatei und Messbericht |
| 11 | **Spectral Audio Restoration** | Rauschen, Knacken, Brummen, Störgeräusche und andere Audiofehler spektral untersuchen und bearbeiten. | `14-media-vfx` | Spektrogramm, Original/Resultat |
| 12 | **Time & Pitch Intelligence** | Tempo, Tonhöhe, Timing, Transienten und Gesangsintonation mit möglichst wenigen Artefakten korrigieren. | `14-media-vfx` | Referenz- und Ergebnisclip, Artefaktcheck |
| 13 | **Electronic Music Production** | Trance, Techno, House, EDM und weitere Genres mit Step-Sequencer, Arpeggiator, Groove und Automation. | `14-media-vfx` | Sequencer-Pattern und Mixdown |
| 14 | **Cinematic & Orchestral Scoring** | Orchesterarrangements, Filmmusik, Leitmotive, Timecode-Synchronisierung und emotionale Musikdramaturgie. | `14-media-vfx` | Cue-Sheet, Tempo-Map, Export |
| 15 | **Adaptive Game Music Director** | Musik reagiert auf Spielereignisse, Gefahr, Kämpfe, Exploration, Dialoge und andere Zustände in Unreal/Godot. | `13-unreal-games` | Audio-Event-Map; Unity ebenfalls als Zieladapter |
| 16 | **Spatial & Immersive Audio** | 3D-Raumklang, Positionierung, Mehrkanal-Audio, binaurale Vorschau und geeignete Surround-Exporte. | `14-media-vfx` | Kanalplan, Hörtetest, Engine-Test |
| 17 | **Foley & Sound Effects Creator** | Schritte, Fahrzeuge, Wind, Regen, Maschinen, Umgebungsgeräusche und Soundeffekt-Bibliotheken erstellen und bearbeiten. | `14-media-vfx` | Soundbibliothek und Event-Tags |
| 18 | **Lyrics & Multilingual Song Studio** | Liedtexte, Reimschemata, Silbenrhythmus, Übersetzungen, Karaoke und Sprachversionen auf Deutsch, Italienisch und Englisch. | `14-media-vfx` | Synchronisierter Text und Sprachvarianten |
| 19 | **Audio-to-MIDI & Intelligent Transcription** | Melodien und Rhythmen aus Aufnahmen analysieren, Noten/MIDI schätzen und Sprache transkribieren. | `14-media-vfx` | MIDI/Text plus Unsicherheitsbericht |
| 20 | **Autonomous Audio QA & Export** | Claude prüft Audioqualität, Synchronität, Clips, Lautheit, Dateiformate, Rechte und erstellt reproduzierbare Exporte. | `19-qa-verification` | Messprotokoll, Stems, Audiodatei |

## Technische Grenzen und QA-Trigger
- `STUDIO-MUSIK-01` und Audioeditor `CORE-08` erhalten, nicht ersetzen. ACE-Step/geeignete Modelle nur nach VRAM-/Modell-Lizenzcheck.
- REA: native DAW/Audioeffekte vorrangig, keine Zwangsinstallation großer DAWs. VST3/CLAP-Hosts nur mit Lizenz-, Sandbox- und Kompatibilitätstest.
- Unity-Audio nur als optionaler Import-/AudioMixer-/Eventadapter für Unity-Spiele; keine Unity-Installation nur fürs Mastering.
- Negativtest: Clipping, Plugin-Crash, fehlende GPU, defekte WAV, gesperrter Stimmeinsatz, falsche Kanalzahl, LUFS-Target verfehlt.

## Spezifische Testfälle
Zu allen 20 Verbesserungen stehen in `qa/TESTFAELLE.csv` jeweils ein überprüfbarer Positiv- und Negativfall (insgesamt 40). Die Tests sind **nicht** durchgeführt; Claude muss automatisierte Testprogramme nach Implementierung hinterlegen.

## Ergebnisbericht
Jede Teilaufgabe erzeugt: veränderte echte Projektdatei, Vorschaubild oder Audio, Build/Test-Log mit Exitcode, Performance-/Lizenzbericht, Git-Diff, verbleibende Einschränkungen. Kein TESTED ohne Beleg.
