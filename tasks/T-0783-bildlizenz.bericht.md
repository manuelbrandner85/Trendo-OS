# T-0783 Bericht: Studio-Bilder ohne SDXL Turbo

Zweig `claude/T-0783-bildlizenz` (Basis main `184fcbbfc`). Beleg mit Rohdaten und SHA-256: `reports/T-0783/README.md`.

## Was war – was ist jetzt

| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| Lizenz | 14 Studio-Bilder aus SDXL Turbo (Stability AI Community License: Registrierung, Umsatzgrenze, Hinweispflicht) | alle 85 Studio-Bilder (83 main + 2 aus T-0782) aus FLUX.2 [klein] 4B, **Apache-2.0** | Modellkarte + `LICENSE.md` + API-Rohdaten von Hugging Face vom 26.11.2026 in `reports/T-0783/`; 9B-Variante ist FLUX Non-Commercial und wird nicht benutzt |
| Modelldateien | – | genau die Apache-Dateien | SHA-256 auf dem PC = Hugging-Face-LFS (`flux-2-klein-4b` `ec3d4e73…`, `qwen_3_4b` `6c671498…`, `flux2-vae` `868fe7b3…`), vom Generator mit `--modelle` geprüft (echt) |
| Betroffene Bilder | Commit `cce5c6261` (8), T-0611/`676ea9466` (4), T-0782-Zweig (2) | ersetzt | Git/Berichte + Forensik: 71 übrige Bilder gleichen pixelgenau einer ComfyUI-Ausgabe mit `unet_name=flux-2-klein-4b`, genau diese 14 nicht (`reports/T-0783/erzeugung.log`) |
| Erzeugung | sd-cli + SDXL Turbo (ComfyUI galt als „nicht installiert“) | ComfyUI 0.41.0 lokal (`Studio-Werkzeuge\GAMEFORGE_GODMODE_v2_0\engines\ComfyUI_windows_portable`), eigener Prozess auf 127.0.0.1:8191, danach per PID beendet | `erzeugung.log` (Laufzeiten 8–16 s je Bild, RTX 5070) – echt |
| Generator `tools/studio_bilder.py` | FLUX war schon Standard, SDXL lief daneben von Hand | einziger Weg FLUX.2 [klein] 4B; Modellnamen zentral in `MODELL`, Sperrliste (SDXL/SD Turbo/klein 9B), Prüfung der Modelle in ComfyUI und optional der SHA-256, nur 127.0.0.1, `--host`, Herkunft je Bild in `docs/design/STUDIOBILDER.json`, JPG ohne Metadaten (`-map_metadata -1`, bitexact); Prompts für TUTOR/CODELEARN aus T-0782 übernommen (gleiche Zeilen, gleiche Stelle) | Test `Former_sdxl_images_are_flux_apache` |
| Lizenznachweis | kein Eintrag für Studio-Bilder | Abschnitt „Studio-Bilder“ in `THIRD_PARTY.md` mit Modell, Lizenz, Quelle, Datum, Prüfsummen, Liste der 14 ersetzten Bilder | Test prüft Abschnitt und jede Kennung |
| Sichtprüfung | – | jedes Bild angesehen; 6 Bilder (VIDEOAI, CODEAI, CODELEARN, TRAIN, PHOTOS, DAYPLAN) wegen Marken-Anmutung (Monitor-Schriftzug, Laptop-Plakette, Kamera-Typenschild, Startleiste) mit neuem Startwert nachgewürfelt | Startwerte in `STUDIOBILDER.json` |

Ersetzte Bilder (vorher/nachher mit SHA-256 in `reports/T-0783/README.md`): STUDIO-BOOK-01, STUDIO-ANIM-01, STUDIO-PHOTOS-01, STUDIO-IMAGEAI-01, STUDIO-VIDEOAI-01, STUDIO-AGENTS-01, STUDIO-MODELS-01, STUDIO-TRAIN-01, STUDIO-CODEAI-01, STUDIO-VISION-01, STUDIO-CITE-01, STUDIO-DAYPLAN-01, STUDIO-TUTOR-01, STUDIO-CODELEARN-01. Format unverändert: JPG 768 × 432.

## Tests

- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- App `StudioBilder`: 3/3 grün (neu: „früher mit SDXL Turbo erzeugte Bilder stammen jetzt aus FLUX.2 [klein] 4B“ – Herkunft mit Prüfsumme, Lizenzabschnitt, Generator ohne sd-cli/SDXL; „jedes JPG 768 × 432, keine Metadaten mit Pfaden/Namen, kein EXIF/XMP“). Rot-Grün: mit dem `STUDIOBILDER.json` von main schlägt der neue Test fehl („STUDIO-BOOK-01: keine Herkunft“).
- App `StudioIconTests` 4/4, `StudiosAlle_Ui` 103/103; Core `Sys13_Qualitaet` + `NoFixedUserTests` 11/11.
- Datenschutz: Grep aus dem Paket-Brief über alle geänderten Dateien leer; JPG-Segmente aller 85 Bilder geprüft (nur Bilddaten, alte Bilder zusätzlich Encoder-Kommentar „Lavc…“, kein EXIF/XMP, keine Pfade).

## Abnahmekriterien

1. Leere Vorlage → Ergebnis über die Oberfläche: NICHT ZUTREFFEND (Bild-Assets, keine Programmfunktion).
2. Fremddatei/Export: NICHT ZUTREFFEND.
3. Prozessabbruch: NICHT ZUTREFFEND.
4. Claude/KI-Fachaufgabe im Dokument: NICHT ZUTREFFEND.
5. Test bis zur echten Datei: VOLLWERTIG für den Auftrag – echte Erzeugung mit ComfyUI/FLUX auf der Grafikkarte, Test prüft die echten Dateien und Prüfsummen (kein Override).
6. Bedienung: NICHT ZUTREFFEND (Oberfläche unverändert; Seite „Alle Studios“ gerendert, zeigt im Standardzustand keine Kartenbilder).

## Nicht geprüft / offen

- Die Bild-KI **im Produkt** („Bilder mit KI“, Musik-Cover, Videos mit KI) nutzt weiter sd-cli mit SDXL Turbo, das die Person selbst über die KI-Bibliothek einrichtet. Ob Trendo das beim Verkauf anbieten darf bzw. auf FLUX.2 [klein] 4B umstellen soll, ist eine eigene Entscheidung (nicht Teil dieses Pakets).
- FLUX hält „no faces“ in Fotos-im-Bild nicht ein: STUDIO-PHOTOS-01 zeigt kleine Porträts erfundener Personen in den Rahmen (keine echte Person, keine Marke). Kleine, unlesbare Plaketten an Monitoren sind geblieben.
- `docs/design/STUDIOBILDER.json` beschreibt für die übrigen Bilder noch den Stand vom 24.10.2026 (älterer Erzeuger `tools/design/ki_studiobilder.py`); Bilder selbst sind FLUX, nur Prompt/Startwert dort veraltet.
- Die neuen PNG-Rohbilder liegen im Ausgabeordner der ComfyUI (außerhalb des Repos), wie bei allen früheren Läufen.
- Zusammenführung mit `claude/T-0782-nachhilfe-programmieren`: beide Zweige ändern `STUDIO-TUTOR-01.jpg` und `STUDIO-CODELEARN-01.jpg` (binär) – beim Merge die Fassung aus T-0783 nehmen. Die Prompt-Zeilen in `tools/studio_bilder.py` sind identisch.

## Berührte Dateien

`tools/studio_bilder.py`, `docs/design/STUDIOBILDER.json`, `THIRD_PARTY.md`, `tests/Trendo.App.Tests/StudioImagesTests.cs`, `src/Trendo.App/Assets/studios/` (14 JPG, davon STUDIO-TUTOR-01/STUDIO-CODELEARN-01 neu auf diesem Zweig), `reports/T-0783/*`, `tasks/T-0783-bildlizenz.md`, `tasks/T-0783-bildlizenz.bericht.md`.
