# P1 – Windows-Vertical-Slice: Testbericht
**Stand:** 21.10.2026, 14:37 (Europe/Berlin) · **Commit getestet:** `1cbcbfa` (Branch `p1/windows-slice`) · **Stack:** .NET 10 + Avalonia 12.1.4 (ADR-0005)

## Was gebaut wurde (Status: IMPLEMENTED)
| Baustein | Datei | Hinweis |
|---|---|---|
| Core-Dienst (In-Process, keine Netzwerkschnittstelle) | `src/Trendo.Core/TrendoCore.cs` | Abweichung von Ziel „lokale API“: bewusst noch kein localhost-Endpunkt in P1 |
| Projektisolation | `ProjectStore.cs` | Eigene Pfadwurzel, ID-Regex, `../`/absolut/Laufwerk/Symlink abgewiesen + auditiert |
| Approval-Gate | `ApprovalGate.cs` | Plan → Freigabe → genau eine Ausführung; Ablauf 10 min; Ziel muss übereinstimmen |
| Append-only Audit-Log | `AuditLog.cs` | JSON-Lines mit SHA-256-Hash-Kette, `Verify()` erkennt Manipulation |
| Secrets | `Secrets.cs` | Windows Credential Manager (advapi32), In-Memory-Fallback nur für Nicht-Windows/Tests |
| Studio-Registry | `StudioRegistry.cs` | Liest `requirements.yaml`; fehlendes Programm → Blockiert, kein Adapter → Nicht verbunden |
| Integration Explorer/Browser | `Integrations.cs` | Nur über Approval-Gate; Browser nur `https` |
| UI (deutsch, dunkles Theme) | `src/Trendo.App/` | Start, Alle Studios, Projekte, Freigaben, System & Sicherheit |

## Testergebnisse
| Lauf | Umgebung | Ergebnis | Nachweis |
|---|---|---|---|
| Build | Windows 11 Pro 26H2, .NET SDK 10.0.402, Standardbenutzer | **0 Fehler, 1 Warnung** (veraltete API `Bitmap.Save`) | `p1/windows/build.log` |
| Unit-Tests | Windows (wie oben) | **29/29 bestanden**, 0 übersprungen, 372 ms | `p1/windows/test.log`, `windows.trx` |
| Unit-Tests | Linux-Box, .NET 10.0.402 | **29/29 bestanden** | `p1/box_linux_test.log` |
| App-Start/Ende | Windows, `--screenshot --auto-exit 4` | Exit-Code 0; Audit: core.start → app.screenshot → core.stop, Kette intakt | `p1/windows/audit.jsonl`, `screenshot.png` |

Artefakt-Hashes (Windows-Build): `TrendoOS.exe` SHA-256 `7B2799FAF29F1D3496EEA1A47B6E2CED092FF5E54528EF1F67D8D05BA881712B`, `TrendoOS.dll` SHA-256 `BC3639AAEB2E41F42FCC3FBD7D7B30DFCAF26B0A63AD7C2547EB0DCC86194BDA`.
Log-Hashes: `p1/windows/SHA256SUMS.txt` (Logs sind um Benutzer-/Host-/Pfadangaben bereinigt; Hashes beziehen sich auf die bereinigten Dateien).

## QA-Zuordnung (ehrlich)
| QA | Status | Umfang des Nachweises |
|---|---|---|
| QA-001 Start/Beenden | **TESTED (Windows, Teilumfang)** | Unit-Test + echter App-Start/Ende mit Exit 0. Kein Installer (P1-Exit-Kriterium „Installer“ offen) |
| QA-003 Projektisolation | **TESTED (Unit, Windows)** | 12 Fälle Datei/API. Agentenebene nicht vorhanden. Symlink-Fall auf Windows ohne Admin nicht erstellbar → dort faktisch nicht geprüft (nur Linux) |
| QA-004 Freigaben | **TESTED (Unit, Windows)** | ohne/abgelehnt/Replay/abgelaufen/Zielwechsel/nur https/Audit-Manipulation; Credential-Manager-Roundtrip echt auf Windows |
| QA-015 Studio-Status | **TESTED (Unit, Windows)** + Screenshot: 0 Bereit, 3 Nicht verbunden, 1 Blockiert (LibreOffice fehlt), 49 Noch nicht gebaut, 1 Abgelehnt |
| QA-019 Offline | **TESTED (Unit, Teilumfang)** | Core ohne Netz-Abhängigkeiten; kein echter Lauf mit getrenntem Netzwerk |
| QA-017 Tastatur | NOT_RUN | |
| Echte Explorer/Browser-Öffnung | NOT_RUN | Code vorhanden; in Tests durch Fake-Launcher ersetzt; kein Live-Klick im UI-Test |

## Offene Punkte für P1-Abnahme
Installer/Paket, localhost-API mit Auth, Live-Test Explorer/Browser per Klick, Tastatur-/Screenreader-Test, Projekt-Rollen (RBAC), UI-Automatisierungstests, veraltete API-Warnung beheben.

---
# Nachtrag: P1-Abnahmevorbereitung (21.10.2026, 14:50 Europe/Berlin)
**Commit getestet:** `65ce31d` (Branch `p1/acceptance`) · Windows 11 Pro 26H2, Standardbenutzer, ohne Admin · Nachweise: `reports/p1/acceptance/` (+ `SHA256SUMS.txt`)

## Änderungen
- Build-Warnung behoben (`Bitmap.Save` mit `PngBitmapEncoderOptions`) → **0 Warnungen, 0 Fehler**.
- Paket + Installer pro Benutzer: `installer/build-package.ps1` (self-contained win-x64, ZIP + SHA-256), `install.ps1` (→ `%LOCALAPPDATA%\Programs\TrendoOS`, Startmenü-Verknüpfung, keine Registry, kein Admin), `uninstall.ps1` (Benutzerdaten bleiben, außer `-RemoveData`).
- Tastatur: Strg+1…5 für alle Hauptbereiche, Tab-Reihenfolge, zugängliche Namen (AutomationProperties).
- Offline-Modus `--offline`: Browser-/Netzwerkaktionen im Core abgewiesen, UI-Knopf deaktiviert, Status „OFFLINE“.
- Explorer-Selbsttest `--selftest-open-explorer --approver …`: echter Explorer-Start nur über Approval-Gate mit protokollierter Freigabe.
- Neues Projekt `tests/Trendo.App.Tests` (Avalonia.Headless + xUnit v3).

## Ergebnisse
| Schritt | Ergebnis |
|---|---|
| Build | 0 Warnungen, 0 Fehler |
| Core-Tests | **31/31 bestanden** (Windows) |
| UI-Tests (headless) | **7/7 bestanden** (Windows) |
| Linux-Box | 31/31 + 7/7 bestanden |
| Paket | `TrendoOS-win-x64.zip` 70,8 MB, SHA-256 `cd58607a5ac30c651c704ccef01a5ab23618c368bb22cb89698e2f85efacab99` (`artifact_SHA256SUMS.txt`) |
| Installation pro Benutzer | exe + Startmenü-Verknüpfung vorhanden, Ziel korrekt |
| Installierte App, offline, Screenshot | Exit 0, Statusleiste „OFFLINE“ (`screenshot_installed_offline.png`) |
| Echter Explorer-Start | Freigabe `D15FE2EA690F766B` durch „Manuel (Freigabe über Grok Bot)“ protokolliert, Explorer-Fenster mit Projektordner nachweislich offen (danach geschlossen), Exit 0 |
| Deinstallation | exe und Verknüpfung entfernt |

Hinweis: Audit-Kopien im Repo sind um Benutzerpfade bereinigt, ihre Hash-Kette ist dadurch nicht mehr nachrechenbar; Prüfung „Audit-Kette intakt: True“ erfolgte auf dem PC vor der Bereinigung.

## QA-Status (aktualisiert)
| QA | Status | Umfang |
|---|---|---|
| QA-001 | **TESTED** | Start/Ende aus Build und aus Benutzer-Installation, Exit 0 |
| QA-003 | TESTED (Unit, Windows) | unverändert; Symlink-Fall nur Linux |
| QA-004 | **TESTED** | Unit + UI (Klick → Freigabe → genau eine Ausführung, Ablehnen) + echter Explorer-Start mit Freigabeprotokoll |
| QA-015 | TESTED | unverändert |
| QA-017 | **TESTED (Teilumfang)** | Tastaturnavigation + zugängliche Namen automatisiert; Screenreader (Narrator), Kontrast, Multi-Monitor, Reduced Motion: NOT_RUN |
| QA-019 | **TESTED (simuliert)** | Offline-Modus + blockierter HttpClient; **kein** Lauf mit physisch getrenntem Netzwerk |
| Browser-Öffnung live | NOT_RUN | nur mit Fake-Launcher (UI-Test); kein echter Browser-Start, um keine Webseite ungefragt zu öffnen |

## Noch offen für P1-Abnahme
1. Manuels Abnahme (ACCEPTED erfolgt nur durch ihn).
2. Narrator-/Kontrast-Test (QA-017 Rest), echter Offline-Lauf mit getrenntem Netz (QA-019 real).
3. Lokale API (localhost + Auth) – laut Spezifikation Teil von P1, noch In-Process.
4. Rollen/RBAC im Core; Code-Signatur des Pakets (kein Zertifikat vorhanden).
5. Kosmetik: Navigationsbeschriftung „Strg+5“ abgeschnitten.

---
# Nachtrag: Lokale API + RBAC (21.10.2026, 14:56 Europe/Berlin)
**Commit getestet:** `217d182` (Branch `p1/api-rbac`) · Windows 11 Pro 26H2, Standardbenutzer, ohne Admin · Nachweise: `reports/p1/api-rbac/` (+ `SHA256SUMS.txt`)

## Neu (IMPLEMENTED)
- `src/Trendo.Api`: lokale HTTP-API (Kestrel), **nur 127.0.0.1**, Option `--api [--api-port N]` (Standard 47800). Prüft Loopback-Client, Host-Header (DNS-Rebinding → 421), Bearer-Token.
- Tokens: je Rolle ein 256-Bit-Token, gespeichert nur über `ISecretStore` (Windows: Credential Manager, Ziel `TrendoOS:api-token-<rolle>`), Vergleich in konstanter Zeit, Rotation möglich, nie im Audit-Log.
- RBAC im Core (`Rbac.cs`): Guest = Studios ansehen; User = + Projekte, Aktionen anfordern; Owner = + Freigeben/Ablehnen, Audit. Durchgesetzt im **Approval-Gate** (Anfordern/Freigeben) und in der API; Verstöße als `rbac.denied` auditiert. Desktop-Bediener = lokaler Owner.
- Navigation: Kürzel „Strg+N“ als zweite Zeile, Leiste breiter → nichts mehr abgeschnitten (Screenshot).
- `--api-selftest`: Live-Prüfung über echten Loopback mit echten Tokens.

## Ergebnisse (Windows)
| Schritt | Ergebnis |
|---|---|
| Build | 0 Warnungen, 0 Fehler |
| Core-Tests | **39/39** |
| API-Tests (echter Kestrel auf Loopback) | **14/14** |
| UI-Tests (headless) | **7/7** |
| Summe | **60/60 bestanden** (Linux-Box ebenfalls 60/60) |
| API-Live-Selbsttest | **13/13 PASS** (401 ohne/falsches Token, 403 für Guest/User wo verboten, 201/202/200 erlaubt, 421 fremder Host, Token nicht im Log) |
| App mit `--api` | Listener ausschließlich `127.0.0.1:47800` (Prozess TrendoOS), `/api/studios` ohne Token → 401, health ok, App-Exit 0 |
| Firewall | 0 Regeln für TrendoOS angelegt (Loopback benötigt keine) |

## QA-Status (Änderungen)
- QA-004: **TESTED** inkl. RBAC (Core, API, UI) – nur Owner darf freigeben.
- QA-003: zusätzlich über API (Pfadausbruch → 400).
- Lokale API mit Auth (P1-Pflichtpunkt): **TESTED** auf Windows.

## Hinweise
- Der API-Selbsttest hat auf dem PC echte Tokens im Credential Manager angelegt (`TrendoOS:api-token-guest/user/owner`). Sie bleiben als App-Tokens bestehen; Entfernen per Credential Manager oder `ApiTokens.Rotate`.
- Es gibt noch keine UI zur Token-Anzeige/-Rotation und keine Benutzerverwaltung (Rollen sind tokenbasiert).

## Noch offen für P1-Abnahme
1. Manuels Abnahme.
2. QA-017 Rest (Narrator, Kontrast, Multi-Monitor, Reduced Motion).
3. QA-019 realer Lauf mit getrenntem Netzwerk.
4. Code-Signatur des Pakets (Zertifikat fehlt).
5. Optional: Token-Verwaltung in der UI.

---
# P1-Abnahme
**Status: ACCEPTED** durch Manuel am 21.10.2026, ca. 14:57 (Europe/Berlin), per Chat.
Abgenommen **mit dokumentierten offenen Punkten** (diese bleiben ausdrücklich NICHT bestanden):
- QA-017 Narrator / Kontrast / Multi-Monitor / Reduced Motion: NOT_RUN
- QA-019 realer Offline-Lauf (physisch getrenntes Netz): NOT_RUN
- Code-Signatur des Windows-Pakets: fehlt
