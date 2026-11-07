# Lizenz- und Sicherheitsprüfung
- Ausgeliefert im Bündel: 78 npm-Pakete, alle freizügig (MIT/ISC/Apache-2.0/BSD) – Liste `src/Trendo.App/Assets/webdesign/LIZENZEN.md`, volle Texte `LIZENZEN.txt` (wird auch in jede exportierte Seite kopiert).
- Ausgeschlossen: Webstudio (AGPL), Theatre Studio (AGPL), GrapesJS Studio SDK (kommerziell), Onlook-Cloud-Anbindungen.
- Fremde Repos nur als Klon in %TEMP% mit `core.hooksPath=/dev/null`, nichts daraus ausgeführt; npm nur mit `--ignore-scripts`.
- Laufzeit: kein Netz, kein CDN; Editor über `file://`. Brücke nimmt nur `bereit`/`speichern` an; Seitendaten werden geprüft (JSON, root/content, Typen, max. 8 MB).
- Schutz der fertigen Seite: `javascript:`/`data:`/`vbscript:` in href/src → `#`; `</` in den Daten wird entschärft (kein Ausbruch aus dem Skript); Titel HTML-maskiert.
