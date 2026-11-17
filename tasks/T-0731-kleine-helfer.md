# T-0731: SYS-02 Kleine Helfer & Musik – Musik ohne Zusatzprogramm, Bildschirmfoto mit Bereichswahl

**Ziel:** Musik (MP3/WAV) spielt ohne nachinstalliertes mpv; andere Formate melden ehrlich BLOCKIERT mit Einrichten-Knopf; Bildschirmfoto mit Bereichsauswahl per Maus ohne ffmpeg.
**Branch:** claude/T-0731-kleine-helfer
**Dateien/Bereich:** `src/Trendo.App/MainWindow.MusicPlayer.cs`, `src/Trendo.App/MainWindow.Sys02.cs`, neu `src/Trendo.App/WinAudio.cs`, `src/Trendo.App/ScreenGrab.cs`, `src/Trendo.Core/Media/PcmSource.cs`, Test `tests/Trendo.App.Tests/T0731HelferUiTests.cs` (+ Testton `Daten/ton-440hz-2s.mp3`), Vorschau-Modus in `tools/Trendo.Render/Program.cs`, Belege `reports/T-0731/`.
**Grundlage:** Restliste Rang 1 (`docs/audit/2026-11-16-profi-audit/d7-restliste.md`), audit-1 SYS-02 („Ton nur mit nachinstalliertem mpv … Bildschirmfoto nur mit ffmpeg“).
**Fertig, wenn:**
- [x] Eine echte MP3 spielt ohne mpv hörbar über das Tongerät; Länge, Fortschritt, Pause und Titelende stimmen (Test ohne `AudioOverride`, Beleg unter `reports/T-0731/`).
- [x] Formate, die Trendo nicht selbst spielt (z. B. FLAC), melden „BLOCKIERT: …“ mit „Player einrichten“ statt stiller Fehlfunktion.
- [x] „Bereich wählen“: Trendo klappt sich weg, zeigt den eingefrorenen Desktop, Maus zieht einen Bereich auf, PNG enthält genau diesen Ausschnitt; Esc bricht ab.
- [x] „Ganzen Bildschirm aufnehmen“ ohne ffmpeg unter Windows; echter Beleg: PNG mit Pixeln eines fremden Fensters.
- [x] betroffene Testklassen grün (kein Volltest – macht die Hauptsitzung).
**Nicht tun:** keine neue Bibliothek, kein mpv-Mitliefern, kein Admin/Registry; Video-Wiedergabe im Trendo-Fenster ist nicht Teil dieses Pakets.
**Am Ende:** Branch pushen, Bericht `tasks/T-0731-kleine-helfer.bericht.md`.
