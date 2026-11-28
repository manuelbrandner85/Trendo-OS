# Sicherheits-Werkzeuge & Beweise – Testergebnisse

Studio: System auf Gefahren prüfen (SPEC-12) · Bereich: System & Sicherheit · Seite: `forensik` · Stand: 0.9.114 (Stand 99cbb5c)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 11 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| SPEC-12 UI: Prüfsumme vergleichen, Fall anlegen, Beweis sichern, Bericht | ✓ bestanden |
| SPEC-12: Beweis wird unverändert mit SHA-256 und Kette gesichert, Veränderung wird erkannt | ✓ bestanden |
| SPEC-12: Höchstens 120 h, Warnung 24 h vorher, danach vollständig gelöscht | ✓ bestanden |
| T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“ | ✓ bestanden |
| T-0717 Forensik: bleibt außerhalb der Notsicherung – keine Sicherung, kein Anschluss; Fälle nur atomar (120-Stunden-Regel unverändert) | ✓ bestanden |
| T-0755 SPEC-12 (echt): Autostart-Ordner mit echten Verknüpfungen und einer unsignierten Temp-Datei – Ampel je Eintrag, Bericht HTML und CSV | ✓ bestanden |
| T-0755 SPEC-12 (echt): Signaturprüfung – Windows-Datei gültig von Microsoft, eingebettete Signatur gültig, unsignierte Datei erkannt, veränderte Datei ungültig | ✓ bestanden |
| T-0755 SPEC-12 (echt, nur lesend): Prüfung dieses PCs – Run-Schlüssel, geplante Aufgaben und installierte Programme werden gelesen und bewertet | ✓ bestanden |
| T-0755 SPEC-12 UI (echte Dateien, echte Signaturprüfung): „System prüfen“ zeigt Ampel, Datei, Herausgeber, Erklärung; Filter; Bericht als HTML/CSV; nichts wird verändert | ✓ bestanden |
| T-0755 SPEC-12: Ampel-Regeln – grün nur gültig + bekannt, gelb unsigniert/unbekannt/Skript, rot bei fehlender Datei, ungültiger Signatur, %TEMP% oder verstecktem Befehl | ✓ bestanden |
| YARA-Karte: ohne Einrichtung Einrichten-Knopf; mit yara-x Treffer als Warnung mit deutscher Art; nichts wird verändert; fehlender Ordner verständlich | ✓ bestanden |
