# T-0204: Spiele-Studio – eigenes Spiel statt drei fester Vorlagen

**Ausführung:** Box-Agent (nicht Claude Code). Windows-Nachweise am PC über den Integrator.

**Ziel:** Ich kann ein eigenes kleines Spiel gestalten, nicht nur eine von drei Vorlagen (Schlange, Mauerbrecher, Hüpfspiel) kopieren.
**Branch:** box/T-0204-spiele-studio
**Dateien/Bereich:** `MainWindow.W4Game.cs`, `src/Trendo.Core/GameStudio.cs`, Tests.
**Befund:** Name verspricht „mit der Unreal Engine“; ohne Unreal gibt es nur 3 feste Browser-Spiele ohne Einstellungen.
**Fertig, wenn:**
- [ ] Ich stelle Spielregeln ein (Tempo, Leben, Punkte zum Sieg, Farben, Spielfigur-Bild aus Datei, Hintergrund, Ton) und sehe die Vorschau sofort im Studio.
- [ ] Ich kann eigene Level als Raster zeichnen (Hüpfspiel/Mauerbrecher).
- [ ] Ich kann das Spiel als Ordner/ZIP zum Weitergeben exportieren.
- [ ] Unreal: Projekt anlegen UND vorhandenes Projekt öffnen; ohne Unreal klarer Hinweis, kein Versprechen im Untertitel.
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Studionamen im Katalog nicht ändern (Vorschlag dazu in den Bericht).
**Am Ende:** Branch pushen, Bericht. PR über den üblichen Ablauf (kleiner PR, main grün).
