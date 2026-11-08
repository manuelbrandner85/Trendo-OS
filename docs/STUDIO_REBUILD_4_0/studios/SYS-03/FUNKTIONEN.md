# Funktionsliste: Surfen & Herunterladen

Studio SYS-03 · Surfen & Herunterladen · Bereich System & Sicherheit

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

| Nr | Funktion | Stand laut Katalog | Test |
|---|---|---|---|
| SYS-03-P01 | Browser-Erkennung | vorhanden | – |
| SYS-03-P02 | Adresse normalisieren | vorhanden | – |
| SYS-03-P03 | Riskante Downloads | vorhanden | – |
| SYS-03-P04 | Unfertige Downloads | vorhanden | – |
| SYS-03-P05 | Download-Übersicht | vorhanden | – |
| SYS-03-P06 | Prüfsumme prüfen | umgesetzt, Box getestet | – |
| SYS-03-P07 | Download-Manager | umgesetzt, Box getestet | – |
| SYS-03-P08 | Lesezeichen | umgesetzt, Box getestet | – |
| SYS-03-P09 | Lesemodus | umgesetzt, Box getestet | – |
| SYS-03-P10 | Tor-Knopf | umgesetzt, Box getestet | – |
| SYS-03-P11 | Privates Fenster | umgesetzt, Box getestet | – |
| SYS-03-P12 | Standardbrowser-Info | umgesetzt, Box getestet | – |
| SYS-03-P13 | Suchmaschine wählen | umgesetzt, Box getestet | – |
| SYS-03-P14 | Seite als PDF | umgesetzt, Box getestet | – |
| SYS-03-P15 | Link-Prüfung | umgesetzt, Box getestet | – |
| SYS-03-P16 | Phishing-Hinweise | umgesetzt, Box getestet | – |
| SYS-03-P17 | Formular-Schutz | umgesetzt, Box getestet | – |
| SYS-03-P18 | Downloads sortieren | umgesetzt, Box getestet | – |
| SYS-03-P19 | Quarantäne-Ordner | umgesetzt, Box getestet | – |
| SYS-03-P20 | Defender-Scan anstoßen | umgesetzt, Box getestet | – |
| SYS-03-P21 | Webseiten-Notiz | umgesetzt, Box getestet | – |
| SYS-03-P22 | Offline-Kopie | umgesetzt, Box getestet | – |
| SYS-03-P23 | Netzwerkstatus | umgesetzt, Box getestet | – |
| SYS-03-P24 | Proxy-Info | umgesetzt, Box getestet | – |
| SYS-03-P25 | Verlauf löschen anleiten | umgesetzt, Box getestet | – |
| SYS-03-P01 | Browser-Erkennung | vorhanden | – |
| SYS-03-P02 | Adresse normalisieren | vorhanden | – |
| SYS-03-P03 | Riskante Downloads | vorhanden | – |
| SYS-03-P04 | Unfertige Downloads | vorhanden | – |
| SYS-03-P05 | Download-Übersicht | vorhanden | – |
| SYS-03-P06 | Prüfsumme prüfen | umgesetzt, Box getestet | – |
| SYS-03-P07 | Download-Manager | umgesetzt, Box getestet | – |
| SYS-03-P08 | Lesezeichen | umgesetzt, Box getestet | – |
| SYS-03-P09 | Lesemodus | umgesetzt, Box getestet | – |
| SYS-03-P10 | Tor-Knopf | umgesetzt, Box getestet | – |
| SYS-03-P11 | Privates Fenster | umgesetzt, Box getestet | – |
| SYS-03-P12 | Standardbrowser-Info | umgesetzt, Box getestet | – |
| SYS-03-P13 | Suchmaschine wählen | umgesetzt, Box getestet | – |
| SYS-03-P14 | Seite als PDF | umgesetzt, Box getestet | – |
| SYS-03-P15 | Link-Prüfung | umgesetzt, Box getestet | – |
| SYS-03-P16 | Phishing-Hinweise | umgesetzt, Box getestet | – |
| SYS-03-P17 | Formular-Schutz | umgesetzt, Box getestet | – |
| SYS-03-P18 | Downloads sortieren | umgesetzt, Box getestet | – |
| SYS-03-P19 | Quarantäne-Ordner | umgesetzt, Box getestet | – |
| SYS-03-P20 | Defender-Scan anstoßen | umgesetzt, Box getestet | – |
| SYS-03-P21 | Webseiten-Notiz | umgesetzt, Box getestet | – |
| SYS-03-P22 | Offline-Kopie | umgesetzt, Box getestet | – |
| SYS-03-P23 | Netzwerkstatus | umgesetzt, Box getestet | – |
| SYS-03-P24 | Proxy-Info | umgesetzt, Box getestet | – |
| SYS-03-P25 | Verlauf löschen anleiten | umgesetzt, Box getestet | – |

## Fehlende Funktionen laut Vorbild-Analyse

- ✗ Autostart-Liste mit Einfluss auf den Start – bewusst nicht, Windows-Einstellungen werden nicht geändert
- ✗ Prüfung über fremde Online-Dienste (z. B. VirusTotal) – bewusst nicht, alles bleibt auf dem PC

## Teilweise vorhanden

- ◐ Empfänger sehen eine Dateiliste im Browser – kein Hochladen zurück (bewusst nur lesen)
