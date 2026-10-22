# Verbesserung 42 – Entwicklerführung

Verbesserung 42 ist nicht vollständig implementiert. `ready` steht bei keiner Aktion. Diese Führung sagt, wie ein Adapter dazu kommt und wie eine Wirkung gesperrt bleibt.

## Ort

| Teil | Datei |
|---|---|
| Ablauf, Status, Freigabe | `src/Trendo.Core/PromptAction/StudioWorkflow.cs` |
| Verträge | `src/Trendo.Core/PromptAction/Model.cs` |
| Felder `Feld: Wert` | `src/Trendo.Core/PromptAction/PromptFields.cs` |
| Datei schreiben und lesen | `src/Trendo.Core/PromptAction/FieldKit.cs` |
| Katalog und Matrix | `src/Trendo.Core/PromptAction/CapabilityCatalog.cs` |
| Registrierung | `PromptActions.CreateDefault` in `IntelligencePromptModel.cs` |
| Gesperrte Systemaktionen | `src/Trendo.Core/PromptAction/SystemPromptAdapter.cs` |
| Angeschlossene Dienste | `src/Trendo.Core/PromptAction/ConnectedAdapters.cs`, `PromptServices.cs` |
| Webseiten-Naht | `PageWriteAdapter` implementiert `IWebsitePageAdapter` und ist registriert |
| Leiste | `src/Trendo.App/MainWindow.PromptAction.cs` |
| Tests | `tests/Trendo.Core.Tests/PromptActionTests.cs`, `ConnectedPromptTests.cs` |

## Adapter

Eine Klasse implementiert `IStudioActionAdapter`. `TryParse` liest den Auftrag und gibt `null` zurück, wenn eine andere Aktion desselben Studios gemeint ist. Die erste Treffer-Aktion gewinnt. Die gesperrte Geschwisteraktion steht in `CreateDefault` vor der lokalen Aktion.

`Missing` fragt nur fehlende Pflichtfelder. `Validate` lehnt ungültige Werte ab und rät nichts. `Produce` schreibt nur über `ProjectStore` in das aktuelle Projekt, oder es liefert `FieldKit.Blocked`. `SideEffect` bleibt `FieldKit.Blocked`, außer bei den vier Aktionen mit `CanPerformSideEffect`.

`CanPerformSideEffect` ist nur bei `datei.laden`, `datei.teilen`, `programm.installieren` und `programm.starten` wahr. Der Workflow ruft `ApprovalGate.Execute` nur dann. Sonst endet eine Wirkung mit `Unsupported`. Eine Ablehnung, bevor etwas nach außen lief, gibt den Idempotenzschlüssel frei. Ein angefangener Installations-, Freigabe- oder Programmstart ohne Beleg bleibt `blocked`. Eine Ausnahme lässt den Schlüssel auf `claimed`.

## Reife

| `DeclaredLevel` | Wann |
|---|---|
| `Partial` | `Produce` schreibt eine Datei, die der Test danach liest. Eine Grenze bleibt (Werkzeug fehlt, Wirkung aus). |
| `Blocked` | `Produce` liefert `Supported: false`. Der Test startet die Aktion und erwartet `Unsupported`, leere Artefakte und keine Ersatzdatei. |
| `Ready` | Jede registrierte Aktion des Studios ist `Ready`, und der Beleg ist ein Test, der die Aktion wirklich ausführt. Aktuell keiner. |
| `NotImplemented` | Kein Adapter. Im Katalog aktuell keiner. |

`Evidence` nennt die Testklasse und die Methode, die die Aktion ausführt (`PromptActionTests` oder `ConnectedPromptTests`). Sonst leer.

## Seiteneffekt

Verboten aus dem Auftragstext, auch mit `Freigabe: erteilt`:

- `SitePage.Publish`, `OpenSource.Fetch`, `ExtensionCatalog.InstallVerified`
- `LiveBroadcast.Go`, `WorkflowEngine` erzeugen oder laufen lassen
- Blender, Unreal-Editor, Upload, SMTP, Zahlung, `Ledger`-Buchung
- Theme, zweites Freigabesystem, Forensikfrist, Desktop ändern
- Kamera öffnen, Mikrofon, Roboter, CNC, Explorer, Reparatur, Handy verbinden, Koppeln
- Kostenpflichtige KI, Online-Konten, Language Studio 2.0

Die verbotenen Schlüssel `approval`, `approved`, `consent`, `freigabe`, `sideeffect`, `password`, `apikey` entfernt der Workflow aus den Parametern. `TryParse` darf „Freigabe: erteilt“ nicht als Erlaubnis lesen.

Ein lokaler Adapter setzt `Effect` auf `None`. Hat er ein anderes `Effect` und `CanPerformSideEffect` ist falsch, wird aus einer geschriebenen Datei kein `QA bestanden`: der Lauf endet bei der Wirkung mit `Unsupported`. Die gefährliche Schwesteraktion steht davor und liefert `FieldKit.Blocked`. Die vier freigabepflichtigen Aktionen schreiben den Plan mit `Effect != None` und führen die Wirkung erst in `SideEffect` aus.

## Matrix

`CapabilityCatalog.Markdown(PromptActions.CreateDefault())` muss zeichengleich zu `docs/improvement-42/studio-capability-matrix.md` sein. Der Test `Matrix_deckt_den_Katalog` prüft das. Die Blocker-Spalte kommt aus `StudioProfile.Blocker`, solange nicht jede Aktion des Studios `Ready` ist. Titel und Bereich müssen zu `StudioCatalog` passen.

## Prüfung

`dotnet test Trendo.sln -c Release` vor dem Push. `blocked` ist nicht `passed`. QA-017, QA-019 und die Codesignatur werden nicht als bestanden markiert. Die 120-Stunden-Regel bleibt `ForensicsLab.MaxLife`. Verbesserungen 23, 25, 34 und Language Studio 2.0 bleiben abgelehnt. Verbesserung 35 bleibt offen.
