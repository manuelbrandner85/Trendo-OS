# Bericht T-0606: Anmeldung am eigenen Kittl-Konto im POD-Studio

**Zweig:** `claude/T-0606-kittl-anmeldung`. Nach `main` übernommen am 09.11.2026 nach ausdrücklichem OK.

## Inhalt

Knopf „Bei Kittl anmelden“, Statusanzeige, „Verbindung prüfen“ und „Abmelden“. `claude mcp login` braucht ein echtes Terminal und läuft deshalb in einem eigenen Konsolenfenster. Die Zugangsdaten verwaltet Claude Code je Windows-Konto, Trendo speichert keine.

## Testergebnis

Volltest auf `670112e83`, alle Exitcodes 0: Api 16 bestanden; Core 2158 bestanden, 5 übersprungen; App 952 bestanden, 4 übersprungen; Paket-Tests 9 OK.

## Echter Lauf

`claude mcp login` im eigenen Konsolenfenster und `claude mcp get` melden „Connected“, Claude sieht 39 Werkzeuge von Kittl. Kein Werkzeug wurde aufgerufen, keine Credits verbraucht. Ohne Terminal bricht die Anmeldung ab und macht eine bestehende Anmeldung ungültig.

## Nicht geprüft

Der Knopf in der echten App, Linux (`x-terminal-emulator`), ein Lauf mit einer zweiten Person, eine echte Erzeugung bei Kittl. Claude meldet, dass die Anmeldung in wenigen Tagen abläuft.
