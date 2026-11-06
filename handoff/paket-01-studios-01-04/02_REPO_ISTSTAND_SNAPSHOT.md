# GitHub-Snapshot und Re-Verify-Verpflichtung

Quellprüfung 2026-11-05, öffentliches Repository `https://github.com/manuelbrandner85/Trendo-OS`, `main`-Baum mit zum Prüfzeitpunkt gemeldetem Tree-SHA `4768bf377af5325eaa5a943515eadf541d83cc77`.

Bekannte bestätigte Pfade:
- `CLAUDE.md` – 20 Claude-Code-Agenten, Freigaben, kein Rewrite, keine Duplikate.
- `docs/claude/AUSBAU_PLAN_2026-11-05.md` – bestehende parallele Ausbauarbeiten, etwa Studio-Fachprogramme.
- `src/Trendo.App/StudioCatalog.cs` – u.a. `CORE-05`, `CORE-06`, `CORE-08`, `STUDIO-MUSIK-01`.
- `src/Trendo.Core/PromptAction/GamePromptAdapter.cs` – Browser-Spielvorlage und Unreal-.uproject-Generator; Editorstart zur Prüfzeit gesperrt.
- `src/Trendo.App/MainWindow.ProgGame.cs`, `MainWindow.ProgGamePro.cs`, `MainWindow.ProgGameRelease.cs`, `MainWindow.W4Game.cs` – bestehende Spiele-Oberfläche.
- `src/Trendo.App/MainWindow.Studio3DPro.cs`, `MainWindow.Studio3DViewport.cs`, `MainWindow.W4Studio3D.cs` – native 3D-Oberfläche.
- `src/Trendo.App/MainWindow.MusikStudio.cs` – lokale Musikgenerierung/ACE-Step; `src/Trendo.App/MainWindow.AudioEditor.cs` – Audioeditor; `MainWindow.Bildbearbeitung.cs` und `MainWindow.VideoSchnitt.cs`.
- `docs/programme/spiele/IMPLEMENTATION_STATUS.md`, `docs/programme/3d/IMPLEMENTATION_STATUS.md`, `docs/programme/kreativ-medien/IMPLEMENTATION_STATUS.md`, `docs/programme/musik-studio/IMPLEMENTATION_STATUS.md` – teils ältere Implementierungsberichte, kein Ersatz für aktuelle Tests.
- `.claude/agents/01-architecture-lead.md` bis `20-release-compliance.md`.

Im historischen Report `docs/programme/spiele/IMPLEMENTATION_STATUS.md` waren SP-12 und SP-13 noch geplant; im neueren `docs/claude/vorbilder/spiele-godot-gdevelop.md` sind sie als vorhanden dokumentiert. **Widerspruch: vor Neuimplementierung echten HEAD + Tests prüfen**.

Kein lokaler Trendo-Build wurde in dieser ChatGPT-Umgebung ausgeführt, kein Zugriff auf die Unity/Unreal/Blender-Installation des PCs. `APPROVED_SPEC` ist lediglich Nutzerentscheidung.
