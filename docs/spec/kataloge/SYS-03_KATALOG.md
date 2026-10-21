# SYS-03 – Ultimate Browser Internet & Download Center: Funktionskatalog (APPROVED)

> **Status: APPROVED (APPROVED_SPEC).** Von Manuel am 21.10.2026 im Chat freigegeben (Vorschlag aus PR #143). Freigegeben heißt: Umfang festgelegt – nicht ACCEPTED. Die Spalte „Stand im Code“ wird bei Umsetzung ehrlich gepflegt (höchstens „umgesetzt, Box getestet“; PC-Prüfung offen).

| ID | Funktion | Beschreibung | Abnahmekriterium | Stand im Code |
|---|---|---|---|---|
| SYS-03-P01 | Browser-Erkennung | Installierte Browser finden | Chrome/Edge/Firefox korrekt erkannt | vorhanden |
| SYS-03-P02 | Adresse normalisieren | Nur http/https, sonst Suche | javascript:/file: werden nie geöffnet | vorhanden |
| SYS-03-P03 | Riskante Downloads | Warnung bei .exe/.msi/.ps1/.bat | Warnung sichtbar vor Öffnen | vorhanden |
| SYS-03-P04 | Unfertige Downloads | Erkennung .crdownload/.part | Werden nicht als fertig gezeigt | vorhanden |
| SYS-03-P05 | Download-Übersicht | Download-Ordner mit Größe/Datum | Liste stimmt mit Ordner | vorhanden |
| SYS-03-P06 | Prüfsumme prüfen | SHA-256 gegen eingegebenen Wert | Abweichung = rote Meldung | umgesetzt, Box getestet |
| SYS-03-P07 | Download-Manager | Eigene Downloads mit Fortsetzen | Abbruch + Fortsetzen ergibt gleichen Hash | umgesetzt, Box getestet |
| SYS-03-P08 | Lesezeichen | Lokal, Import aus Chrome-HTML | Import verlustfrei | umgesetzt, Box getestet |
| SYS-03-P09 | Lesemodus | Artikeltext ohne Werbung (lokal geparst) | Kein Skript wird ausgeführt | umgesetzt, Box getestet |
| SYS-03-P10 | Tor-Knopf | Tor Browser offiziell öffnen/anleiten | Nur offizieller Link, keine Bundles | umgesetzt, Box getestet |
| SYS-03-P11 | Privates Fenster | Browser im Inkognito-Modus starten | Richtiger Parameter je Browser | umgesetzt, Box getestet |
| SYS-03-P12 | Standardbrowser-Info | Anzeigen, nicht ändern | Keine Registry-Änderung | umgesetzt, Box getestet |
| SYS-03-P13 | Suchmaschine wählen | DuckDuckGo/Startpage/Google | Einstellung wirkt in Ctrl+K | umgesetzt, Box getestet |
| SYS-03-P14 | Seite als PDF | Über Browser-Headless drucken | PDF A4, Hash im Log | umgesetzt, Box getestet |
| SYS-03-P15 | Link-Prüfung | Ziel anzeigen, Kurz-URLs auflösen (optional) | Kein Aufruf ohne Zustimmung | umgesetzt, Box getestet |
| SYS-03-P16 | Phishing-Hinweise | Punycode/ähnliche Domains warnen | Testdomains werden markiert | umgesetzt, Box getestet |
| SYS-03-P17 | Formular-Schutz | Hinweis vor Absenden sensibler Daten | Nur Hinweis, nie Auto-Absenden | umgesetzt, Box getestet |
| SYS-03-P18 | Downloads sortieren | Nach Typ in Unterordner (Vorschau) | Rückgängig möglich | umgesetzt, Box getestet |
| SYS-03-P19 | Quarantäne-Ordner | Riskante Dateien isoliert ablegen | Datei wird nicht ausgeführt | umgesetzt, Box getestet |
| SYS-03-P20 | Defender-Scan anstoßen | Einzeldatei über MpCmdRun (ohne Admin) | Ergebnis angezeigt; fehlt → BLOCKED | umgesetzt, Box getestet |
| SYS-03-P21 | Webseiten-Notiz | URL + Notiz in Notizen speichern | Wiederfindbar über Suche | umgesetzt, Box getestet |
| SYS-03-P22 | Offline-Kopie | Seite als HTML speichern | Öffnet offline | umgesetzt, Box getestet |
| SYS-03-P23 | Netzwerkstatus | Online/Offline, DNS-Test | Offline-Modus wird respektiert | umgesetzt, Box getestet |
| SYS-03-P24 | Proxy-Info | System-Proxy anzeigen | Nur lesen | umgesetzt, Box getestet |
| SYS-03-P25 | Verlauf löschen anleiten | Schritt-für-Schritt je Browser | Kein direkter Eingriff in Profile | umgesetzt, Box getestet |

Stand: 5 vorhanden (vorher), 20 umgesetzt und auf der Linux-Box getestet, 0 teilweise, 0 offen. PC-Prüfung steht für alle aus.
