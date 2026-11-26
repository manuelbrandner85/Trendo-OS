# T-0776: Fremdprogramm-Nachläufe mit vorhandenen Werkzeugen (Restliste Q3)

**Ziel:** Trendo-Exporte in LibreOffice, FreeCAD 1.1, Krita, Inkscape und 7-Zip öffnen und zurücklesen – mit Rohlog und SHA-256 – und die falschen „nicht installiert“-Aussagen berichtigen.
**Freigabe:** Nutzer, 26.11.2026 (Etappe 0.9.112). Basis main 0.9.111 = 2b1ffa923.
**Branch:** claude/T-0776-fremdnachlauf
**Dateien/Bereich:** neuer Test `tests/Trendo.Core.Tests/T0776FremdnachlaufTests.cs`, Hilfsskripte `tools/echtlauf/t0776/`, Belege `reports/T-0776/`; Fehlerbehebungen nur dort, wo der Nachlauf einen Fehler zeigt; Nachträge in den Berichten T-0682/T-0691/T-0692/T-0694/T-0740/T-0753/T-0754 und im Audit 2026-11-26.
**Fertig, wenn:**
- [x] Werkzeuge nur lesend gefunden (Trendo-Werkzeugordner, Programme-Ordner, PATH) – `reports/T-0776/werkzeuge.txt`
- [x] Text, Geld-XLSX, Shop-CSV in LibreOffice; CAD-STEP, CAD-DXF (und Wohnungs-DXF) in FreeCAD; PSD/ORA in Krita; SVG in Inkscape; Sicherungs- und Export-ZIP in 7-Zip – je Rohlog + SHA-256
- [x] Test „echt“, der ohne Werkzeug „übersprungen“ meldet
- [x] falsche „nicht installiert“-Aussagen in Code, Doku und Berichten berichtigt
- [x] betroffene Testklassen grün (kein Volltest – macht die Hauptsitzung)
**Nicht tun:** nichts installieren; nichts unter `%LOCALAPPDATA%\TrendoOS` schreiben; Fremdprogramme nur mit eigenem Profil im eigenen Temp-Ordner; nur eigene Prozesse beenden; kein Push nach main, kein Merge, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0776-fremdnachlauf.bericht.md`.
