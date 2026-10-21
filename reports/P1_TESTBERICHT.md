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
