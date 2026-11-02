# Sicherheits-Werkzeuge & Beweise – Testergebnisse

Studio: System auf Gefahren prüfen (SPEC-12) · Bereich: System & Sicherheit · Seite: `forensik` · Stand: 0.9.36 (Stand fecc44b)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 19 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen | ✓ bestanden |
| SPEC-12 UI: Prüfsumme vergleichen, Fall anlegen, Beweis sichern, Bericht | ✓ bestanden |
| SPEC-12: Beweis wird unverändert mit SHA-256 und Kette gesichert, Veränderung wird erkannt | ✓ bestanden |
| SPEC-12: Höchstens 120 h, Warnung 24 h vorher, danach vollständig gelöscht | ✓ bestanden |
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
| T-0201: Datei aufs Feld oder aufs Studio ziehen füllt das passende Feld | ✓ bestanden |
| T-0201: Mausklick auf „Datei wählen …“ füllt das Feld, die Datei steht danach unter „Zuletzt“; Eintippen geht weiter | ✓ bestanden |
| T-0201: Ordnerfeld öffnet den Ordner-Dialog | ✓ bestanden |
| T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“ | ✓ bestanden |
| X-02 UI: Beim Start automatisch geprüft, Seite zeigt Ergebnis, erneut prüfen | ✓ bestanden |
