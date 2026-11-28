# Geräte & Laufwerke – Testergebnisse

Studio: Geräte erkennen (SYS-07) · Bereich: System & Sicherheit · Seite: `geraete` · Stand: 0.9.113 (Stand 1978f1a)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 12 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Geräte: namenloses PCI-Gerät erscheint mit Kennung und Angebot; „Namen nachschlagen“ lädt die Liste (Prüfsumme) und zeigt Hersteller und Modell; danach kein Angebot mehr | übersprungen |
| SYS-07 T-0764 UI: Stick einstecken → „Neu eingesteckt“ mit Auswerfen; Auswerfen → abziehbar, Eintrag weg; Systemlaufwerk ohne Knopf; Problemgerät mit nächstem Schritt | ✓ bestanden |
| SYS-07 T-0764 echt (nur lesen): Zuordnung Laufwerk → Gerät über SetupAPI findet den Datenträger des Systemlaufwerks – ohne Auswurf | ✓ bestanden |
| SYS-07 T-0764 echt: Windows-Auswurf (cfgmgr32) für ein nicht vorhandenes Laufwerk meldet „nicht gefunden“ und wirft nichts aus | ✓ bestanden |
| SYS-07 T-0764: Geräteliste liest den Problemcode (5. Spalte) und nennt den nächsten Schritt; ältere Ausgabe ohne Spalte geht weiter | ✓ bestanden |
| SYS-07 T-0764: Systemlaufwerk, Trendo-Laufwerk und feste Platten werden nie ausgeworfen – das Betriebssystem wird gar nicht gefragt | ✓ bestanden |
| SYS-07 T-0764: Windows-Problemcode → verständlicher nächster Schritt (Name oder Zahl); ohne Problem kein Hinweis | ✓ bestanden |
| SYS-07 T-0764: nur Wechseldatenträger bekommen „Auswerfen“; neu eingesteckte Sticks sind als Menge erkennbar | ✓ bestanden |
| SYS-07 UI: Geräte suchen zeigt Gruppen | ✓ bestanden |
| SYS-07: Linux-/sys wird gelesen; Neues und Verschwundenes wird erkannt | ✓ bestanden |
| SYS-07: Windows-Geräteliste (PnP) wird gelesen, Probleme erkannt, Systemeinträge ausgeblendet | ✓ bestanden |
| T-0777 Katalog: im echten Register stehen SYS-04, SYS-07, CORE-04 und X-08 nicht mehr als PC_VERIFIED im Katalog | ✓ bestanden |
| T-0777 Nachbesserung: Studio-Karten und Fortschritt zeigen SYS-04, SYS-07, CORE-04, X-08 nicht mehr als „auf dem PC geprüft“ (Register unverändert) | ✓ bestanden |
