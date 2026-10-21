# Machbarkeit: Abhängigkeiten der Studios (Stand 21.10.2026)

Methode: `winget show --id <ID> -e --scope user|machine` auf dem Windows-Test-PC (winget 1.30), Lizenz laut winget-Manifest. Eintrag „ja“ = es gibt einen Installer für diesen Scope. Grundsatz: Kernkomponenten im Setup.exe; Studio-Werkzeuge erst beim Öffnen des Studios: erkennen → Funktion prüfen → bei Bedarf **einmal fragen** → automatisch installieren. Vorhandene Programme werden nur integriert (Pfad gespeichert), nie neu installiert oder verändert.

| Werkzeug (Manifest) | Studio | winget-ID | pro Benutzer | systemweit | Lizenz | Entscheidung |
|---|---|---|---|---|---|---|
| Chromium | Browser | Hibbiki.Chromium (inoffizieller Build) | ja | ja | BSD-3 | Windows: nicht nötig (Edge vorhanden); Linux-Edition: enthalten |
| Google Chrome | Browser | Google.Chrome | nein | ja | proprietär | nur geführt (offizieller Download) |
| LibreOffice | Office | TheDocumentFoundation.LibreOffice | nein | ja | MPL-2.0 | Auto-Installation, **UAC** (nur MSI systemweit) |
| mpv | Medien | shinchiro.mpv | – (kein Scope im Manifest) | – | GPL-2.0+ | Auto-Installation ohne Scope-Angabe (Inno, Standard pro Benutzer) |
| VLC (Alternative) | Medien | VideoLAN.VLC | nein | ja | GPL-2.0 | nicht verwendet (mpv reicht) |
| Blender | 3D | BlenderFoundation.Blender | nein | ja | GPL-3.0+ | vorhandenes wird integriert; sonst Auto, **UAC** |
| Krita | Medien | KDE.Krita | nein | ja | GPL-3.0 | Auto, **UAC** |
| Inkscape | Medien | Inkscape.Inkscape | nein | ja | GPL-2.0 | Auto, **UAC** |
| Kdenlive | Medien | KDE.Kdenlive | ja | ja | GPL-3.0 | Auto pro Benutzer, ohne Admin |
| Audacity | Medien | Audacity.Audacity | – | – | GPL-3.0+ | Auto, **UAC** (WiX) |
| Tesseract OCR | Letter | UB-Mannheim.TesseractOCR | – | – | Apache-2.0 | Auto, **UAC** (NSIS) |
| Git | Web & App | Git.Git | ja | ja | GPL-2.0 | Auto pro Benutzer (auf Test-PC bereits vorhanden → integriert) |
| Python | Web & App | Python.Python.3.13 | ja | ja | PSF-2.0 | Auto pro Benutzer, ohne Admin |
| Ollama + Qwen2.5 0.5B | KI | Ollama.Ollama + `ollama pull` | ja | nein | MIT / Apache-2.0 | Auto pro Benutzer, ohne Admin (lokale KI, kostenlos) |
| Grok Build | Agent | – | – | – | kontogebunden | nur offizieller Zugang |
| Unreal Engine | Game | – (nur Epic Launcher: EpicGames.EpicGamesLauncher, systemweit) | nein | Launcher ja | Epic EULA | **nur geführt**: Engine-Download nur über Epic-Konto im Launcher erlaubt |
| Tor Browser | Recherche | TorProject.TorBrowser | ja | nein | BSD-3 u. a. | technisch möglich, aber geführt bis Sicherheits-QA |
| Grok Bot | Cloud | – | – | – | Kontotarif | nicht Teil des OS |

## API-Schlüssel
| Dienst | Pflicht? | Automatisch anlegbar? | Umsetzung |
|---|---|---|---|
| xAI API | nein (optional, kostenpflichtig) | nein – Konto, Nutzungsbedingungen, Zahlung | „Schlüssel holen“ öffnet console.x.ai, Einfügen, Live-Prüfung `GET /v1/models`, Speicherung im Credential Manager |
| GitHub | nein | teilweise – offizieller OAuth Device Flow existiert (noch nicht eingebaut) | „Schlüssel holen“ (Token-Seite) + Live-Prüfung `GET /user` |
Lokale KI benötigt keinen Schlüssel. Vollautomatisches Anlegen von Schlüsseln ist nicht möglich bzw. nicht erlaubt (Registrierung, AGB, 2FA erfordern den Nutzer).
