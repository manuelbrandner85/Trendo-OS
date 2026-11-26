# Sicherheits-Werkzeuge & Beweise – Testplan

Studio: System auf Gefahren prüfen (SPEC-12) · Bereich: System & Sicherheit · Seite: `forensik` · Stand: 0.9.110 (Stand 0eb9d52)

11 Tests sprechen dieses Programm an.

- SPEC-12 UI: Prüfsumme vergleichen, Fall anlegen, Beweis sichern, Bericht
- SPEC-12: Beweis wird unverändert mit SHA-256 und Kette gesichert, Veränderung wird erkannt
- SPEC-12: Höchstens 120 h, Warnung 24 h vorher, danach vollständig gelöscht
- T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“
- T-0717 Forensik: bleibt außerhalb der Notsicherung – keine Sicherung, kein Anschluss; Fälle nur atomar (120-Stunden-Regel unverändert)
- T-0755 SPEC-12 (echt): Autostart-Ordner mit echten Verknüpfungen und einer unsignierten Temp-Datei – Ampel je Eintrag, Bericht HTML und CSV
- T-0755 SPEC-12 (echt): Signaturprüfung – Windows-Datei gültig von Microsoft, eingebettete Signatur gültig, unsignierte Datei erkannt, veränderte Datei ungültig
- T-0755 SPEC-12 (echt, nur lesend): Prüfung dieses PCs – Run-Schlüssel, geplante Aufgaben und installierte Programme werden gelesen und bewertet
- T-0755 SPEC-12 UI (echte Dateien, echte Signaturprüfung): „System prüfen“ zeigt Ampel, Datei, Herausgeber, Erklärung; Filter; Bericht als HTML/CSV; nichts wird verändert
- T-0755 SPEC-12: Ampel-Regeln – grün nur gültig + bekannt, gelb unsigniert/unbekannt/Skript, rot bei fehlender Datei, ungültiger Signatur, %TEMP% oder verstecktem Befehl
- YARA-Karte: ohne Einrichtung Einrichten-Knopf; mit yara-x Treffer als Warnung mit deutscher Art; nichts wird verändert; fehlender Ordner verständlich
