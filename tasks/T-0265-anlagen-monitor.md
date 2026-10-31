# T-0265 – Digitaler Zwilling als echtes Programm „Anlagen-Monitor“

- Freigabe: Manuel im Chat am 31.10.2026 („alles Offene in jedem Studio umsetzen“, Masterprompt GODMODE 131)
- Vorbilder (Funktionsumfang, öffentlich dokumentiert): Grafana, Home Assistant, ThingsBoard; kein Code übernommen (siehe `docs/claude/vorbilder/monitor-grafana.md`)
- Umgesetzt: Anlagenbaum mit Ampel, Kacheln je Sensor (Wert, Mini-Verlauf, Trend, „Alarm in etwa x h“), großes Zeitdiagramm (Grenzbänder, Verlauf verdichtet, Ampelwechsel mit Hysterese, Prognose-Linie), Zeitraum 24 h/7 Tage/30 Tage/Alles, Grenzen und Einheit direkt eintippen, Kennzahlen/Energie im Zeitraum, Prognose-Stunden, Sensor anlegen, CSV einlesen, abgeleiteter Sensor, umbenennen, CSV/SVG speichern, löschen mit Bestätigung, Speicher begrenzen, Betriebsstunden/Wartung/stille Sensoren (sofort), „Diesen PC messen“ und Live alle 5 s, Beispielanlagen (erfunden).
- Tests: Core 2 (Beispielanlagen, Diagramm, SVG), App 2 (echte Eingabe) ersetzen SPEC10_Ui/SPEC10_UiPlus; Sys12-Statuszeichen weiter grün
