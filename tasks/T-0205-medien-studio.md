# T-0205: Videos & Fotos bearbeiten – sehen, was ich schneide

**Ausführung:** Box-Agent (nicht Claude Code). Windows-Nachweise am PC über den Integrator.

**Ziel:** Ich wähle ein Video per Dialog, sehe es, setze Anfang und Ende auf einer Zeitleiste und bekomme das Ergebnis.
**Branch:** box/T-0205-medien-studio
**Dateien/Bereich:** `MainWindow.W4Media.cs`, `src/Trendo.Core/MediaStudio.cs`, Tests.
**Befund:** Datei nur per eingetipptem Pfad; „Übernehmen“ ohne Wirkung bei leerem Feld; Ausschnitt nur über Sekundenzahl ohne Vorschau; keine Bildbearbeitung außer Verkleinern.
**Fertig, wenn:**
- [ ] Datei per Dialog/Drag & Drop; Vorschaubild und Länge werden angezeigt.
- [ ] Zeitleiste mit Start/Ende-Markern und Vorschaubildern; Ergebnis abspielbar.
- [ ] Bilder: zuschneiden, drehen, Helligkeit/Kontrast, Text einfügen, als JPG/PNG speichern.
- [ ] Mehrere Clips aneinanderhängen.
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** FFmpeg nicht ungefragt installieren.
**Am Ende:** Branch pushen, Bericht. PR über den üblichen Ablauf (kleiner PR, main grün).
