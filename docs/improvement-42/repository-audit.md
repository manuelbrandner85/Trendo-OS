# Verbesserung 42 – Bestandsaufnahme (Phase A)

Stand der Prüfung: Commit `bbbfd5d7656fa73ad52b4d381afa9980bc34d30f` auf `main` (`docs: Verbesserung 42 … (#238)`). Branch dieser Umsetzung: `cursor/v42-kern-fa7f`. Verbesserung 42 ist im Auftrag `docs/spec/IMPROVEMENT_42.md` vom Eigentümer freigegeben. Das ist kein Eintrag in `docs/spec/decisions.yaml` und kein Beleg, dass die Funktion fertig ist.

## Was im Repository wirklich liegt

- Lösung: `Trendo.sln`. Projekte: `src/Trendo.Core`, `src/Trendo.App` (Avalonia), `src/Trendo.Api`, `src/Trendo.Companion`. Tests: `tests/Trendo.Core.Tests`, `tests/Trendo.App.Tests`, `tests/Trendo.Api.Tests`.
- Bauen: `dotnet build Trendo.sln -c Release`. Testen: `dotnet test Trendo.sln -c Release`. Es gibt kein `.github/workflows`.
- `src/Trendo.App/StudioCatalog.cs`: genau 54 Einträge in `ById`. Titel und Bereiche stehen in `docs/improvement-42/studio-capability-matrix.md`. Ein App-Test vergleicht Katalog und Matrix.
- Studios kommen in der Oberfläche über `MainWindow.W45.cs` (`StudioPages`) und `MainWindow.Sys13.cs` (`BasePageModules`). Briefseiten (`brief`, `akten`, `akte`, `ausgabe`, `briefkoepfe`) gehören zu `STUDIO-LETTER-01`.

## Wiederverwendete Bausteine

| Auftrag nennt | Liegt wirklich unter |
|---|---|
| StudioCatalog | `src/Trendo.App/StudioCatalog.cs` |
| PromptLibrary | `src/Trendo.Core/W2/Prompts.cs` (`brief-formulieren` ist Vorlage Version 1) |
| Intel.AskAsync | `IntelligenceCore.AskAsync` in `src/Trendo.Core/W2/IntelligenceCore.cs`, Oberfläche `MainWindow.W2.cs` |
| Freigabe | `src/Trendo.Core/ApprovalGate.cs` (10 Minuten, einmal, Ziel muss stimmen) |
| Audit | `src/Trendo.Core/AuditLog.cs` |
| Geheimnisse | `src/Trendo.Core/Secrets.cs` (`InMemorySecretStore` auf Linux) |
| Werkzeugprüfung | `src/Trendo.Core/ToolCheck.cs` |
| 3D / Blender | `src/Trendo.Core/Studio3D.cs`, `Scene3D.cs`, `Mesh3D.cs` |
| Forensik 120 Stunden | `ForensicsLab.MaxLife` und `WarnBefore` in `src/Trendo.Core/W3/Forensics.cs` |
| Projektgrenze | `src/Trendo.Core/ProjectStore.cs` |
| Briefe | `src/Trendo.Core/Letters.cs`, `LetterCase.cs`, `LetterArchive`, `PdfExport` |
| Post | `src/Trendo.Core/W3/Mail.cs` (`MailCenter` schreibt `.eml`, sendet nicht) |
| Tabellen | `Ooxml` in `src/Trendo.Core/Sys/Sys10.cs` |
| Geld | `src/Trendo.Core/Finance.cs`, `BusinessStudio.cs` |
| Software | `src/Trendo.Core/DevStudio.cs` |
| Spiele | `src/Trendo.Core/GameStudio.cs` |
| Medien | `src/Trendo.Core/MediaStudio.cs` |

## Entscheidungen, die bleiben

Aus `docs/spec/decisions.yaml`, unverändert: IMP-23, IMP-25, IMP-34 und LANG-2.0 sind `REJECTED`. IMP-35 ist `UNDECIDED`. IMP-41 ist `APPROVED`. SPEC-07 bleibt die abgelehnte Aufwertung; das Basisstudio „Texte übersetzen“ bleibt im Katalog. W1-Issues und W7-Issues werden hier nicht geschlossen. QA-017, QA-019 und Codesignatur werden nicht als bestanden markiert.

## Werkzeuge auf diesem Linux-Image

Vorhanden im PATH: `dotnet`, `ffmpeg`, `ffprobe`, `git`, `python3`. Nicht im PATH: `blender`, `soffice`, Unreal. PDF-Export über LibreOffice, Blender-Render und Unreal-Projektbau können hier nicht als gelungen gelten.

## Ausgangsbau vor diesem Auftrag

`dotnet build Trendo.sln -c Release` auf `bbbfd5d` war grün (0 Fehler). Bekannte Warnungen, nicht von diesem Auftrag: CS9113 in `W7Tests`, xUnit2029/2013/2031 in `LetterQaTests`/`W2Tests`, CS8669 in `StudioIcons`, CS0108 `MainWindow.Focus`, CS0618 `TextBox.Watermark`, CS8602 in `BusOpsUiTests`, xUnit1051. `dotnet test Trendo.sln -c Release` auf demselben Stand: Api 15 bestanden, Core 488 bestanden und 10 übersprungen (`WinRealTests`), App 203 bestanden.

## Abgrenzung zum parallelen Desktop-Auftrag

PR #237 (`cursor/ui41-sm-01-startmenu-3c36`) ändert Startmenü, Fenster und Shell. Diese Phase fasst die Auftragsleiste in `SafeView` (`MainWindow.Sys13.cs`) und lässt die Seite `start` aus. Die 3D-Seite ist die einzige Seite, die die Resthöhe füllt (`FitPage`). Dort hängt die Leiste im linken Rollbereich von `MainWindow.W4Studio3D.cs`, weil die Vorschau bei 1366×768 sonst unter 48 px fällt. `MainWindow.axaml.cs`, `MainWindow.W1.cs`, `MainWindow.W45.cs`, `MainWindow.Windows.cs`, `MainWindow.W1Letter.cs`, `MainWindow.W1Office.cs`, `MainWindow.Look.cs` und `tools/Trendo.Render/Program.cs` bleiben unangetastet.
