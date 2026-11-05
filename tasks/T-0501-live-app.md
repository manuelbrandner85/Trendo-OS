# T-0501: Eigene App „Trendo Live TV" (wie TiviMate, mit mehr Funktionen)

**Ziel:** Live TV als eigenständige Android-TV-App `os.trendo.tv.live` mit eigenem Design im Trendo-Stil (keine Kopie von TiviMate-Oberfläche oder -Marke).
**Branch:** claude/T-0501-live-app (von claude/T-0500-tv-edition)
**Freigaben Manuel 05.11.2026 (alle Ja):** Komfort-Grundlagen, Catch-up & Timeshift, Multi-View, Aufnahmen auf USB, Sprachsteuerung, Heute-Tipps & Sport, Handy als zweiter Bildschirm, Profile & Kinderschutz, Abgleich mit Windows, Direkt ins Live-TV, Ruckelschutz, Trendo integriert.

**Architektur:** Gradle-Module `:shared` (Design, Live-TV-Logik, Player, Handy-Eingabe), `:app` (TRENDO OS), `:live` (Trendo Live TV). Beide Apps gleicher lokaler Signaturschlüssel. Quellen-Übernahme über `LiveExportProvider` mit `protectionLevel=signature`.

**Nicht tun:** keine Zugangsdaten durch Claude eingeben; TiviMate und andere Apps nicht anfassen; keine fremden Logos/Cover ins Repo.

## Phasen
1. [x] Modul-Aufteilung, App-Gerüst, Banner, Übernahme aus Trendo, Trendo verlinkt App (Live-TV-Karte/Seitenleiste)
2. [ ] Komfort-Grundlagen (Sortieren/Umbenennen, eigene Logos, Auto-Aktualisierung, letzter Sender, Verlauf, Ton/Untertitel/Format, Stream-Infos) + Ruckelschutz
3. [ ] Catch-up & Timeshift
4. [ ] Heute-Tipps & Sport, Sprachsteuerung
5. [ ] Profile & Kinderschutz (Zeitlimit)
6. [ ] Multi-View, Aufnahmen auf USB
7. [ ] Handy als zweiter Bildschirm, Abgleich mit Windows, Direkt ins Live-TV
