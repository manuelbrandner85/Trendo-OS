# Trendo – Dokumente (Word, PDF, PowerPoint, Excel, HTML) in Markdown umwandeln mit markitdown (Microsoft, MIT), eigener Prozess.
# Aufruf: python trendo_markdown.py <repo (unbenutzt)> <eingabe> <ausgabe.md>
# Nur lokale Dateien (keine Adressen), Erweiterungen (Plugins) aus, höchstens 50 MB. Ausgabe (stdout, eine Zeile JSON, nur ASCII): Zeichen, Titel.
import json
import sys
from pathlib import Path

ALLOWED = {".pdf", ".docx", ".pptx", ".xlsx", ".html", ".htm"}


def main() -> int:
    if len(sys.argv) != 4:
        print("Aufruf: trendo_markdown.py <repo> <eingabe> <ausgabe.md>", file=sys.stderr)
        return 2
    src, out = Path(sys.argv[2]), Path(sys.argv[3])
    if not src.is_file() or src.suffix.lower() not in ALLOWED:
        print("Nur lokale Word-, PDF-, PowerPoint-, Excel- oder HTML-Dateien.", file=sys.stderr)
        return 2
    if src.stat().st_size > 50 * 1024 * 1024:
        print("Datei zu groß (über 50 MB).", file=sys.stderr)
        return 2
    from markitdown import MarkItDown

    try:
        result = MarkItDown(enable_plugins=False).convert_local(str(src))
    except Exception as e:  # beschädigte, verschlüsselte oder fremde Datei
        print(f"Nicht lesbar: {type(e).__name__}: {e}", file=sys.stderr)
        return 3
    text = (result.text_content or "").strip()
    if not text:
        print("Kein Text gefunden (z. B. nur gescannte Bilder).", file=sys.stderr)
        return 4
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(text + "\n", encoding="utf-8")
    print(json.dumps({"zeichen": len(text), "titel": (result.title or "")[:200]}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
