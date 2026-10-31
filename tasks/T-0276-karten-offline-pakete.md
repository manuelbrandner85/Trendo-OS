# T-0276 – Karten: Offline-Kartenpakete (MBTiles, ZIP, Kachel-Ordner)

- Freigabe: Manuel im Chat am 31.10.2026 („Erst die Restpunkte“, „immer ok von mir“ für das neue Paket)
- Umgesetzt: `Karten/TilePack` liest MBTiles (SQLite, schreibgeschützt; TMS-Zeilen umgerechnet), ZIP und Ordner im Schema z/x/y.png/.jpg/.webp; Paket hat Vorrang vor Zwischenspeicher und Internet, Karte springt auf das Gebiet, Paket bleibt nach Neustart gemerkt, Entfernen. Trendo lädt selbst keine Kacheln massenhaft herunter (OSM-Kachelrichtlinie) – das Paket bringt der Nutzer mit (eigener Kachelserver, OpenMapTiles, Kachel-Werkzeug).
- Neue Abhängigkeit: Microsoft.Data.Sqlite 10.0.14 (MIT) mit SQLitePCLRaw/e_sqlite3 (Apache-2.0, SQLite gemeinfrei) – in THIRD_PARTY.md, THIRD_PARTY_NUGET.md (Skript) und im Abhängigkeitskatalog eingetragen.
- Tests: Core 1 (Ordner, ZIP, MBTiles, Fehlerfälle), App 1 (laden, Info, Fehlermeldung, entfernen); Lizenztest SYS-13-P22 grün
