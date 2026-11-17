# T-0713: Freigaben-Nachtrag (offene Punkte aus B4 / T-0706)

**Ziel:** Die offenen Punkte aus „Freigaben in den Kern“ schließen: Sammel-Freigabe für Ersteinrichtung, „alle fehlenden installieren“ und „Reparieren“; Ablaufprüfung in Decide/Execute; Entscheidung zur Persistenz offener Anfragen; PIN-Hinweis; weitere `Principal.LocalOwner`-Stellen mit Wirkung.
**Branch:** claude/T-0713-freigaben-nachtrag (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** `src/Trendo.Core/ApprovalGate.cs`, `Integrations.cs`, `Profile.cs`, `TrendoCore.cs`, `W3/Packages.cs`, `PromptAction/StudioWorkflow.cs`; `src/Trendo.App/MainWindow.{Freigabe,Setup,axaml,Lock,Look,NotStopp,AppCenter,Share,Projektmappe,PromptAction}.cs`; Tests; `tools/Trendo.Render` (Demo-Schalter).
**Fertig, wenn:**
- [x] Eine Sammel-Rückfrage (Liste mit Wirkung, Lizenz, Größe, Herkunft, je Eintrag abwählbar) → je Komponente genau eine Freigabe, Ausführung nur im Tor; Kinderprofil darf nicht
- [x] Decide/Execute prüfen das Ablaufdatum (ApprovalException + Audit)
- [x] Persistenz entschieden und umgesetzt (atomar, Ablauf beim Laden)
- [x] PIN-Hinweis-Karte in den Einstellungen mit „PIN festlegen“
- [x] LocalOwner-Stellen mit Wirkung umgestellt, Rest aufgelistet
- [x] Core-Tests, UI-Test mit echtem Dialog, FreigabenKernUi grün (Architekturtest verschärft), berührte Seiten, Funktions-Baseline, Sicherheitsprüfung
**Nicht tun:** `MainWindow.ProgramShell.cs` und Programm-Dateien (parallel T-0710, T-0711); kein Volltest.
**Am Ende:** Branch pushen, Bericht `tasks/T-0713-freigaben-nachtrag.bericht.md`.
