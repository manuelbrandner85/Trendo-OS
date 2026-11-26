# Geräte & Laufwerke – Testplan

Studio: Geräte erkennen (SYS-07) · Bereich: System & Sicherheit · Seite: `geraete` · Stand: 0.9.110 (Stand 0eb9d52)

11 Tests sprechen dieses Programm an.

- Geräte: namenloses PCI-Gerät erscheint mit Kennung und Angebot; „Namen nachschlagen“ lädt die Liste (Prüfsumme) und zeigt Hersteller und Modell; danach kein Angebot mehr
- SYS-07 T-0764 UI: Stick einstecken → „Neu eingesteckt“ mit Auswerfen; Auswerfen → abziehbar, Eintrag weg; Systemlaufwerk ohne Knopf; Problemgerät mit nächstem Schritt
- SYS-07 T-0764 echt (nur lesen): Zuordnung Laufwerk → Gerät über SetupAPI findet den Datenträger des Systemlaufwerks – ohne Auswurf
- SYS-07 T-0764 echt: Windows-Auswurf (cfgmgr32) für ein nicht vorhandenes Laufwerk meldet „nicht gefunden“ und wirft nichts aus
- SYS-07 T-0764: Geräteliste liest den Problemcode (5. Spalte) und nennt den nächsten Schritt; ältere Ausgabe ohne Spalte geht weiter
- SYS-07 T-0764: Systemlaufwerk, Trendo-Laufwerk und feste Platten werden nie ausgeworfen – das Betriebssystem wird gar nicht gefragt
- SYS-07 T-0764: Windows-Problemcode → verständlicher nächster Schritt (Name oder Zahl); ohne Problem kein Hinweis
- SYS-07 T-0764: nur Wechseldatenträger bekommen „Auswerfen“; neu eingesteckte Sticks sind als Menge erkennbar
- SYS-07 UI: Geräte suchen zeigt Gruppen
- SYS-07: Linux-/sys wird gelesen; Neues und Verschwundenes wird erkannt
- SYS-07: Windows-Geräteliste (PnP) wird gelesen, Probleme erkannt, Systemeinträge ausgeblendet
