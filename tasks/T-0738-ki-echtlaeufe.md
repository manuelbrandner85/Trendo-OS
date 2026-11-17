# T-0738: Echtläufe – Bilder mit KI und Trendo Musik

**Ziel:** Für STUDIO-IMAGEAI-01 und STUDIO-MUSIK-01 (Restliste d7, Rang 10 und 11) einen echten Lauf aus der App mit den Werkzeugen dieses PCs belegen – oder ehrlich BLOCKIERT melden, wenn etwas fehlt.
**Branch:** claude/T-0738-ki-echtlaeufe-2 (ersetzt claude/T-0738-ki-echtlaeufe)
**Dateien/Bereich:** `src/Trendo.App/MainWindow.KreativStudios2.cs` (Bilder mit KI), `src/Trendo.Core/Kreativ/KreativKits2.cs` (ImageAiKit), Tests `tests/Trendo.App.Tests/T0738KiEchtlaeufeUiTests.cs`, `tests/Trendo.Core.Tests/KreativKits2Tests.cs`, Belege `reports/T-0738/`, gemeinsam nur `.gitattributes` (eine Zeile für die Belege).
**Fertig, wenn:**
- [ ] Vorhandensein geprüft: sd-cli (stable-diffusion.cpp), Bildmodell, Grafikkarte, ACE-Step 1.5 mit Modellen – nichts installiert, nichts geladen.
- [ ] Bilder mit KI: Klick auf „Bilder malen“ im Testfenster mit dem echten Werkzeugordner → PNG im Bildordner des Dokuments; Beleg mit Rohlog (sd-cli-Ausgabe), PNG, SHA-256, Laufzeit, ffprobe.
- [ ] Bilder mit KI ohne Werkzeuge: Meldung nennt Programm (sd-cli.exe) und Modell (SDXL Turbo/SD Turbo) einzeln und führt zur KI-Bibliothek; getestet.
- [ ] Trendo Musik: Klick auf „Erstellen“ mit dem echten ACE-Step → Lied im Projektordner und in der Bibliothek; Beleg mit Rohlog (Server-Ausgabe dieses Laufs), Audiodatei, SHA-256, Dauer laut ffprobe.
- [ ] gefundene echte Fehler im Weg behoben; gezielte Testklassen grün (kein Volltest).
**Nicht tun:** keine Downloads/Installationen, kein Volltest, keine gemeinsamen Dateien außer `.gitattributes`, kein Push nach main.
**Am Ende:** Branch pushen, Bericht `tasks/T-0738-ki-echtlaeufe.bericht.md`, Arbeitskopie löschen.
