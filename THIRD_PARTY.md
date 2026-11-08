# Drittkomponenten (THIRD_PARTY)

Quelle: `src/Trendo.Core/DependencyCatalog` (Installation bei Bedarf beim Öffnen eines Studios nach einmaliger Frage; Übersicht in der App unter „Komponenten“ mit Lizenzen angezeigt; einmalige Zustimmung vor jeder Auto-Installation). Erzeugt aus dem Code; Lizenzen laut Hersteller/winget-Manifest (Stand 21.10.2026).

| Komponente | Lizenz | Auslieferung | winget-ID | Begründung |
|---|---|---|---|---|
| .NET 10 Runtime | MIT | Im Setup enthalten | — | Self-contained im Setup enthalten |
| Avalonia UI + SkiaSharp + HarfBuzzSharp | MIT / BSD-3-Clause | Im Setup enthalten | — | NuGet-Bibliotheken, frei weitergebbar |
| Schrift Inter | SIL OFL 1.1 | Im Setup enthalten | — | Avalonia.Fonts.Inter, frei weitergebbar |
| Velopack | MIT | Im Setup enthalten | — | Bestandteil von Setup.exe |
| YamlDotNet | MIT | Im Setup enthalten | — | NuGet-Bibliothek |
| MQTTnet 5.2 | MIT | Im Setup enthalten | — | NuGet-Bibliothek – Live-Messwerte im Studio „Geräte überwachen“ (nur Abonnieren) |
| ZUGFeRD-csharp 18.0.1 | Apache-2.0 | Im Setup enthalten | — | NuGet-Bibliothek – E-Rechnung (XRechnung, CII) im Studio „Geld & Rechnungen“; Hersteller beendet die Pflege am 30.11.2026, die Version bleibt Apache-2.0 (Nachfolger ist kommerziell und wird nicht genutzt) |
| Ical.Net 5.2.4 (mit NodaTime 3.2) | MIT / Apache-2.0 | Im Setup enthalten | — | NuGet-Bibliothek – fremde Kalenderdateien mit Wiederholungsregeln, Ausnahmen und Zeitzonen im Kalender einlesen |
| Microsoft.Data.Sqlite (mit SQLitePCLRaw/e_sqlite3) | MIT, Apache-2.0, SQLite gemeinfrei | Im Setup enthalten | — | NuGet-Bibliothek – Offline-Kartenpakete (MBTiles) lesen |
| Hintergrund (KI-generiert) + Trendo-Logo (eigener Entwurf) | Eigenes Projekt-Asset (KI-generiert) | Im Setup enthalten | — | Für Trendo OS erstellt |
| Microsoft Edge WebView2 | Microsoft | Nicht erforderlich | — | Trendo OS nutzt keine Web-Ansichten; nicht erforderlich |
| Ollama (lokale KI-Laufzeit) | MIT | Auto-Installation pro Benutzer (ohne Admin, winget --scope user / ollama pull) | Ollama.Ollama | Installer ~1 GB → nicht ins Setup gepackt; pro Benutzer ohne Admin installierbar |
| KI-Modell Qwen2.5 0.5B (qwen2.5:0.5b) | Apache-2.0 | Auto-Installation pro Benutzer (ohne Admin, winget --scope user / ollama pull) | — | Gewichte ~400 MB, über Ollama geladen |
| KI-Modell Qwen2.5 3B (qwen2.5:3b, optional) | Qwen Research License (frei für nicht-kommerzielle Nutzung; kommerziell gesonderte Lizenz nötig) | Auto pro Benutzer nach Zustimmung (ollama pull) | — | ~1,9 GB; Standard ab 16 GB RAM, wenn geladen |
| GitHub CLI | MIT | Auto-Installation pro Benutzer nach Zustimmung (winget --scope user); Anmeldung je Person im Browser | GitHub.cli | Pro Benutzer installierbar |
| Git für Windows | GPL-2.0 | Auto-Installation pro Benutzer (ohne Admin, winget --scope user / ollama pull) | Git.Git | Pro Benutzer installierbar |
| Python 3.13 | PSF-2.0 | Auto-Installation pro Benutzer (ohne Admin, winget --scope user / ollama pull) | Python.Python.3.13 | Pro Benutzer installierbar |
| mpv Player | GPL-2.0-or-later | Auto-Installation pro Benutzer (ohne Admin, winget --scope user / ollama pull) | shinchiro.mpv | Pro Benutzer installierbar |
| Android Platform-Tools (adb) | Apache-2.0 | Auto pro Benutzer nach Zustimmung (winget, portables Paket, ohne --scope user) | Google.PlatformTools | Handy per Kabel oder WLAN. Unter Linux das Paket „adb“. Keine Treiber |
| scrcpy | Apache-2.0 | Auto pro Benutzer nach Zustimmung (winget, portables Paket, ohne --scope user) | Genymobile.scrcpy | Bildschirm zeigen. Unter Linux das Paket „scrcpy“. Keine Treiber |
| LibreOffice | MPL-2.0 | Auto-Installation nach Zustimmung, Windows-UAC nötig (nur systemweiter Herstellerinstaller) | TheDocumentFoundation.LibreOffice | Nur systemweiter MSI-Installer – Windows fragt nach Admin |
| Blender | GPL-3.0-or-later | Auto-Installation nach Zustimmung, Windows-UAC nötig (nur systemweiter Herstellerinstaller) | BlenderFoundation.Blender | Nur systemweiter Installer – Windows fragt nach Admin |
| COLMAP | BSD-3-Clause | Auto pro Benutzer nach Zustimmung (offizielles Windows-ZIP, Prüfsumme fest, ohne Admin) | — | Fotogrammetrie. Kein winget-Paket. ZIP 4.2.1 ohne CUDA, nur im Benutzerordner |
| Meshroom | MPL-2.0 | Nur geführt: offizieller Herstellerweg | — | Fotogrammetrie-Alternative. Kein zuverlässiges winget-Paket |
| Krita | GPL-3.0 | Auto-Installation nach Zustimmung, Windows-UAC nötig (nur systemweiter Herstellerinstaller) | KDE.Krita | Systemweiter Installer |
| Inkscape | GPL-2.0 | Auto-Installation nach Zustimmung, Windows-UAC nötig (nur systemweiter Herstellerinstaller) | Inkscape.Inkscape | Systemweiter Installer |
| Kdenlive | GPL-3.0 | Auto-Installation pro Benutzer (ohne Admin, winget --scope user / ollama pull) | KDE.Kdenlive | Pro Benutzer installierbar |
| Audacity | GPL-3.0-or-later | Auto-Installation nach Zustimmung, Windows-UAC nötig (nur systemweiter Herstellerinstaller) | Audacity.Audacity | Systemweiter Installer |
| Tesseract OCR | Apache-2.0 | Auto-Installation nach Zustimmung, Windows-UAC nötig (nur systemweiter Herstellerinstaller) | UB-Mannheim.TesseractOCR | Systemweiter Installer |
| Unreal Engine | Epic EULA (nicht weitergebbar) | Nur geführt: offizieller Herstellerweg | — | Nur über Epic Games Launcher mit Epic-Konto erlaubt – keine automatische Installation |
| Google Chrome (optional) | Google-Lizenz (proprietär) | Nur geführt: offizieller Herstellerweg | — | Proprietär – nur offizieller Download |
| Claude Code | Konto/Berechtigung erforderlich | Nur geführt: offizieller Herstellerweg | — | Kontogebunden – nur offizieller Zugang |
| Tor Browser | BSD-3-Clause u. a. | Nur geführt: offizieller Herstellerweg | — | Sicherheits-QA ausstehend – nur offizielle Quelle |
| Chromium | BSD-3-Clause | Nicht erforderlich | — | Windows: Microsoft Edge ist vorhanden; in der Linux-Edition enthalten |
| Claude Code | Kontotarif | Nicht erforderlich | — | Optionaler Cloudbereich, nicht Teil des OS-Bundles |
| f3d 4.0.1 | BSD-3-Clause | Auto-Installation pro Benutzer nach Zustimmung (offizielle Zip, SHA-256 geprüft) | — | Vorschaubilder für 3D-Dateien, eigener Prozess |
| assimp 6.0.5 | BSD-3-Clause | Auto-Installation pro Benutzer nach Zustimmung (offizielle Zip, SHA-256 geprüft) | — | 3D-Dateien umwandeln, läuft in eigenem Trendo-Prozess (--assimp-convert) |
| glTF-Validator 2.0.0-dev.3.10 (Khronos) | Apache-2.0 | Auto-Installation pro Benutzer nach Zustimmung (offizielle Zip, SHA-256 geprüft) | — | 3D-Modelle offiziell prüfen (VR & AR) |
| glTF-Transform 4.5.1 | MIT | Nach Klick per npm in den Werkzeugordner (feste Fassung, ohne Installationsskripte) | — | Modelle für Web & Brille packen (meshopt, WebP); Start mit node direkt |
| axe-core 4.14.0 + jsdom 30.1.3 | MPL-2.0 (axe-core, unverändert) / MIT (jsdom) | Nach Klick per npm in den Werkzeugordner (feste Fassungen, ohne Installationsskripte) | — | „Webseite bauen“: Barrierefreiheit genau prüfen; eigener Prozess, Seiten-Skripte laufen nicht, nichts geht ins Netz |
| FreeSewing 4.10.2 (core, models, plugin-theme, 11 Schnitte) | MIT | Nach Klick per npm in den Werkzeugordner (feste Fassung, ohne Installationsskripte) | — | „Kleidung & Produkte“: Schnittmuster nach Körpermaßen als SVG 1:1; eigener Node-Prozess mit Trendos Startskript |
| yara-x 1.22.0 (VirusTotal) + YARA-Regeln von ReversingLabs (Stand e0a0be54) | BSD-3-Clause / MIT | Nach Klick: offizieller Download mit fester SHA-256 in den Werkzeugordner | — | „System auf Gefahren prüfen“: Dateien und Ordner auf Merkmale bekannter Schadsoftware prüfen; nur lesen |
| restic 0.19.1 | BSD-2-Clause | Nach Klick: offizieller Download mit fester SHA-256 in den Werkzeugordner | — | „Daten schützen“: verschlüsselte, entdoppelte Sicherungen mit Versionen; Passwort nur im Prozess, nie gespeichert |
| typst 0.15.1 | Apache-2.0 | Auto-Installation pro Benutzer nach Zustimmung (offizielle Zip, SHA-256 geprüft) | — | Briefe/Rechnungen als PDF, eigener Prozess, Dateizugriff auf den Projektordner begrenzt |
| mediamtx 1.21.3 | MIT | Auto-Installation pro Benutzer nach Zustimmung (offizielle Zip, SHA-256 geprüft) | — | Eigener Sendeserver, Standard nur 127.0.0.1 |
| streamlink 8.6.3 (Windows-Build mit Python und FFmpeg) | BSD-2-Clause; FFmpeg LGPL/GPL im Paket | Auto-Installation pro Benutzer nach Zustimmung (offizielle Zip, SHA-256 geprüft) | — | Nur Livestreams von Das Erste/ARD und arte, kein Login, keine Aufnahme; getrenntes Programm, nicht mitgeliefert |

Hinweise:
- Unreal Engine (Epic EULA), Google Chrome, Claude Code, Tor Browser: keine automatische Installation – rechtlich bzw. aus Sicherheits-/Kontogründen nur offizieller Weg.
- Systemweite Installer (MSI/NSIS) von LibreOffice, Blender, Krita, Inkscape, Audacity, Tesseract: automatisch per winget, aber Windows fragt nach Administratorrechten; bereits installierte Programme werden nur erkannt, nie verändert.
- Studio-Kacheln zeigen „Bereit“ nur, wenn Programm real erkannt UND Studio-Adapter implementiert und getestet ist.
- Weitergabe der NuGet-Bibliotheken (MIT/BSD/OFL) im Setup ist durch die Lizenzen gedeckt; Lizenztexte siehe jeweilige Projekte.

## Stand Test-PC (21.10.2026)
- Ollama 0.40.7 + qwen2.5:0.5b: installiert pro Benutzer, getestet (siehe reports/p1/ki).
- Python 3.13.15: installiert pro Benutzer, getestet (siehe reports/p1/deps).

- Material Design Icons (@mdi/js 7.4.47, Apache-2.0): Symbolpfade in `src/Trendo.App/MdiIcons.cs` (im Setup enthalten).
- Fluent UI System Icons (@fluentui/svg-icons 1.1.344, MIT, © Microsoft Corporation): Studio-Symbole in `src/Trendo.App/StudioIcons.cs` (Pfade eingebettet, im Setup enthalten). https://github.com/microsoft/fluentui-system-icons
- Hintergründe (`Assets/wallpaper.jpg`, `Assets/wallpapers/*.jpg`): am 24.10.2026 lokal mit KI erzeugt (ComfyUI + FLUX.2 klein 4B, kein Cloud-Dienst) eigens für Trendo OS, keine Fremdfotos; Anweisung, Startwert und Modell je Bild in `docs/design/HINTERGRUENDE.json`.
- Leisten-Symbole (`Assets/dock/*.png`): am 03.11.2026 lokal mit KI erzeugt (ComfyUI + FLUX.2 klein 4B, kein Cloud-Dienst) eigens für Trendo OS, ohne Schrift und ohne Logos; Anweisungen und Startwerte in `tools/dock_bilder.py`.
- App-Symbole von WhatsApp, Telegram, TikTok, Instagram und Facebook liegen **nicht** in Trendo: Trendo holt nach dem Hinzufügen einer App das offizielle Symbol von der eigenen Seite des Dienstes (wie ein Browser bei installierten Web-Apps) und speichert es nur im Datenordner der Person; die Marken gehören den jeweiligen Firmen, die Symbole werden nicht verändert.
- UltraGameStudio (MIT, © 2026 wellingfeng, https://github.com/wellingfeng/UltraGameStudio, Stand 2547ea8): Gliederung der Spiele-Fachrollen, Rat-Modus, Ablauf-Strategien, Bild-Prüfschleife, Sprite-Raster und Befehle als Vorlage für src/Trendo.Core/Programs/GameExperts.cs und GameHarness.cs; Texte und Code für Trendo neu auf Deutsch geschrieben, kein Fremdcode und keine Fremdbilder übernommen. Nicht übernommen: Gratis-Kanal-/API-Weiterleitung.
- Web Design Studio (Editor-Bündel `src/Trendo.App/Assets/webdesign`, im Setup enthalten): Puck `@puckeditor/core` 0.24.0 (MIT), React 19.1 (MIT), React Three Fiber 9.8.1 (MIT), three.js 0.180 (MIT) und 74 weitere freizügig lizenzierte npm-Pakete (MIT/ISC/Apache-2.0/BSD); vollständige Liste `LIZENZEN.md`, alle Lizenztexte `LIZENZEN.txt` (wird in jede exportierte Seite mitkopiert). Quellen und Bau: `tools/webdesign-editor`. Nicht übernommen: Webstudio und Theatre-Studio (AGPL-3.0), GrapesJS Studio SDK (kommerziell).
- FSRS-Planer für Karteikarten (`src/Trendo.Core/W6/Fsrs.cs`): Algorithmus FSRS-5 und Standardgewichte von Open Spaced Repetition (MIT, © 2022 Open Spaced Repetition, https://github.com/open-spaced-repetition/py-fsrs); Formeln in C# neu geschrieben, kein Fremdcode kopiert. Nicht übernommen: Anki (AGPL-3.0).
- Noten erkennen (`src/Trendo.App/Assets/ki/trendo_noten.py`): Spotify basic-pitch 0.4.0 (Apache-2.0, https://github.com/spotify/basic-pitch) mit dem mitgelieferten ONNX-Modell; nicht mitgeliefert, nach Zustimmung in eigener Python-Umgebung eingerichtet.
- IFC-Austausch (`src/Trendo.App/Assets/ki/trendo_ifc.py`): IfcOpenShell 0.9.0.post1 (LGPL-3.0, https://github.com/IfcOpenShell/IfcOpenShell) als eigener Python-Prozess; nicht mitgeliefert, nach Zustimmung in eigener Umgebung eingerichtet, nicht in Trendo eingebunden.
- Scan-Netze (`src/Trendo.App/Assets/ki/trendo_open3d.py`): Open3D 0.20.0 (MIT, https://github.com/isl-org/Open3D) als eigener Python-Prozess; nicht mitgeliefert, nach Zustimmung in eigener Umgebung eingerichtet.
- Objekte erkennen (`src/Trendo.App/Assets/ki/trendo_objekte.py`): YOLOX-S (Megvii, Apache-2.0, https://github.com/Megvii-BaseDetection/YOLOX) als ONNX-Modell aus dem offiziellen Release 0.1.1rc0 (feste SHA-256), mit onnxruntime 1.31.1 (MIT), numpy 2.2.6 (BSD-3-Clause) und Pillow 12.3.0 (MIT-CMU) auf dem Prozessor; nicht mitgeliefert, nach Zustimmung in eigener Python-Umgebung eingerichtet. Ultralytics-YOLO (AGPL-3.0) wird bewusst nicht verwendet.
- Dokumente umwandeln (`src/Trendo.App/Assets/ki/trendo_markdown.py`): markitdown 0.1.9 (Microsoft, MIT, https://github.com/microsoft/markitdown) mit den Teilen für PDF, Word, PowerPoint und Excel als eigener Python-Prozess, ohne Plugins, nur lokale Dateien; nicht mitgeliefert, nach Zustimmung in eigener Umgebung eingerichtet.
- PMTiles-Leser (`src/Trendo.Core/Karten/PmTiles.cs`): eigener Code nach der offenen PMTiles-v3-Spezifikation von Protomaps (BSD-3-Clause, https://github.com/protomaps/PMTiles); go-pmtiles nur zum Prüfen in Tests, nicht mitgeliefert.
- Karten-Studio, Vektorkarten (`src/Trendo.App/Assets/karten`, im Setup enthalten): MapLibre GL JS 6.17.0 (BSD-3-Clause), `@protomaps/basemaps` 5.7.2 (BSD-3-Clause) und 22 weitere freizügig lizenzierte npm-Pakete; Glyphen Noto Sans (SIL OFL 1.1) und Kartensymbole (MIT, nach tangrams/icons) aus protomaps/basemaps-assets; vollständige Liste `LIZENZEN.md`, Texte `LIZENZEN.txt`. Quellen und Bau: `tools/karten-vektor`. Kartendaten bringt der Nutzer mit (z. B. OpenStreetMap, ODbL).
- Wohnung planen, IFC-Ansicht (`src/Trendo.App/Assets/ifc`, im Setup enthalten): web-ifc 0.0.78 (ThatOpen, MPL-2.0, unverändert eingebunden; Quelltext https://github.com/ThatOpen/engine_web-ifc) und three.js 0.180.0 (MIT); Lizenztexte `LIZENZEN.txt`. Quellen und Bau: `tools/ifc-ansicht`.
- Freie Bilder & Klänge, Reiter „Symbole“: Daten der Iconify-Symbolsätze (https://github.com/iconify/icon-sets, fester Stand 3eb33380) werden erst auf Klick geladen und nur gelesen; Lizenz je Satz aus `collections.json` – angeboten werden nur freie Sätze (MIT, Apache-2.0, ISC, BSD, Unlicense, CC0, OFL; CC BY mit Namensnennung in „Lizenzen.txt“). Nicht-kommerzielle, „share-alike“-, GPL- und MPL-Sätze sind ausgeblendet. Nichts davon ist im Setup enthalten.
- Geräte erkennen: PCI-Namensliste `pci.ids` (PCI ID Project, https://github.com/pciutils/pciids, fester Stand 32bce8c6 mit SHA-256; Doppellizenz GPL-2.0+/BSD-3-Clause, hier unter BSD-3-Clause genutzt); wird erst auf Klick geladen, nicht im Setup enthalten.
- Dateien teilen, „Ans Handy senden“: eigener Code nach der offenen LocalSend-Protokollbeschreibung (https://github.com/localsend/protocol); kein fremder Code in Trendo. Die App LocalSend (Apache-2.0) läuft auf dem Handy und wird nicht mitgeliefert.
