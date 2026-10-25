# T-0008 Bericht: Installation, Update, Rückfall und Deinstallation (C4, IMP-16)

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC) · **Status:** PC-Lauf bestanden, Rohlog mit SHA-256

## Durchgeführt am 2026-10-25 auf Manuels PC
Windows 11 Pro Insider Preview (NT 10.0.26340), Benutzerinstallation über Velopack, ohne Admin. Skript `tools/messung/c4-setup-test.ps1`.
- Vorher gesichert: `%APPDATA%\TrendoOS` (37 Dateien) und die Installation 0.9.7. Am Ende sind die Daten byte-gleich zurückgeschrieben.
- Beendet wurden nur Trendo-Prozesse, die das Setup im Test selbst gestartet hat.

| Schritt | Ergebnis |
|---|---|
| 1 Deinstallieren 0.9.7 (`Update.exe uninstall --silent`) | Programmordner, Registry-Eintrag (HKCU) und Startmenü-Verknüpfung entfernt; Benutzerdaten unverändert (37 Dateien) |
| 2 Installieren 0.9.6 (`Setup --silent`) | Version 0.9.6, Registry 0.9.6, Startmenü da; App startet mit den echten Daten (erstes Bild nach 12,4 s, direkt nach der Installation) |
| 3 Update auf 0.9.7 (`Update.exe apply -p TrendoOS-0.9.7-full.nupkg`) | Version und Registry 0.9.7, Daten da; App startet (3,7 s) |
| 4 Rückfall: 0.9.6-Setup über 0.9.7 | Version und Registry 0.9.6; die ältere Version liest die neueren Daten und startet (2,5 s) |
| 5 Deinstallieren | Ordner, Registry und Startmenü entfernt; Benutzerdaten bleiben |
| 6 Ausgangszustand | 0.9.7 wieder installiert (Registry 0.9.7, Startmenü und Desktop-Verknüpfung da), Daten byte-gleich wie vorher |

**Ergebnis:** 18 von 18 Prüfungen bestanden („ERGEBNIS: ALLE BESTANDEN“).
**Rohlog:** `reports/c4-setup/c4-setup-test-2026-10-25.txt`, SHA-256 in `reports/c4-setup/SHA256SUMS`. Die Bytes sind per `.gitattributes` unverändert.

## Befunde
- **Benutzerdaten nach dem Deinstallieren:** Sie bleiben liegen, gewollt für ein späteres Neuinstallieren. Eine Option „auch meine Daten löschen“ gibt es nicht. Offene Entscheidung: Soll das Setup danach fragen?
- **Erster Start direkt nach der Installation:** 12,4 s. Das ist derselbe Effekt wie in D7: frisch geschriebene, unsignierte Dateien. Spätere Starts brauchen 2,5–3,7 s. Mit Code-Signatur (C7) wird es voraussichtlich besser; belegt ist das nicht.
- **Update-Weg:** geprüft mit dem lokalen Paket über `Update.exe apply`, nicht über den eingebauten Updater mit GitHub-Quelle. Auf GitHub gibt es kein Release 0.9.7.

## Nicht geprüft
- Update über das Internet (GitHub-Release) und Delta-Pakete.
- Installation auf einem frischen Windows ohne .NET oder andere Programme (IMP-24) und auf einem zweiten Benutzerkonto.
- SmartScreen-Verhalten beim Herunterladen aus dem Internet, weil das Setup nicht signiert ist.
