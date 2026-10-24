# Trendo OS + Claude Code – Schritt für Schritt (Windows)

1. **Backup** des bestehenden Trendo-OS-Ordners erstellen. Die ZIP-Datei entpacken. Nur die enthaltenen Dateien in den bestehenden Repository-Ordner (`Trendo.sln`) **zusammenführen**; bestehende `README.md`, `docs/spec/`, `src/`, `tests/` nicht überschreiben. Bei Konflikten stoppen und Diff prüfen.
2. PowerShell im Repository öffnen. Prüfen: `git status`, `python --version`, `dotnet --version`, `claude --version`. Python 3.10+ benötigt, sonst über einen vertrauenswürdigen Weg nach Freigabe installieren.
3. `python tools/trendo_ops.py bootstrap` ausführen. Der Befehl erstellt nur `.trendo-local/` und ergänzt `.git/info/exclude`, ohne Remote-Zugriff.
4. `python tools/trendo_ops.py doctor` ausführen; Voraussetzungen und optionale Tools prüfen.
5. `python -m unittest discover -s tests/claude_handoff -v` ausführen. Dies prüft nur lokale Paketkomponenten, nicht Trendo OS.
6. Claude Code im Repository starten: `claude`. Nach dessen Projektvertrauen/Projekt-MCP-Zustimmung `/mcp` prüfen, danach `/agents` und `/trendo-audit` verwenden.
7. Deinen ersten Arbeitsauftrag erteilen: **„Lies `CLAUDE.md` und `docs/claude/GODMODE_2_ARCHITEKTUR.md`. Nutze `/trendo-audit`, dann erstelle mit `/trendo-plan` einen read-only Plan für die nächsten Aufträge. Ändere noch nichts.“**
8. Später reale Arbeit in einem Claude-Branch oder Worktree (niemals main); `dotnet build ...`, `dotnet test ...` nach Änderungen. Die Owner-Freigabe bleibt Pflicht.
9. Optional read-only Dashboard starten: `python tools/trendo_dashboard.py` und lokal `http://127.0.0.1:8765` öffnen. Nur reale Hook- und Jobereignisse werden angezeigt.
10. Persistenten Job anlegen: `python tools/trendo_ops.py job create --title "UI-Audit" --prompt "Untersuche die Avalonia-UI read-only ..."`. `python tools/trendo_ops.py job list` zeigt echten Status. `python tools/trendo_ops.py job run <ID>` ist standardmäßig nur Vorschau. Mit `--execute` erfolgt erst der **ausdrücklich lokal gestartete** CLI-Lauf (Claude muss bereits installiert sein). Kein automatischer Neustart/Weiterlauf nach Ausschalten des PCs.

**HINWEIS:** Die Projekt-Hooks prüfen bekannte Risikomuster, sind keine perfekte OS-Sandbox. Installations-/Netzwerk-/System- und Produktivaktionen weiterhin über separate Freigaben. Das MCP enthält nur read-only Funktionen. Die speziellen Blender-/Unreal-Helfer sind erst nach Installation der Programme verwendbar.
