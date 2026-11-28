# Programme verwalten – Testplan

Studio: Programme installieren (SYS-04) · Bereich: System & Sicherheit · Seite: `appcenter` · Stand: 0.9.113 (Stand 1978f1a)

12 Tests sprechen dieses Programm an.

- SYS-04 UI: Suchen, installieren, Updates anzeigen und aktualisieren, entfernen
- SYS-04: Installieren nur nach Freigabe, als Benutzer, gefährliche IDs abgelehnt
- SYS-04: winget- und flatpak-Ausgaben werden korrekt gelesen
- T-0681 App-Center: Installieren fragt in einem eigenen Fenster nach (was, woher, Größe) – ohne Bestätigung passiert nichts, Ablehnen steht im Protokoll, erst „Installieren“ installiert
- T-0681 App-Center: auch Entfernen und Aktualisieren fragen vorher; die Rückfrage nennt Programm und Vorgang; Nein ändert nichts
- T-0755 SYS-04 (ERSETZT: Testprogramm statt winget): Abbrechen beendet den Lauf samt Kindprozess, Protokoll sagt „abgebrochen“, Nachprüfung: nicht installiert
- T-0755 SYS-04 (ERSETZT: Testprogramm statt winget): Installieren mit Protokoll, Rückgabewert und Nachprüfung; Entfernen prüft nach; Fehler wird erkannt
- T-0755 SYS-04 UI (ERSETZT: Testprogramm statt winget): Rückfrage zeigt den Befehl; Lauf zeigt Befehl, Fortschritt und Ausgabe live, schreibt ein Protokoll und prüft nach; Abbrechen beendet den Lauf
- T-0755 SYS-04: Fortschritt, Ausgabezeilen und Nachprüfung werden aus winget-Ausgaben gelesen (nachgebildete Ausgabe)
- T-0777 Katalog: im echten Register stehen SYS-04, SYS-07, CORE-04 und X-08 nicht mehr als PC_VERIFIED im Katalog
- T-0777 Nachbesserung: Studio-Karten und Fortschritt zeigen SYS-04, SYS-07, CORE-04, X-08 nicht mehr als „auf dem PC geprüft“ (Register unverändert)
- Y3/Y6: Sicherheitszentrale mit Schutz-Ring und Ampel-Karten je Prüfung; Programme verwalten mit App-Regal – Klick sucht, installiert nichts ohne „Installieren“
