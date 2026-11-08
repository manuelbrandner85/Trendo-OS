# Zielarchitektur SPEC-12
Trendo-Studio (Avalonia) → vorhandene Programm-Ansicht → Adapter je Auswahl (Prozess/CLI oder WebView), Ergebnisse als Dateien im Projekt, Freigabe vor Installation (GRUNDSATZ-01), Audit-Log.
- VirusTotal/yara-x: BSD-3, Rust-Einzelprogramm (yr): Dateien und Ordner mit YARA-Regeln auf Schadsoftware-Merkmale prüfen – nur lesen, Ergebnis als Befundliste
- reversinglabs/reversinglabs-yara-rules: MIT – geprüfte YARA-Regelsammlung (Ransomware, Trojaner, Hacktools) als Regelquelle für yara-x; Stand 2025-11
