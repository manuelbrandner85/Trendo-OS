# T-0262 – Lernen & Training als echtes Programm „Karteikarten“

- Freigabe: Manuel im Chat am 31.10.2026 („alles Offene in jedem Studio umsetzen“, Masterprompt GODMODE 131)
- Vorbilder (Funktionsumfang, öffentlich dokumentiert): Anki, Quizlet; kein Code übernommen (siehe `docs/claude/vorbilder/lernen-anki.md`)
- Umgesetzt: Lernmodus mit großer Karte (Leertaste/Klick deckt auf, Tasten 1–4 bewerten, Abstand-Vorschau auf den Knöpfen), Eintippen mit Tippfehler-Toleranz, Auswahl-Quiz (Zifferntasten), Kartenverwaltung als bearbeitbare Tabelle mit Suche/Löschen/Liste einfügen, Statistik mit 7-Tage-Diagramm, Lernserie, Problemkarten, Prüfungsplan (sofort), Stapel umbenennen/umkehren/als CSV speichern. Bestehende Lernkarten bleiben erhalten.
- SM-2 abgestuft: „Schwer“ wächst langsam, „Leicht“ springt weiter (neue Karte 4 Tage); „Gut“ unverändert klassisch (1, 6, × Leichtigkeit)
- Gefunden und behoben: Eintipp-Modus zeigte nach der Antwort schon die nächste Karte statt des Ergebnisses; Umbenennen löste beim Fokusverlust ein zweites Mal aus
- Tests: Core 2 (SM-2, Vorschau = echte Bewertung, Abstufung), App 2 (echte Tastatur/Maus: Leertaste, 1–4, Eintippen, Quiz, Tabelle, Werkzeuge)
