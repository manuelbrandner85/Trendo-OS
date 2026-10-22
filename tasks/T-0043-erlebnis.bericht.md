# T-0043: Verbesserung 43 im gemeinsamen Ablauf

**Ziel:** Plan, Los, Vorschau, Fassungen und Einfach/Profi in den Prompt-zu-Auftrag-Kern. Webseiten-Punkte nur als Schnittstelle.
**Branch:** cursor/v42-erlebnis-fa7f
**Dateien:** `StudioWorkflow`, `MainWindow.PromptAction.cs`, `WebsiteStudio.cs`, Tests, `docs/improvement-43/website-adapter.md`.

## Testergebnis

- `dotnet test Trendo.sln -c Release`: Api 15 bestanden, Core 520 bestanden und 10 übersprungen, App 211 bestanden, Fehler 0.
- Davon PromptActionTests: 25 bestanden. Leiste, 3D-Vorschau, Ordnerklick und Knopfprobe: 8 bestanden.

## Offene Punkte

- Kein Studioadapter schreibt in der Oberfläche schon eine Datei. Der Kern erzwingt die Datei, sobald ein Adapter `Los` ausführt.
- Punkte 3–8 der Webseite sind nicht gebaut. Vertrag: `IWebsitePageAdapter`.
- Der angefangene Briefadapter liegt nur lokal im Stash `wip briefadapter` und ist nicht Teil dieses Branches.

## Nicht geprüft

- Kein Windows-PC, keine Codesignatur, kein QA-017, kein QA-019.
- Kein Pixel-Streaming, kein Three.js, kein Blender-Export in eine Webseite.
