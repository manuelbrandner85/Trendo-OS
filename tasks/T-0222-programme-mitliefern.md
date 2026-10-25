# T-0222: Studio-Programme mit dem Betriebssystem mitliefern

**Auftrag (Manuel, 2026-10-25):** „Alle Studios sollen keine Programme runterladbar sein, sondern … im Betriebssystem integriert ohne nachträgliche Installation – diese werden mit installiert mit dem Betriebssystem.“ Auswahl: **Mitliefern** (portable Fassungen im Setup).
**Branch:** claude/T-02xx
**Bezug:** GRUNDSATZ-01 („mitinstalliert, beim Setup wählbar oder per Klick“) – neu gilt für die Studios nur noch „mitinstalliert“. Das ist ein Decision-Delta und braucht Manuels OK zum Eintrag.

## Programme (Windows-Edition)
| Programm | Studio | Lizenz | Mitliefern | Quelle (offiziell) | ca. Größe gepackt |
|---|---|---|---|---|---:|
| Blender | 3D | GPL-3.0 | ja, Portable-ZIP | blender.org | 400 MB |
| LibreOffice | Office | MPL-2.0 | ja, entpackt aus dem offiziellen Installer | libreoffice.org | 350 MB |
| Krita | Medien – Grafik | GPL-3.0 | ja, Portable-ZIP | krita.org | 250 MB |
| Inkscape | Medien – Vektor | GPL-2.0 | ja, ZIP | inkscape.org | 150 MB |
| Kdenlive | Medien – Video | GPL-3.0 | ja, Standalone | kdenlive.org | 300 MB |
| Audacity | Medien – Audio | GPL-3.0 | ja, ZIP | audacityteam.org | 50 MB |
| OBS Studio | Sendung & Stream | GPL-2.0 | ja, ZIP | obsproject.com | 150 MB |
| FFmpeg | Medien | GPL/LGPL | ja | ffmpeg.org (verlinkte Builds) | 150 MB |
| mpv | Medien – Wiedergabe | GPL-2.0 | ja | mpv.io (verlinkte Builds) | 40 MB |
| SumatraPDF | PDF | GPL-3.0 | ja, Portable | sumatrapdfreader.org | 15 MB |
| qpdf | Office – PDF | Apache-2.0 | ja (heute schon ZIP) | github.com/qpdf | 25 MB |
| 7-Zip | Archive | LGPL-2.1 | ja, Konsolenfassung | 7-zip.org | 2 MB |
| Tesseract OCR | Letter | Apache-2.0 | ja, entpackt | github.com/UB-Mannheim | 100 MB |
| COLMAP | 3D aus Fotos | BSD-3 | ja, ZIP | github.com/colmap | 500 MB |
| Git (PortableGit) | Web & App | GPL-2.0 | ja | git-scm.com | 300 MB |
| Python (embeddable) | Automatisierung | PSF-2.0 | ja | python.org | 30 MB |
| VSCodium statt VS Code | Web & App | MIT | ja (VS Code selbst darf nicht weitergegeben werden) | vscodium.com | 150 MB |
| scrcpy (mit adb) | Handy | Apache-2.0 | ja, ZIP | github.com/Genymobile | 10 MB |
| Ollama + Qwen2.5 0.5B | KI-Assistent | MIT / Apache-2.0 | ja | ollama.com | 1,9 GB |

**Bleibt verlinkt (Lizenz verbietet Weitergabe):** Unreal Engine (Epic-EULA), Google Chrome, Claude Code (Konto), Qwen2.5 3B (Forschungslizenz), Meshroom (5 GB, optional), Tor (Sicherheits-QA offen).

## Aufbau
- Summe ca. **5–6 GB** gepackt, entpackt ca. 12 GB. Eine einzelne Setup.exe kann nicht so groß werden (Windows-Programmdateien sind auf 4 GB begrenzt).
- Darum: **Vollpaket als Ordner/USB/ISO** – `TrendoOS-Setup.exe` plus Ordner `Programme\` mit einem Paket je Programm und SHA-256-Liste. Das Setup richtet alle Programme in einem Schritt mit ein (Fortschrittsanzeige, ohne Internet, ohne Admin, im Benutzerordner `TrendoWerkzeuge`). Danach ist nichts mehr nachzuladen; die Studios starten die Programme direkt.
- Updates der App bleiben klein (Velopack-Delta); Programm-Updates als eigenes Paket.
- Lizenztexte und Quellcode-Hinweise (GPL) liegen bei jedem Programm, Übersicht in `THIRD_PARTY_NOTICES`.
- Linux-Edition: die Programme kommen als Pakete fest ins Image (eigener Auftrag). Pi: dasselbe, ARM-Fassungen.

**Fertig, wenn:**
- [ ] Alle „ja“-Programme liegen im Vollpaket mit SHA-256, Setup richtet sie ohne Internet ein.
- [ ] Jedes Studio findet sein Programm sofort; keine Download-Knöpfe mehr für mitgelieferte Programme.
- [ ] Test: frische Installation aus dem Vollpaket mit getrenntem Netz, jedes Programm startet (Rohlog + SHA-256).
- [ ] alle drei Testprojekte grün

**Nicht tun:** kein Admin, kein winget/msiexec-Installieren, nichts außerhalb des Benutzerordners; kein Release/Upload ohne OK.
