# Umsetzungsstand (08.11.2026)

| Bereich | Stand |
|---|---|
| Bestandsaufnahme 56 Studios | erledigt |
| Runde 1–9 Recherche/Bewertung (36 Studios) | erledigt |
| CORE-05 f3d-Vorschaubilder | eingebaut, echter Lauf geprüft (reports/alle-studios/f3d-e2e.md), 10 Kern- und 1 Oberflächentest |
| CORE-05/CORE-06 Claude in Blender/Unreal/Godot (C5/C6) | MCP je Projekt + sichtbarer Start eingebaut, 10 Kern- und 1 Oberflächentest; Zusammenspiel mit dem Claude-Chat (Sitzung A, C1–C4) NOT RUN |
| STUDIO-MUSIK-01 basic-pitch (Noten auch mehrstimmig) | eingebaut als KI-Laufzeit „Noten“ (ohne TensorFlow, ONNX), echter Lauf (reports/alle-studios/basic-pitch-e2e.md), 8 Kern- und 1 Oberflächentest grün; Einrichtung per uv NOT RUN |
| SPEC-13 PMTiles (Offline-Rasterkarten) | eigener Leser in TilePack, echter Vergleich mit go-pmtiles bis 87 381 Kacheln byte-gleich (reports/alle-studios/pmtiles-e2e.md), 12 Tests grün |
| CORE-05 assimp (3D → GLB) | eingebaut im 3D-Studio, eigener Prozess --assimp-convert, echter Lauf (reports/alle-studios/assimp-e2e.md), Tests grün |
| CORE-08 MLT (Schnitt als MLT-Projekt, Rendern mit melt aus Kdenlive) | Kern eingebaut, echter Lauf (reports/alle-studios/mlt-e2e.md), 10 Tests grün; Oberfläche im Video-Studio (Sitzung A) NOT RUN |
| SPEC-13 Vektorkarten (MapLibre + eigener PMTiles-Leser) | eingebaut, echter Lauf im Trendo-WebView unter file:// (reports/alle-studios/vektorkarten-e2e.md), Tests grün |
| SYS-10 typst (Brief/Rechnung als PDF) | eingebaut, echter Lauf über den Adapter (reports/alle-studios/runde2-werkzeuge-e2e.md), Tests grün |
| SPEC-15 mediamtx (eigener Sendeserver, nur 127.0.0.1) | eingebaut, echter Lauf mit Teststream, Tests grün; OBS-Lauf NOT RUN |
| STUDIO-TV-01 streamlink (Das Erste/ARD, arte live) | eingebaut, echter Lauf (arte → m3u8), Tests grün; Abspielen im Player NOT RUN |
| SPEC-09 Open3D (Scan glätten/vereinfachen) | eingebaut im Scan-Studio, echter Lauf (reports/alle-studios/open3d-e2e.md), 4 Kern- und 1 Oberflächentest grün; Einrichtung per uv NOT RUN |
| CORE-03 KI-Team: Team-Lauf fortsetzen (Konzept LangGraph, eigener Code) | eingebaut (5d45824cd), 3 Kerntests grün, KI-Team-Oberflächentests grün; echter Lauf mit Claude NOT RUN |
| SPEC-01 IFC-Export (IfcOpenShell) | eingebaut: Exportieren „IFC (BIM)“ + „BIM → IFC einrichten“, echter Lauf mit Vorlage, Geometrie nachgemessen (reports/alle-studios/ifc-e2e.md); Öffnen in BIM-Programmen NOT RUN |
| SYS-05 Node-RED | Anbindung war schon da (N1–N7); echter Lauf mit Trendos Einstellungen; Härtung umgesetzt (Herkunftsprüfung, --ignore-scripts) und echt nachgeprüft (reports/alle-studios/node-red-e2e.md) |
| SPEC-01 IFC-Ansicht (web-ifc/ThatOpen + three.js) | eingebaut: „IFC ansehen“ / „IFC öffnen …“, echter Lauf im Trendo-WebView (reports/alle-studios/ifc-ansicht-e2e.md), Test grün |
| CORE-03 KI-Team: geprüfte Antwortform (pydantic-ai-Konzept, eigener Code) | eingebaut, erster Nutzer „Planer“ (ziel + schritte); Nachfrage nur bei fehlerhaftem JSON, Freitext bleibt unverändert; 10 Tests, alle 46 Agenten-Tests grün; echter Lauf mit Claude NOT RUN |
| Runde 3 übrige (Rollenbeschreibungen nach crewAI) | NOT RUN |
| SPEC-06 FSRS-Planer für Karteikarten (Runde 4) | eingebaut, umschaltbar im Lernplan, 12 Kern- und 1 Oberflächentest grün |
| Runde 4 übrige (SpiceSharp, Math.NET Numerics, Statik-Konzepte) | geplant, NOT RUN – NuGet-Pakete bzw. eigener Code |
| SPEC-10 MQTT-Live-Werte (MQTTnet) | eingebaut, echter Lauf mit Broker auf 127.0.0.1 (reports/alle-studios/mqtt-e2e.md), Tests grün; echte Geräte NOT RUN |
| SPEC-04 glTF-Validator + glTF-Transform | eingebaut im VR-Studio („Prüfen & für Web packen“), echter Lauf: Regal 532 → 105 KB, weiter fehlerfrei (reports/alle-studios/gltf-e2e.md); Einrichten per Knopf NOT RUN |
| SPEC-11 E-Rechnung (XRechnung mit ZUGFeRD-csharp 18) | eingebaut in „Rechnung schreiben“, Datei wird wieder eingelesen und geprüft (reports/alle-studios/erechnung-e2e.md), 9 Kern- und 1 Oberflächentest grün; KoSIT-Validator und Empfang in Fremdprogrammen NOT RUN; Bibliothek ab 1.12.2026 ohne Pflege |
| SYS-09/P16 fremde Kalenderdateien (ical.net 5.2.4) | eingebaut im Kalender („ICS einlesen …“): Regeln mit Ende, Ausnahmen, verschobene Termine, Zeitzonen; echter Vergleich mit Python icalendar/recurring-ical-events – 632 Termine aus 4 Dateien gleich (reports/alle-studios/ics-fremd-e2e.md); 10 Kern-, 1 Echt-, 1 Oberflächentest grün; echte Konto-Exporte NOT RUN |
| SYS-10 Geschäftsbrief nach DIN 5008 (typst, Konzept typst-letter-pro) | eingebaut in der typst-Karte (Form A/B), echter Lauf: typst misst Anschrift, Info und Betreff selbst – im Soll ±0,5 mm (reports/alle-studios/din5008-e2e.md); 5 Kern-, 2 Echt-, 1 Oberflächentest grün; Ausdruck im Fensterkuvert NOT RUN |
| CORE-06 Spiel-Effekte nach sfxr (eigener C#-Code, gemeinfreies Verfahren) | eingebaut in der Geräusch-Werkstatt (7 Vorlagen, Varianten, Bibliothek/Paket), am Ton gemessen: Frequenz, Hüllkurve, Tonhöhenverlauf (reports/alle-studios/sfxr-e2e.md); 9 Kern- und 1 Oberflächentest grün; Hörtest NOT RUN |
| CORE-06 Level als Tiled-Karten (.tmj + Kachelbild + Welt) | eingebaut im Spiel-Editor („Als Tiled-Karten ausgeben“), unabhängig gegengelesen mit pytiled-parser (reports/alle-studios/tiled-e2e.md); 7 Kern-, 1 Echt-, 1 Oberflächentest grün; Öffnen in Tiled/Godot NOT RUN |
| CORE-07 Barrierefreiheit genau prüfen (axe-core 4.14 + jsdom, eigener Prozess) | eingebaut in der Qualitätsprüfung (Einrichten per Klick über npm, Befunde auf Deutsch), 4 Kern- und 1 Oberflächentest grün (reports/alle-studios/axe-e2e.md); echter Lauf grün (8 Befunde, Seiten-Skript lief nicht, 0,8 s) |
| CORE-09 Objekte erkennen und zählen (YOLOX-S, onnxruntime, Prozessor) | eingebaut im Kamera-Bereich (Einrichten per Klick, Modell mit fester SHA-256), 6 Kern- und 1 Oberflächentest grün (reports/alle-studios/yolox-e2e.md); echter Lauf grün (Fahrrad 95 %, Hund 91 %, 217 ms; doppelte Zählung und Beschriftung behoben) |
| SPEC-08 Schnittmuster nach Maß (FreeSewing 4.10.2, 11 Schnitte) | eingebaut in „Kleidung & Produkte“ (Einrichten per Klick über npm, fehlende Maße aus Standardgröße mit Nennung), 5 Kern- und 1 Oberflächentest grün (reports/alle-studios/freesewing-e2e.md); echter Lauf grün, alle 11 Schnitte (undeklarierte FreeSewing-Abhängigkeiten gefunden und behoben) |
| X-07 Symbole (Iconify, Lizenz je Satz) | eingebaut als Reiter „Symbole“ (258 freie Sätze, 18 ausgeblendet), alle 21 000+ Symbole aus 4 echten Sätzen sicher ausgegeben, Vorschau gerendert (reports/alle-studios/iconify-e2e.md); 7 Kern-, 1 Echtdaten-, 1 Oberflächentest grün |
| SPEC-12 Auf Schadsoftware-Merkmale prüfen (yara-x 1.22.0 + 310 ReversingLabs-Regeln) | eingebaut als Werkzeug-Karte, Ausgabeformat aus dem Quellcode von yr 1.22 übernommen, echtes Regel-Archiv (SHA-256) entpackt und geprüft (reports/alle-studios/yara-e2e.md); 6 Kern-, 1 Echtdaten-, 1 Oberflächentest grün; echter Lauf grün (310 Regeln kompiliert, Prüfmarke erkannt; meta-Format von yr korrigiert) |
| CORE-11 Verschlüsselte Sicherung mit Versionen (restic 0.19.1) | eingebaut als Karte in „Daten schützen“ (sichern, Stände, Wiederherstellen in neuen Ordner), JSON-Felder aus dem Quellcode von 0.19.1 (reports/alle-studios/restic-e2e.md); 6 Kern- und 1 Oberflächentest grün; echter Lauf grün (verschlüsselt, entdoppelt, Byte für Byte zurück; Zurückholen ohne Originalpfad korrigiert) |
| CORE-10 Word/PDF/PowerPoint/Excel ins Wissen (markitdown 0.1.9) | eingebaut in „Wissen merken“ (KI-Laufzeit „Dokumente“, Aufrufe aus dem Quellcode von 0.1.9 geprüft) (reports/alle-studios/markitdown-e2e.md); 3 Kern- und 1 Oberflächentest grün; echter Lauf grün (5 Formate; kaputte Dateien abgelehnt, Titel, Umlaute in Python-Meldungen korrigiert) |
| SYS-07 Geräte ohne Namen benennen (pci.ids) | eingebaut: namenlose PCI-Geräte erscheinen jetzt (fielen vorher weg), Hersteller und Modell aus der echten Liste (2524 Hersteller, 21 739 Geräte) (reports/alle-studios/runde9-geraete-einheiten-e2e.md); 5 Kern-, 1 Echtdaten-, 1 Oberflächentest grün |
| SYS-02 Einheiten erweitert (Zeit, Energie, Leistung, Druck, Datenmengen, Winkel) | eigener Code nach exakten Definitionen, UnitsNet und NCalc nach Bestandsprüfung verworfen; 16 Tests grün |
| SYS-06 Ans Handy senden (LocalSend-Protokoll v2, eigener Code) | eingebaut in „Teilen im Heimnetz“, echte HTTP- und UDP-Verbindungen gegen einen Empfänger nach der Protokollbeschreibung (reports/alle-studios/localsend-e2e.md); 14 Kern- und 2 Oberflächentests grün; echtes Handy, HTTPS und WLAN-Multicast NOT RUN |
| Runden 10+ (20 Studios, davon SPEC-07 abgelehnt) | NOT RUN |
| Web Design Studio Modul 05 (Design-Tokens) | eingebaut, 2 Tests, im Browser geprüft (docs/web-design-godmode) |
| Volltest nach den Änderungen | grün: Api 16, Core 1546, App 829 (reports/alle-studios/volltest-2026-11-08.md) |
