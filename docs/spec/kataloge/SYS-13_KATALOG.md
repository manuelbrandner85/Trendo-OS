# SYS-13 – System Architecture Integration & Release Quality: Funktionskatalog (APPROVED)

> **Status: APPROVED (APPROVED_SPEC).** Von Manuel am 21.10.2026 im Chat freigegeben (Vorschlag aus PR #143). Freigegeben heißt: Umfang festgelegt – nicht ACCEPTED. Die Spalte „Stand im Code“ wird bei Umsetzung ehrlich gepflegt (höchstens „umgesetzt, Box getestet“; PC-Prüfung offen).

| ID | Funktion | Beschreibung | Abnahmekriterium | Stand im Code |
|---|---|---|---|---|
| SYS-13-P01 | Absturzprotokoll | logs/absturz-*.txt, geschwärzt, 20 behalten | Kein Benutzername im Log | vorhanden |
| SYS-13-P02 | Version anzeigen | --version mit Commit | Gleich mit Release | vorhanden |
| SYS-13-P03 | Release nur von main | release.sh prüft Sauberkeit | Abbruch bei Abweichung | vorhanden |
| SYS-13-P04 | Auto-Update | Velopack aus GitHub-Releases | Update 0.x→0.y ohne Admin | vorhanden |
| SYS-13-P05 | Anforderungsprüfung | validate_requirements.py | Fehler bei Status ohne Beleg | vorhanden |
| SYS-13-P06 | Bauplan | build_plan.py | Plan = YAML | vorhanden |
| SYS-13-P07 | Belege mit SHA | reports/ mit SHA256SUMS | Prüfsummen stimmen | vorhanden |
| SYS-13-P08 | Selbsttest | --selftest-crashlog | Exit 0 | vorhanden |
| SYS-13-P09 | Audit-Protokoll | Alle Aktionen JSONL | Manipulation erkennbar (Hash-Kette) | teilweise |
| SYS-13-P10 | Datenschutz-Schwärzung | Pfade/Namen in Berichten | Test findet keine | vorhanden |
| SYS-13-P11 | Offline-Kern | Core ohne Netzbibliothek | QA-019-Test | vorhanden |
| SYS-13-P12 | API lokal | 127.0.0.1 mit Token | Kein Zugriff von außen | vorhanden |
| SYS-13-P13 | Modul-Register | Jede Seite einem Modul zugeordnet | Test: keine verwaisten Seiten | teilweise |
| SYS-13-P14 | Fehlerseite | Freundliche Meldung statt Absturz | Ausnahme zeigt Hinweis | teilweise |
| SYS-13-P15 | Leistungsbudget | Start < 3 s, Seite < 300 ms | Messung im Test | offen |
| SYS-13-P16 | Speicherleck-Test | 100 Seitenwechsel ohne Anstieg >50 MB | Messung | offen |
| SYS-13-P17 | Signatur-Status | Code-Signing ehrlich als offen | Nie als bestanden | vorhanden |
| SYS-13-P18 | Portable-Paket | ZIP ohne Installation | Startet | vorhanden |
| SYS-13-P19 | Datensicherung | Sicherung/Wiederherstellung | Rundweg | vorhanden |
| SYS-13-P20 | Migrationen | Einstellungsformat versioniert | Alte Datei wird gelesen | offen |
| SYS-13-P21 | Telemetrie aus | Keine Telemetrie | Netzwerkmitschnitt leer | teilweise |
| SYS-13-P22 | Abhängigkeits-Lizenzen | NuGet-Lizenzliste im Paket | Liste vollständig | offen |
| SYS-13-P23 | Sicherheits-Scan | dotnet list package --vulnerable in CI | Keine bekannten Lücken | offen |
| SYS-13-P24 | PC-Testlauf | dotnet test auf Zielrechner | Log + SHA in reports/ | vorhanden |
| SYS-13-P25 | Rückfall-Version | Vorherige Version installierbar | Downgrade-Anleitung getestet | offen |

Stand: 15 vorhanden, 4 teilweise, 6 offen (Selbsteinschätzung, nicht abgenommen).
