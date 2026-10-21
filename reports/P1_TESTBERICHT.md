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
