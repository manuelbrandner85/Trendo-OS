# T-0005 Bericht: Not-Stopp und Änderungsvorschau (IMP-19)

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC) · **Commit:** `d203d7f`

## Umgesetzt
- **`ApprovalGate`** (vorhandenes Gate erweitert, kein zweites System):
  - `Halt(by, grund)`: Offene und erteilte, noch nicht ausgeführte Freigaben verfallen. `Execute` und Freigeben sind gesperrt, `HaltToken` wird abgebrochen, Eintrag `notstopp.aktiv` im Audit-Log. Stoppen darf jeder.
  - `Resume(by)`: nur mit Freigaberecht, Audit `notstopp.aufgehoben`. Verworfene Freigaben bleiben verworfen.
  - `ApprovalRequest.Preview`: die Änderungsvorschau.
- **Abläufe:** Bei aktivem Not-Stopp startet kein Ablauf, laufende brechen zwischen Schritten und Dateien ab, zeitgesteuerte ruhen. „Freigeben …“ erzeugt die Vorschau aus einem Probelauf (Dateiliste).
- **Prompt-Aufträge:** Die Freigabe enthält die Vorschau des Adapters.
- **App** (`MainWindow.NotStopp.cs`):
  - roter Knopf „Not-Stopp“ in der Kopfleiste, nach dem Auslösen grün „Fortsetzen“; die Pause-Taste löst ebenfalls aus;
  - stoppt den laufenden Download, die Freigabe im Netz, Sendung oder Aufnahme und setzt den Roboterarm auf Not-Aus;
  - Freigabeseite: rotes Hinweisfeld mit Zusammenfassung und „Fortsetzen“, Freigeben gesperrt;
  - jede Freigabe zeigt Ziel und „Was passiert:“.
- **Tests:**
  - `T0005_NotStopp.Gate_halt` und `T0005_NotStopp.Workflow_halt_and_preview` (Core)
  - `NotStopp_Ui.Knopf_und_taste` (App)

## Tests (Release, Windows-PC)
Build 0 Warnungen, 0 Fehler · Api 15/15 · Core 668 bestanden, 1 übersprungen (von 669) · App 323 bestanden, 2 übersprungen (von 325), 25 min 16 s · Paket-Tests OK.

## Nicht geprüft / offen
- Not-Stopp aus der Ferne (Handy, IMP-27/T-0012) ist nicht gebaut.
- Fremde Programme, die Trendo gestartet hat (Blender, Unreal, OBS), werden nicht beendet. Der Not-Stopp gilt für Trendos eigene Automationen.
- KI-Antworten, die gerade laufen, werden nicht abgebrochen. Sie haben keine Seiteneffekte.
- Echter Roboterarm: nur der Not-Aus-Zustand im Treiber ist geprüft, keine echte Hardware.
