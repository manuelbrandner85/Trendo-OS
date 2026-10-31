# T-0267 – Sendung & Streaming als echtes Programm „Sendeablauf“

- Freigabe: Manuel im Chat am 31.10.2026 („alles Offene in jedem Studio umsetzen“, Masterprompt GODMODE 131)
- Vorbilder (Funktionsumfang, öffentlich dokumentiert): Rundown-Planer im Rundfunk (Ablaufpläne), OBS Studio (Szenen); kein Code übernommen (siehe `docs/claude/vorbilder/sendeablauf-rundown.md`)
- Umgesetzt: Ablauftabelle zum direkten Bearbeiten (Titel, Dauer, Hinweis; ↑ ↓ ✕; neue Zeile mit Enter), Beginnzeiten rechnen mit, farbige Zeitleiste mit Ende des Sendeplatzes, Sendung/Beginn/Sendeplatz im Kopf, „Beginn = jetzt“, On-Air-Uhr (laufender Punkt, großer Countdown mit Farbwarnung, Fortschritt, Nächstes, Restzeit), Live senden (ffmpeg) bleibt rechts, Weitergabe (Ablaufplan CSV, YouTube-Kapitel, Moderationskarten, OBS-Szenen), Rechner sofort (Leitung, Sprechzeit, Lautheit, Hochkant, Seitenverhältnis, Timecode), Wochenplan, Untertitel verschieben.
- Gefunden und behoben: „In OBS öffnen“-Leiste ragte in schmalen Spalten aus dem Fenster (bricht jetzt um, betrifft alle Studios)
- Tests: App 2 neu (echte Eingabe) ersetzen SPEC15_Ui/SPEC15_UiPlus; T0203-OBS-Tests und SPEC15_LiveUi grün
