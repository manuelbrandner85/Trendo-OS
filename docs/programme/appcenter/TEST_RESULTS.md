# Programme verwalten – Testergebnisse

Studio: Programme installieren (SYS-04) · Bereich: System & Sicherheit · Seite: `appcenter` · Stand: 0.9.114 (Stand 99cbb5c)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 12 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| SYS-04 UI: Suchen, installieren, Updates anzeigen und aktualisieren, entfernen | ✓ bestanden |
| SYS-04: Installieren nur nach Freigabe, als Benutzer, gefährliche IDs abgelehnt | ✓ bestanden |
| SYS-04: winget- und flatpak-Ausgaben werden korrekt gelesen | ✓ bestanden |
| T-0681 App-Center: Installieren fragt in einem eigenen Fenster nach (was, woher, Größe) – ohne Bestätigung passiert nichts, Ablehnen steht im Protokoll, erst „Installieren“ installiert | ✓ bestanden |
| T-0681 App-Center: auch Entfernen und Aktualisieren fragen vorher; die Rückfrage nennt Programm und Vorgang; Nein ändert nichts | ✓ bestanden |
| T-0755 SYS-04 (ERSETZT: Testprogramm statt winget): Abbrechen beendet den Lauf samt Kindprozess, Protokoll sagt „abgebrochen“, Nachprüfung: nicht installiert | ✓ bestanden |
| T-0755 SYS-04 (ERSETZT: Testprogramm statt winget): Installieren mit Protokoll, Rückgabewert und Nachprüfung; Entfernen prüft nach; Fehler wird erkannt | ✓ bestanden |
| T-0755 SYS-04 UI (ERSETZT: Testprogramm statt winget): Rückfrage zeigt den Befehl; Lauf zeigt Befehl, Fortschritt und Ausgabe live, schreibt ein Protokoll und prüft nach; Abbrechen beendet den Lauf | ✓ bestanden |
| T-0755 SYS-04: Fortschritt, Ausgabezeilen und Nachprüfung werden aus winget-Ausgaben gelesen (nachgebildete Ausgabe) | ✓ bestanden |
| T-0777 Katalog: im echten Register stehen SYS-04, SYS-07, CORE-04 und X-08 nicht mehr als PC_VERIFIED im Katalog | ✓ bestanden |
| T-0777 Nachbesserung: Studio-Karten und Fortschritt zeigen SYS-04, SYS-07, CORE-04, X-08 nicht mehr als „auf dem PC geprüft“ (Register unverändert) | ✓ bestanden |
| Y3/Y6: Sicherheitszentrale mit Schutz-Ring und Ampel-Karten je Prüfung; Programme verwalten mit App-Regal – Klick sucht, installiert nichts ohne „Installieren“ | ✓ bestanden |
