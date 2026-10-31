# T-0268 – Verkehr & Fahrzeug als echtes Programm „Fahrzeugbuch“

- Freigabe: Manuel im Chat am 31.10.2026 („alles Offene in jedem Studio umsetzen“, Masterprompt GODMODE 131)
- Vorbilder (Funktionsumfang, öffentlich dokumentiert): Spritmonitor, Fuelio, Drivvo; kein Code übernommen (siehe `docs/claude/vorbilder/fahrzeugbuch-spritmonitor.md`)
- Umgesetzt: Fahrzeugliste mit Erinnerungen, Fahrzeug anlegen, Übersicht (Ø Verbrauch, ct/km, Kosten, km-Stand, Erinnerungen, Verbrauchsverlauf je Tankfüllung mit Mittelwert, Kosten je Monat), Tank-/Ladebuch und Fahrtenbuch als Tabellen (neue Zeile mit Enter, korrigieren, löschen, Lücken-Warnung, dienstlicher Anteil), Fahrzeugdaten (Kennzeichen, HU, Service, Elektro), Export Fahrten-/Tankbuch CSV, Löschen mit Bestätigung, Rechner sofort und mit Werten des Fahrzeugs vorbelegt (Jahreskosten, Teilen, Reifen, Ladezeit, E-Auto, Pendlerpauschale, Dienstwagen, Kilometergeld), Beispielfahrzeug.
- Gefunden und behoben: Rechner-Vorbelegung blieb nach Fahrzeugwechsel beim alten Fahrzeug
- Tests: App 2 neu (echte Eingabe) ersetzen SPEC16_Ui/SPEC16_UiPlus; FachBedienungTests auf SPEC-04 umgestellt
