# Drittkomponenten (THIRD_PARTY)

Quelle: `src/Trendo.Core/DependencyCatalog` (Installation bei Bedarf beim Öffnen eines Studios nach einmaliger Frage; Übersicht in der App unter „Komponenten“ mit Lizenzen angezeigt; einmalige Zustimmung vor jeder Auto-Installation). Erzeugt aus dem Code; Lizenzen laut Hersteller/winget-Manifest (Stand 21.10.2026).

| Komponente | Lizenz | Auslieferung | winget-ID | Begründung |
|---|---|---|---|---|
| .NET 10 Runtime | MIT | Im Setup enthalten | — | Self-contained im Setup enthalten |
| Avalonia UI + SkiaSharp + HarfBuzzSharp | MIT / BSD-3-Clause | Im Setup enthalten | — | NuGet-Bibliotheken, frei weitergebbar |
| Schrift Inter | SIL OFL 1.1 | Im Setup enthalten | — | Avalonia.Fonts.Inter, frei weitergebbar |
| Velopack | MIT | Im Setup enthalten | — | Bestandteil von Setup.exe |
| YamlDotNet | MIT | Im Setup enthalten | — | NuGet-Bibliothek |
| Hintergrund (KI-generiert) + Trendo-Logo (eigener Entwurf) | Eigenes Projekt-Asset (KI-generiert) | Im Setup enthalten | — | Für Trendo OS erstellt |
| Microsoft Edge WebView2 | Microsoft | Nicht erforderlich | — | Trendo OS nutzt keine Web-Ansichten; nicht erforderlich |
| Ollama (lokale KI-Laufzeit) | MIT | Auto-Installation pro Benutzer (ohne Admin, winget --scope user / ollama pull) | Ollama.Ollama | Installer ~1 GB → nicht ins Setup gepackt; pro Benutzer ohne Admin installierbar |
| KI-Modell Qwen2.5 0.5B (qwen2.5:0.5b) | Apache-2.0 | Auto-Installation pro Benutzer (ohne Admin, winget --scope user / ollama pull) | — | Gewichte ~400 MB, über Ollama geladen |
| KI-Modell Qwen2.5 3B (qwen2.5:3b, optional) | Qwen Research License (frei für nicht-kommerzielle Nutzung; kommerziell gesonderte Lizenz nötig) | Auto pro Benutzer nach Zustimmung (ollama pull) | — | ~1,9 GB; Standard ab 16 GB RAM, wenn geladen |
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
- Hintergrund v2 (`Assets/wallpaper.jpg`): KI-generiert für Trendo OS mit dem früheren Entwicklungswerkzeug (21.10.2026), keine Fremdfotos; wird durch eine mit Claude erstellte Fassung ersetzt.
