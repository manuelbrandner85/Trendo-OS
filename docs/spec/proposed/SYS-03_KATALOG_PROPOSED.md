# SYS-03 – Ultimate Browser Internet & Download Center: Funktionskatalog (PROPOSED)

> **Status: PROPOSED – nicht freigegeben.** Abgeleitet aus Modulname und Spezifikationsabsicht (MODULE_REGISTER: 25 Erweiterungsfunktionen ohne Katalog). Wird erst mit Manuels Freigabe zu APPROVED; nichts davon ist ACCEPTED.

| ID | Funktion | Beschreibung | Abnahmekriterium | Stand im Code |
|---|---|---|---|---|
| SYS-03-P01 | Browser-Erkennung | Installierte Browser finden | Chrome/Edge/Firefox korrekt erkannt | vorhanden |
| SYS-03-P02 | Adresse normalisieren | Nur http/https, sonst Suche | javascript:/file: werden nie geöffnet | vorhanden |
| SYS-03-P03 | Riskante Downloads | Warnung bei .exe/.msi/.ps1/.bat | Warnung sichtbar vor Öffnen | vorhanden |
| SYS-03-P04 | Unfertige Downloads | Erkennung .crdownload/.part | Werden nicht als fertig gezeigt | vorhanden |
| SYS-03-P05 | Download-Übersicht | Download-Ordner mit Größe/Datum | Liste stimmt mit Ordner | vorhanden |
| SYS-03-P06 | Prüfsumme prüfen | SHA-256 gegen eingegebenen Wert | Abweichung = rote Meldung | offen |
| SYS-03-P07 | Download-Manager | Eigene Downloads mit Fortsetzen | Abbruch + Fortsetzen ergibt gleichen Hash | offen |
| SYS-03-P08 | Lesezeichen | Lokal, Import aus Chrome-HTML | Import verlustfrei | offen |
| SYS-03-P09 | Lesemodus | Artikeltext ohne Werbung (lokal geparst) | Kein Skript wird ausgeführt | teilweise |
| SYS-03-P10 | Tor-Knopf | Tor Browser offiziell öffnen/anleiten | Nur offizieller Link, keine Bundles | teilweise |
| SYS-03-P11 | Privates Fenster | Browser im Inkognito-Modus starten | Richtiger Parameter je Browser | offen |
| SYS-03-P12 | Standardbrowser-Info | Anzeigen, nicht ändern | Keine Registry-Änderung | offen |
| SYS-03-P13 | Suchmaschine wählen | DuckDuckGo/Startpage/Google | Einstellung wirkt in Ctrl+K | offen |
| SYS-03-P14 | Seite als PDF | Über Browser-Headless drucken | PDF A4, Hash im Log | offen |
| SYS-03-P15 | Link-Prüfung | Ziel anzeigen, Kurz-URLs auflösen (optional) | Kein Aufruf ohne Zustimmung | offen |
| SYS-03-P16 | Phishing-Hinweise | Punycode/ähnliche Domains warnen | Testdomains werden markiert | offen |
| SYS-03-P17 | Formular-Schutz | Hinweis vor Absenden sensibler Daten | Nur Hinweis, nie Auto-Absenden | offen |
| SYS-03-P18 | Downloads sortieren | Nach Typ in Unterordner (Vorschau) | Rückgängig möglich | offen |
| SYS-03-P19 | Quarantäne-Ordner | Riskante Dateien isoliert ablegen | Datei wird nicht ausgeführt | offen |
| SYS-03-P20 | Defender-Scan anstoßen | Einzeldatei über MpCmdRun (ohne Admin) | Ergebnis angezeigt; fehlt → BLOCKED | offen |
| SYS-03-P21 | Webseiten-Notiz | URL + Notiz in Notizen speichern | Wiederfindbar über Suche | offen |
| SYS-03-P22 | Offline-Kopie | Seite als HTML speichern | Öffnet offline | offen |
| SYS-03-P23 | Netzwerkstatus | Online/Offline, DNS-Test | Offline-Modus wird respektiert | teilweise |
| SYS-03-P24 | Proxy-Info | System-Proxy anzeigen | Nur lesen | offen |
| SYS-03-P25 | Verlauf löschen anleiten | Schritt-für-Schritt je Browser | Kein direkter Eingriff in Profile | offen |

Stand: 5 vorhanden, 3 teilweise, 17 offen (Selbsteinschätzung, nicht abgenommen).
