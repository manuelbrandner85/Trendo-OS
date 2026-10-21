# Abhängigkeits-Manager – PC-Test 21.10.2026 (ohne Admin)
- Setup 0.4.0 still installiert (ExitCode 0, HKCU 0.4.0) – `install.log`
- Erkennung + Funktionsprüfung (`--deps-status`): Git 2.55 ✓ (gestartet), Blender 5.2.1 ✓ (`blender --version`), Unreal 5.8 ✓ (nur Dateiversion, Editor nicht gestartet), Python ✗ („Broken“ = Microsoft-Store-Platzhalter; seit diesem Stand als „fehlt“ gewertet) – `deps-before.txt`
- Echte Bedarfsinstallation **Python 3.13.15** per `winget --scope user`, Freigabe über Approval-Gate protokolliert, 27 s, ohne Admin, danach `python --version` ✓ – `install-python.txt`, `audit-deps.txt`
- Bestehende Software unverändert: Blender 5.2.1, Epic Launcher 1.3.193, Visual Studio 18.10.4, Git 2.55.0.3 vorher = nachher (`baseline-before.txt` / `baseline-after.txt`). Neu: Python 3.13.15 + Python Launcher (Teil des Python-Pakets).
- Tests PC: Core 45/45, API 14/14, UI 16/16 (`pc-tests.txt`)
- winget-Scope-Abfrage je Werkzeug: `winget-scope.txt` → `docs/DEPENDENCY_FEASIBILITY.md`
- Nicht ausgeführt: Ollama + Modell (~1,5 GB) echt installieren, UAC-Installationen, Schlüssel-Live-Prüfung mit echtem Schlüssel, Klick-Ablauf im Studio per Hand (nur Headless-Tests).
