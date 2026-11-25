# T-0767 – Belege „Handy verbinden: Datei senden, Entfernen sperrt, Lauf aufzeichnen“

Stand 24.11.2026, Zweig `claude/T-0767-handy` (Basis main = 0.9.109).

| Datei | Inhalt | echt / ersetzt |
|---|---|---|
| `browser-lauf.log` | Rohlog des Ende-zu-Ende-Laufs (Test `PhoneSendT0767Tests.Real_browser_end_to_end`): echter Handy-Zugang (Kestrel) auf der Heimnetz-Adresse dieses PCs, echtes Chrome headless als „Handy“ (eigenes Profil, eigener Prozess, per PID beendet). Kopplung über den QR-Link, Datei hoch (SHA-256 im Browser gebildet), Datei runter (Browser-Download), Entfernen → 401 | echt; das **Handy ist ein Desktop-Browser**, kein Android-Gerät |
| `handy-lauf-*.md` / `.jsonl` / `.sha256` | Ausgabe von „Lauf aufzeichnen“ aus demselben Lauf (anonymisiert: „Gerät 1“, Netzart statt Adresse, keine Datei- und Gerätenamen) | echt |
| `adb-pruefung.log` | Kabel-Weg: das auf diesem PC schon vorhandene adb (nichts installiert) – `adb version`, `adb devices -l` (kein Handy angeschlossen → leere Liste), danach eigener adb-Server beendet | echt, ohne Handy |
| `SHA256SUMS.txt` | Prüfsummen aller Dateien hier | – |

**Steht aus:** der echte Lauf mit einem Android-Handy (WLAN, optional Kabel). Den macht der Nutzer mit „Lauf aufzeichnen“; das Protokoll landet in Trendo im Ordner „Handy-Protokolle“ und kann hierher kopiert werden.
Nicht geprüft: Windows-Firewall-Abfrage bei Zugriff von einem anderen Gerät (beim Test kam der Browser vom selben PC; lokaler Verkehr wird von der Firewall nicht geblockt).
