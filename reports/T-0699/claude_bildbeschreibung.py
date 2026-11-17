"""T-0699 Beleg: Bildbeschreibung über Claude Code mit dem eigenen Abo – derselbe Weg wie „Bilder erkennen“ ohne lokales Bildverstehens-Modell.

Ablauf wie in MainWindow.VisionDescribe → ClaudeKiBackend: Bild als JPEG in einen eigenen Arbeitsordner kopieren, Claude Code im Frage-Modus
(ClaudeCode.Args, Phase.Ask: --restricted, --strict-mcp-config, Datei-Änderungen gesperrt) mit der Frage VisionTags.DescribeQuestion starten,
Antwort aus stream-json lesen. Aufruf: python -I claude_bildbeschreibung.py <bild> <ausgabe.log>
"""
import json
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

QUESTION = ("Beschreibe dieses Foto sachlich auf Deutsch in höchstens zwei Sätzen: was ist zu sehen, wo, welche Stimmung. "
            "Nenne keine Namen von Personen und rate keine Identität. Schreibe danach eine eigene Zeile, die mit „Schlagworte:“ beginnt, "
            "mit 5 bis 10 deutschen Schlagworten (Einzahl, durch Komma getrennt), z. B. „Schlagworte: Hund, Wiese, Ball, Sommer, draußen“.")
ASK_RULE = "Du bist die Haupt-KI in Trendo OS. Antworte auf Deutsch, klar und hilfreich. Ändere keine Dateien."


def main() -> int:
    image, out = Path(sys.argv[1]), Path(sys.argv[2])
    claude = shutil.which("claude")
    lines = [f"Bild: {image.name} ({image.stat().st_size} Bytes)"]
    if claude is None:
        lines.append("BLOCKIERT: Claude Code ist nicht installiert.")
        out.write_text("\n".join(lines) + "\n", encoding="utf-8")
        return 2
    work = Path(tempfile.mkdtemp(prefix="trendo-claude-frage-"))
    try:
        name = "bild-t0699.jpg"
        shutil.copy(image, work / name)
        prompt = f"Lies zuerst die Bilddatei „{name}“ im Arbeitsordner mit dem Read-Werkzeug. {QUESTION}"
        args = [claude, "-p", prompt, "--output-format", "stream-json", "--verbose", "--restricted", "--strict-mcp-config",
                "--append-system-prompt", ASK_RULE, "--disallowedTools", "Edit Write NotebookEdit MultiEdit"]
        t = time.time()
        p = subprocess.run(args, cwd=work, capture_output=True, text=True, encoding="utf-8", timeout=300)
        lines.append(f"claude Exitcode {p.returncode}, Dauer {time.time() - t:.1f} s")
        result, tools = None, []
        for raw in p.stdout.splitlines():
            try:
                ev = json.loads(raw)
            except json.JSONDecodeError:
                continue
            if ev.get("type") == "assistant":
                for c in ev.get("message", {}).get("content", []):
                    if c.get("type") == "tool_use":
                        tools.append(c.get("name"))
            if ev.get("type") == "result":
                result = ev.get("result")
        lines.append("Werkzeuge: " + ", ".join(tools))
        lines.append("Antwort:")
        lines.append(result or "(keine Antwort) " + p.stderr[-500:])
        out.write_text("\n".join(lines) + "\n", encoding="utf-8")
        return 0 if result else 1
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
