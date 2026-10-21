# ADR-0005: Tech-Stack für Core und UI (Windows-Slice)
**Status:** PROPOSED · **Datum:** 2026-10-21

## Kontext
Der Stack muss Windows jetzt und später Linux (SSD-Edition) und ARM (Pi) tragen, offline laufen und ohne kostenpflichtige KI-API auskommen.

## Optionen
| Option | Pro | Contra |
|---|---|---|
| A: Rust-Core + Tauri (Web-UI) | plattformübergreifend, klein, ARM-fähig, gute Sicherheitsmodelle | Rust-Toolchain nötig, WebView2 auf Windows |
| B: .NET 8 Core + WinUI 3 | sehr gute Windows-Integration | WinUI nicht auf Linux/Pi; zweite UI nötig |
| C: .NET Core + Avalonia UI | plattformübergreifend, eine Sprache | weniger native Windows-Optik |
| D: Python-Core + Qt (PySide6) | schnell prototypisierbar, KDE/Qt-nah für Linux | Packaging/Signierung aufwendiger, Performance |

## Vorläufige Empfehlung
Option A (Rust + Tauri), weil sie alle drei Editionen mit einem Core und einem UI-Designsystem abdeckt. **Nicht entschieden**: Entscheidung erst, wenn TASK-0001 die reale Toolchain am PC zeigt und ein kleiner Prototyp (Startzeit, Speicher, Packaging) gemessen ist. Nutzerabnahme erforderlich.
