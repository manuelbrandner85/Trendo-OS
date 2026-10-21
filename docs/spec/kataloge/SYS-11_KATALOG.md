# SYS-11 – Ready-to-Run & Software Integration: Funktionskatalog (APPROVED)

> **Status: APPROVED (APPROVED_SPEC).** Von Manuel am 21.10.2026 im Chat freigegeben (Vorschlag aus PR #143). Freigegeben heißt: Umfang festgelegt – nicht ACCEPTED. Die Spalte „Stand im Code“ wird bei Umsetzung ehrlich gepflegt (höchstens „umgesetzt, Box getestet“; PC-Prüfung offen).

| ID | Funktion | Beschreibung | Abnahmekriterium | Stand im Code |
|---|---|---|---|---|
| SYS-11-P01 | Programme-Übersicht | Installierte Programme lesen (nur lesen) | Liste ≈ Windows-Apps | vorhanden |
| SYS-11-P02 | App-Center (winget) | Suchen/Installieren --scope user mit Zustimmung | Kein Admin, Zustimmung protokolliert | vorhanden |
| SYS-11-P03 | Pakete je Studio | Benötigte Werkzeuge pro Studio | Fehlend = klar markiert | vorhanden |
| SYS-11-P04 | Offizielle Quellen | Nur Herstellerlinks/winget-IDs | Kein Fremdmirror | vorhanden |
| SYS-11-P05 | Lizenzliste | Lizenz je Komponente | Liste vollständig | vorhanden |
| SYS-11-P06 | Update-Prüfung | winget upgrade --dry (nur anzeigen) | Keine Auto-Updates | offen |
| SYS-11-P07 | Portable Tools | Tools ins Benutzerverzeichnis entpacken | Hash geprüft | teilweise |
| SYS-11-P08 | Werkzeug-Pfade | Eigene Pfade für Tools setzen | Wird bei Erkennung genutzt | vorhanden |
| SYS-11-P09 | Deinstallation anleiten | Link zu Windows-Einstellungen | Keine stille Deinstallation | offen |
| SYS-11-P10 | Starten mit Parametern | Programmverknüpfungen mit Argumenten | Parameter werden übergeben | offen |
| SYS-11-P11 | Autostart-Übersicht | Benutzer-Autostart anzeigen | Nur lesen, ändern mit Zustimmung | offen |
| SYS-11-P12 | Gesperrte Programme | Blender/Unreal/VS nie verändern | Schutzliste greift | vorhanden |
| SYS-11-P13 | Installationsprotokoll | Jeder Schritt im Audit | Audit enthält Befehl + Ergebnis | vorhanden |
| SYS-11-P14 | Offline-Pakete | Lokale Installer aus Ordner | SHA vor Ausführung | offen |
| SYS-11-P15 | Systemvoraussetzungen | RAM/Platte/OS vor Installation prüfen | Zu wenig → BLOCKED | teilweise |
| SYS-11-P16 | Studio-Bereitschaft | Ampel je Studio | Ampel stimmt mit Erkennung | vorhanden |
| SYS-11-P17 | Erste Einrichtung | Assistent mit Paketwahl | Nur gewählte Pakete | vorhanden |
| SYS-11-P18 | Rücksetzen Konfiguration | Trendo-Einstellungen zurücksetzen | Sicherung vorher | teilweise |
| SYS-11-P19 | Dateizuordnungen anzeigen | Welche App öffnet was | Nur lesen | offen |
| SYS-11-P20 | Kompatibilitätshinweise | Bekannte Probleme je Programm | Hinweistext pro Eintrag | offen |
| SYS-11-P21 | Integrationstests | Je Werkzeug Kurztest (--version) | Ergebnis mit Log | teilweise |
| SYS-11-P22 | Export Softwareliste | CSV der installierten Programme | CSV öffnet in Tabelle | offen |
| SYS-11-P23 | Wiederherstellungsliste | Programme für Neuinstallation (winget export) | Datei importierbar | offen |
| SYS-11-P24 | Speicherbedarf | Größe je Tool im Benutzerordner | Summe korrekt | offen |
| SYS-11-P25 | Selbsttest | trendo --selftest mit Bericht | Bericht ohne private Pfade | teilweise |

Stand: 10 vorhanden, 5 teilweise, 10 offen (Selbsteinschätzung, nicht abgenommen).
