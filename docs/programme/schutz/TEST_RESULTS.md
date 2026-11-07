# Schutz & Datenschutz – Testergebnisse

Studio: Daten schützen (CORE-11) · Bereich: System & Sicherheit · Seite: `schutz` · Stand: 0.9.36 (Stand 2963379)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 30 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| A2/A4: Texte & Tabellen mit Neu-Karten (Dokument, Tabelle, Präsentation, PDF, Brief, Word-Vorlagen) und Leerzustand; Neues Projekt mit Ziel-Karten | ✓ bestanden |
| CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen | ✓ bestanden |
| CORE-11: Ordnerprüfung findet Passwort, Schlüssel, gültige IBAN, Kartennummer – maskiert | ✓ bestanden |
| CORE-11: Schutz-Prüfung erkennt fehlende Sicherung und verändertes Protokoll | ✓ bestanden |
| CORE-11: Sicherung enthält keine fremden Ordner – nur der Trendo-Datenordner, eine kaputte ZIP lässt die Daten unverändert | ✓ bestanden |
| CORE-11: Sicherung fällig – ohne Sicherung sofort, sonst erst nach dem Abstand; 0 Stunden heißt nie automatisch | ✓ bestanden |
| CORE-11: Sicherung – höchstens „keep“ Sicherungen, Vorschau zählt ersetzte und neue Dateien, Wiederherstellen holt den alten Stand zurück | ✓ bestanden |
| G10: KI-Aufträge der Grafikkarten-Warteschlange als Karten – läuft mit Laufzeit, wartet mit Platz; fertig = Karte weg | ✓ bestanden |
| G2: Befehlsleiste mit Gruppen-Überschriften und Symbolen; ausgeführter Befehl steht danach oben unter „Zuletzt benutzt“ | ✓ bestanden |
| G3/G4: Vorschlags-Chips unter der Studio-KI-Zeile setzen den Auftrag ein (starten nichts) und verschwinden beim Tippen; Desktop „Weitermachen“ mit zuletzt benutzten Programmen | ✓ bestanden |
| G9: Seitenleisten in Trendo Musik mit Griff; Breite gemerkt und beim nächsten Öffnen angewandt; Doppelklick klappt ein und zur letzten Breite aus | ✓ bestanden |
| I2/I4: KI-Rollen als Karten (Auftrag, Schreibrecht), Wahl markiert; Ablauf-Vorlagen als Karten, ohne Ablauf Leerzustand, Vorlage legt Ablauf an | ✓ bestanden |
| K1: Musikspur im Videoschnitt zeigt die echte Wellenform der Tondatei (laut/leise erkennbar); ohne lesbare Datei nur Mittellinie statt erfundener Welle | ✓ bestanden |
| K3: Abspielleiste von Trendo Musik zeigt die echte Wellenform des gewählten Lieds; ohne Lied keine Welle | ✓ bestanden |
| SPEC-12 UI: Prüfsumme vergleichen, Fall anlegen, Beweis sichern, Bericht | ✓ bestanden |
| SYS-04 UI: Suchen, installieren, Updates anzeigen und aktualisieren, entfernen | ✓ bestanden |
| SYS-06 Freigabe-Dialog: Fenster schließen (X) lehnt ab | ✓ bestanden |
| SYS-06 Freigabe-Dialog: Klick auf „Ablehnen“ lehnt ab | ✓ bestanden |
| SYS-06 Freigabe-Dialog: Klick auf „Erlauben“ startet die Freigabe; Schließen des Hauptfensters beendet sie | ✓ bestanden |
| SYS-06 Freigabe-Dialog: nennt Ordner und Dauer, Esc lehnt ab | ✓ bestanden |
| SYS-06 Freigabe-Gate: ohne Zustimmung keine Freigabe, Anfrage abgelehnt | ✓ bestanden |
| SYS-06 UI: Ordner teilen, Datei echt per HTTP laden, falsches Token abgelehnt, beenden | ✓ bestanden |
| SYS-07 UI: Geräte suchen zeigt Gruppen | ✓ bestanden |
| SYS-08 UI: Seite im Lesemodus lesen, .onion wird nicht abgerufen, Sitzung beenden | ✓ bestanden |
| SYS-09 UI: Kontakt anlegen, Entwurf prüfen und im Mailprogramm öffnen (nichts wird gesendet) | ✓ bestanden |
| T4/T7: Geräte überwachen und Auto & Fahrten starten mit Leerzustand; Beispiel-Knopf darin füllt Dashboard bzw. Fahrzeug | ✓ bestanden |
| W3: Wissensnetz – Seiten als Punkte, [[Verweise]] als Linien (ungerichtet, ohne Doppelte, fehlende Ziele nicht), Klick öffnet die Seite; leer mit Leerzustand | ✓ bestanden |
| X-02 UI: Beim Start automatisch geprüft, Seite zeigt Ergebnis, erneut prüfen | ✓ bestanden |
| Y3/Y6: Sicherheitszentrale mit Schutz-Ring und Ampel-Karten je Prüfung; Programme verwalten mit App-Regal – Klick sucht, installiert nichts ohne „Installieren“ | ✓ bestanden |
| Y4/Y7/Y11/Y14: Gefahren prüfen, Teilen, Qualität und Geräteverbund als Werkzeug-Karten mit Symbol; Geräteverbund ohne Geräte mit Leerzustand | ✓ bestanden |
