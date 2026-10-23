# Audit T-0202, T-0204, T-0205, T-0206, T-0207, T-0212

- 23.10.2026, Linux-Testrechner, Branch `cursor/audit-studios-a11y-1ded`
- Sechs offene Audit-Aufträge. Katalognamen unverändert. Formeln unverändert. SPEC-07 nicht gebaut. FFmpeg nicht installiert.
- T-0202: jeder sichtbare Knopf. Der Lauf steht in `Audit_Jeder_Knopf.Alle_Seiten` und startet im Gesamtlauf einen eigenen Prozess, weil der Schrift-Zwischenspeicher sonst nach etwa 38 Minuten einen Speicherfehler wirft. Allein: 24 min 27 s, grün.
- T-0204: eigene Spielregeln, Vorschau, Raster, ZIP, Unreal nur mit Hinweis. Untertitel von „Spiele bauen“ ohne Unreal-Versprechen.
- T-0205: Bildbearbeitung und Schnitt. JPEG selbst kodiert.
- T-0206: Editor, Zeilen, Speichern, Vorschau, Überschrift erst zeigen, dann schreiben. Profi-Webseite aus main bleibt daneben.
- T-0207: Auswahllisten, Einheiten, Grundriss, Balken, Karte, Stand je Studio. Untertitel-Vorschläge nur im Bericht.
- T-0212: Schalter Ein/Aus, einfacher Modus, Zeichen ✓ ⚠ ✗, weniger Bewegung.
- `dotnet test Trendo.sln -c Release`: Core 535 bestanden, 10 übersprungen, 0 Fehler, 8 s. Api 15 bestanden, 0 Fehler, 290 ms. App 234 bestanden, 0 Fehler, 1 h 5 min. Exit 0.
- Die zehn Übersprungenen sind `WinRealTests` (Drucker, Bildschirme, qpdf, Autostart, Dateizuordnungen, winget, Defender, SAPI, Rückfall, Papierkorb).
- Nicht geprüft: echter Unreal-Editor, echte Kamera, echter Roboter, echter Drucker, Sprachausgabe, Windows-Zeiger. QA-017, QA-019 und Codesignatur unberührt. Kein PC_VERIFIED.
