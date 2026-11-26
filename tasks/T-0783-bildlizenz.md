# T-0783: Studio-Bilder ohne SDXL Turbo (Bildlizenz)

**Ziel:** Alle Studio-Bilder, die mit SDXL Turbo entstanden, lokal mit einem frei lizenzierten Modell (FLUX.2 [klein] 4B, Apache-2.0) neu erzeugen – Verkauf ohne Registrierung, Umsatzgrenze und Hinweis „Powered by Stability AI“ (Entscheidung des Nutzers vom 26.11.2026, Etappe 0.9.113).
**Branch:** claude/T-0783-bildlizenz (Basis main 184fcbbfc)
**Dateien/Bereich:** `src/Trendo.App/Assets/studios/*.jpg` (nur die SDXL-Bilder), `tools/studio_bilder.py`, `docs/design/STUDIOBILDER.json`, `THIRD_PARTY.md`, `tests/Trendo.App.Tests/StudioImagesTests.cs`, `reports/T-0783/`
**Fertig, wenn:**
- [x] Lizenz an der Originalquelle geprüft (Modellkarte Hugging Face, Datum, SHA-256 der Modelldateien)
- [x] Liste der SDXL-Bilder mit Beleg je Bild (Git, Berichte, ComfyUI-Ausgabeordner)
- [x] Bilder lokal mit ComfyUI + FLUX.2 [klein] 4B neu erzeugt, jedes angesehen, Prompt/Format wie bisher
- [x] Generator: nur noch FLUX.2 [klein] 4B, Prüfsummen, Herkunft je Bild; Lizenzabschnitt in THIRD_PARTY.md
- [x] Tests StudioBilder (neu: Herkunft/Lizenz, Größe/Metadaten) und berührte Klassen grün
**Nicht tun:** Bild-KI im Produkt („Bilder mit KI“, sd-cli) umbauen; fremde Prozesse beenden; etwas installieren.
**Am Ende:** Branch pushen, Bericht `tasks/T-0783-bildlizenz.bericht.md`.
