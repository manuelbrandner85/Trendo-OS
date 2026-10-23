# Trendo OS – MASTER: Alle bisher bestätigten Verbesserungen bis einschließlich Nr. 94

**Stand: 22. Oktober 2026 · Konsolidierte Planung und Übergabegrundlage für die Entwicklung**

> **WICHTIG:** Diese Masterdatei sammelt ALLE im bekannten Planungsstand bestätigten Verbesserungen und nennt abgelehnte bzw. offene ausdrücklich. Sie ist **kein Nachweis für Implementierung oder Tests**. Für Nr. 1–40 sowie Teile von Nr. 41–92 sind die ursprünglichen vollständigen Funktionskataloge hier nicht komplett im Wortlaut verfügbar; deshalb wird keine erfundene Detailvollständigkeit behauptet. Die jeweiligen vorhandenen Original-Spezifikationen bleiben zusätzlich verbindlich und müssen für die Implementierung beigezogen werden.

## 1. Beschlussstatus – verbindliche Abgrenzung

- **JA (bestätigt):** ursprüngliche Verbesserungen **1–22, 24, 26–33, 36–40**, danach **41–85**, **87–94**.
- **NEIN (nicht umsetzen):** ursprüngliche Verbesserungen **23, 25, 34**, gesondertes **Language Studio 2.0**, sowie **Verbesserung 86 (Customer Support, CRM & Service Desk GODMODE)**.
- **OFFEN:** ursprüngliche **Verbesserung 35**; keinesfalls stillschweigend aktivieren.
- Die **120-Stunden-Forensikregel** bleibt **unverändert**. Ohne exakte Originaldefinition darf sie nicht durch eine neue Regel ersetzt werden.
- Nummerngleichheiten zwischen früheren „ursprünglichen Verbesserungen“ und den ab Nr. 41 folgenden Studio-GODMODE-Erweiterungen sind im bisherigen Beschlusskontext zu interpretieren. Insbesondere das NEIN zu ursprünglicher 34 darf nicht als Ablehnung der später ausdrücklich bestätigten Studios verstanden werden.

## 2. Globale Architektur- und Qualitätsregeln

- Bestehendes GitHub-Projekt `manuelbrandner85/Trendo-OS` zuerst analysieren; .NET 10/Avalonia-/Projektarchitektur beibehalten, sofern eine freigegebene, getestete Änderung nicht nötig ist.
- **Ein ONE Workspace pro Studio:** Prompt, Projektkontext, bearbeitbare Artefakte, Live-/Dateivorschau, Änderungswünsche, QA und Export an einem Ort.
- **Keine zweite KI-, Agenten-, Asset-, Projekt-, Workflow-, Monitoring- oder QA-Engine**; wiederverwendbare Fachadapter statt Konkurrenzsysteme.
- Jede Studioaktion durch `Prompt → geprüften Auftrag → notwendige Rückfragen → Fachadapter → echtes Artefakt/Ergebnis → Test → nachvollziehbare Freigabe` führen.
- Statuswerte strikt trennen: PROPOSED / APPROVED_SPEC / PLANNED / IN_PROGRESS / IMPLEMENTED / TESTED / ACCEPTED / RELEASED; eine JA-Planungsentscheidung bedeutet nicht fertig.
- Keine erfundenen Dateien, Messwerte, Datenquellen, Hardwaretests, Builds, Kontakte, Modellläufe oder Verfügbarkeiten; Produktivstatus nur mit Beleg.
- AION und GENESIS dauerhaft getrennte Spiele und Arbeitsverzeichnisse; genehmigte Story-/Script-/Reference-Locks gelten fort.
- Optionale Programme und Adapter (Blender, Unreal, ROS 2 usw.) nur bei vorhandener Installation, unterstützter Version, Berechtigung und erfolgreicher Verbindungsprobe.
- Sensible externe Aktionen – Veröffentlichung, Zahlung, Nachrichtenversand, Produktionsdeployment, Maschinen-/Gerätesteuerung, Löschung, rechtlich bindende Vorgänge – benötigen eigene passende Freigabe und technische Sicherheitsbarrieren.
- Anfängerfreundlich, deutschsprachig, verständlicher Assistent; erweiterte Fachkonfiguration nur optional.
- Datenminimierung, Projekt-/Mandantentrennung, Geheimnisschutz, Versionsverwaltung, Protokolle, Wiederherstellung und Rechteprüfung.
- Messbare QA- und Abnahmekriterien pro Funktion und Zielhardware; fehlende Tests = nicht abgenommen.

## 3. Ursprüngliche bestätigte Verbesserungen 1–41

**Wichtig:** Die Einzelbezeichnungen und der vollständige Wortlaut für die ursprünglichen Verbesserungen 1–40 sind in den hier verfügbaren Unterlagen nicht vollständig rekonstruiert. Ihre JA-/NEIN-/OFFEN-Entscheidungen werden trotzdem vollständig aufgenommen; Claude darf keine Funktionsnamen erfinden und muss die ursprüngliche freigegebene Basis-Spezifikation heranziehen.

| Nummer | Status | Verbindlicher Umgang |
|---|---|---|
| 1 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 2 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 3 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 4 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 5 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 6 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 7 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 8 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 9 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 10 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 11 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 12 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 13 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 14 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 15 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 16 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 17 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 18 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 19 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 20 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 21 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 22 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 23 | **NEIN** | Nicht implementieren; Ablehnung erhalten |
| 24 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 25 | **NEIN** | Nicht implementieren; Ablehnung erhalten |
| 26 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 27 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 28 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 29 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 30 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 31 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 32 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 33 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 34 | **NEIN** | Nicht implementieren; Ablehnung erhalten |
| 35 | **OFFEN** | Keine Freigabe; Entscheidung ausstehend |
| 36 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 37 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 38 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 39 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 40 | **JA** | Bestätigter Altbeschluss; exakte ursprüngliche Spezifikation beiziehen |
| 41 | **JA** | 20 bestätigte Designfunktionen; bestehendes UI professionell aufwerten, keine ungefragten Änderungen |

## 4. Vollständiges Statusregister aller späteren Verbesserungen 42–94

Alle hier mit **JA** gekennzeichneten Punkte sind im Entwicklungsplan bestätigt. Zahlen sind der bekannte Umfang des jeweils bestätigten Funktionspakets, **keine** Anzahl technisch fertiggestellter Features.

| Nr. | Status | Paket | Umfang | Kernanforderung |
|---:|---|---|---:|---|
| 42 | **JA** | Universal Studio Prompt-to-Action GODMODE | 20 | Prompt → validierter Auftrag → passende Fachadapter → bearbeitbare echte Artefakte → QA → Vorschau/Überarbeitung/Export; bestehende PromptLibrary und Intel.AskAsync wiederverwenden. |
| 43 | **JA** | Trendo ONE Workspace GODMODE | 20 | Eine konsistente, leicht verständliche Arbeitsoberfläche pro Studio; echte Arbeitsstände, nicht nur Karten und Menüs. |
| 44 | **JA** | Universal Professional Studio Fusion | 10 | Fachprogramme wie Blender/Unreal nur bei Bedarf über geprüfte optionale Adapter integrieren. |
| 45 | **JA** | Ultimate Office Studio GODMODE | 20 | Textverarbeitung, Tabellen, Präsentationen und Office-Produktionsabläufe in einem Workspace. |
| 46 | **JA** | Ultimate E-Mail & Communication Studio GODMODE | 20 | Kommunikation, Posteingang, Entwürfe und Versandfreigaben; keine Nachrichten ohne erforderliche Zustimmung. |
| 47 | **JA** | Ultimate Business & Finance Studio GODMODE | 20 | Finanzen, Kalkulationen und Unternehmenskennzahlen mit überprüfbarer Buchungs- und Berechnungsgrundlage. |
| 48 | **JA** | Ultimate Software Development Studio GODMODE | 23 | Programmierung, Build, Debugging, Tests, Projektänderungen und Releases; echte Code-Artefakte. |
| 49 | **JA** | Ultimate Blender & 3D Creation Studio GODMODE | 25 | Hochwertige Blender-Modelle und 3D-Objekte mit Referenzen, Material, Rigging, Export und QA. |
| 50 | **JA** | Ultimate Game Development Studio GODMODE | 40 | Spielproduktion inklusive Gamedesign, Gameplay, Story, Assets, Levels, Engine-Integration, Testing und Release; AION/GENESIS strikt getrennt. |
| 51 | **JA** | Ultimate Film, Video & Cinematic Studio GODMODE | 40 | Szenen, Schnitt, Kameras, Effekte und Filmproduktion unter einem Dach. |
| 52 | **JA** | Ultimate Image, Photo & Graphic Design Studio GODMODE | 40 | Bilder, Fotografie, Grafik, Bildbearbeitung, Design und geprüfte Exporte. |
| 53 | **JA** | Ultimate Music, Audio & Voice Studio GODMODE | 40 | Musik, Audio, Sprache, Synchronisation und Soundproduktion. |
| 54 | **JA** | Ultimate Web Design & Website Development Studio GODMODE | 40 | Webseiten inklusive Design, Umsetzung, responsiver Darstellung, Tests und kontrollierter Veröffentlichung. |
| 55 | **JA** | Ultimate Architecture & Interior Design Studio GODMODE | 40 | Architektur- und Innenraumplanung, Zeichnungen, CAD-/3D-Visualisierung. |
| 56 | **JA** | Ultimate Engineering & Simulation Studio GODMODE 3.0 | 60 | Konstruktion und Fachsimulation; optionale FreeCAD-, CalculiX-, OpenFOAM-, Blender- und Unreal-Adapter; keine ungeprüften Fachberechnungen. |
| 57 | **JA** | Ultimate Electronics, PCB & Embedded Systems Studio GODMODE | 50 | Elektronik, PCB und Embedded Systems, optionale KiCad-, ngspice-, FreeCAD-, Blender- und Unreal-Adapter. |
| 58 | **JA** | Ultimate Science & Research Studio GODMODE | 50 | Forschungsworkflows, wissenschaftliche Auswertung, Datenherkunft und reproduzierbare Tests, optional JupyterLab, SciPy, ParaView. |
| 59 | **JA** | Ultimate Product, Industrial & Fashion Design Studio GODMODE | 50 | Produkt-, Industrie- und Modegestaltung, CAD, 3D, visuelle Referenzen und technische Prüfungen. |
| 60 | **JA** | Ultimate 3D Scanning & Reconstruction Studio GODMODE | 50 | 3D-Scan, Punktwolken, Rekonstruktion und Qualitätskontrolle; optionale Meshroom-, CloudCompare-, Blender- und Unreal-Adapter. |
| 61 | **JA** | Ultimate Manufacturing, CNC & 3D Printing Studio GODMODE | 50 | Fertigung, CNC und 3D-Druck mit FreeCAD, PrusaSlicer, LinuxCNC etc.; reale Maschinenbewegungen strikt freigabepflichtig. |
| 62 | **JA** | Ultimate Transport, Vehicle & Logistics Studio GODMODE | 50 | Transport und Logistik inklusive Lkw, Fracht, Routen, Fähren, Kosten, geltende Fahrzeiten und Simulation. |
| 63 | **JA** | Ultimate Cybersecurity & Digital Forensics Studio GODMODE | 50 | Cybersecurity und digitale Forensik mit unveränderter ursprünglich bestätigter 120-Stunden-Forensikregel; keine eigenständige Neuinterpretation. |
| 64 | **JA** | Ultimate Maps, Geospatial & Digital Earth Studio GODMODE | 50 | Geodaten, Karten, GIS und Digital Earth mit QGIS/GDAL und optionalen 3D-Adaptern. |
| 65 | **JA** | Ultimate VR, AR & Mixed Reality Studio GODMODE | 50 | VR, AR und Mixed Reality mit Blender, Unreal und OpenXR über optionale Adapter. |
| 66 | **JA** | Ultimate Education, Training & Learning Studio GODMODE | 50 | Lernplattform, interaktive Kurse und Training; eigenständiges zuvor abgelehntes Language Studio 2.0 bleibt NEIN. |
| 67 | **JA** | Ultimate Robotics, Vision & Automation Studio GODMODE | 50 | Robotik, Machine Vision und Automatisierung; ROS 2, Gazebo, MoveIt 2, Blender, Unreal optional; Simulation-first und reale Hardwarefreigaben. |
| 68 | **JA** | Ultimate Data Analytics, Business Intelligence & Visualization Studio GODMODE | 50 | Datenquellen, bereinigte Datensätze, nachweisbare KPIs, Diagramme, Forecasts, Berichte; optionale DuckDB-, Polars-, Superset-, Blender-/Unreal-Adapter. |
| 69 | **JA** | Ultimate Book, Publishing & Editorial Studio GODMODE | 50 | Buchentwicklung, Buchbibel, Lektorat, Cover, Satz, DOCX, PDF, EPUB, überprüfte Druckvorbereitung; optionale Pandoc-, calibre-, Scribus-Adapter. |
| 70 | **JA** | Ultimate Smart Home, IoT & Building Automation Studio GODMODE | 50 | Smart Home, IoT und Gebäudeautomation; optionale Home-Assistant-, openHAB-, Matter-, MQTT-Adapter; unabhängige Sicherheitssysteme und Gerätefreigaben. |
| 71 | **JA** | Ultimate Business Process & Workflow Automation Studio GODMODE | 50 | Unternehmensworkflows, Trigger, APIs, Tests, Zustandsprotokolle, Freigaben; optionale n8n-/Node-RED-Adapter. |
| 72 | **JA** | Ultimate Personal Productivity & Life Management Studio GODMODE | 50 | Persönliche Aufgaben, Termine, Projekte, Notizen, Pläne und sichere geräteübergreifende Fortsetzung. |
| 73 | **JA** | Ultimate Event, Broadcast & Live Production Studio GODMODE | 50 | Livestreams, Veranstaltungstechnik, Kamera/Audio, Regie, Recording und Postproduction; optionale OBS-/vMix-Adapter; kein Start ohne Freigabe. |
| 74 | **JA** | Ultimate Knowledge, Research & Document Intelligence Studio GODMODE | 50 | Quellenbasierte Wissensverwaltung, Dokumentenanalyse, Volltext-/semantische Suche, Entscheidungsschutz und projektisoliertes Wissen. |
| 75 | **JA** | Ultimate Digital Twin & Reality Simulation Studio GODMODE | 50 | Digitale Zwillinge und Realitätssimulation; optionale FreeCAD-, Blender-, Unreal-, OpenModelica-, OPC-UA-, MQTT-Adapter; reale Anlagen getrennt. |
| 76 | **JA** | Ultimate Medical Visualization & Biomedical Engineering Studio GODMODE | 50 | Biomedizinische 3D-Visualisierung und Forschung; optional 3D Slicer, MITK, Blender, Unreal; strikter Datenschutz, keine klinische Diagnoseautomatik. |
| 77 | **JA** | Ultimate AI Agent & Multi-Agent Orchestration Studio GODMODE | 50 | Bestehende Trendo-KI-Engine und Agenten nutzen, Aufträge koordinieren, echte Statusnachweise, Projektisolation, Ressourcen- und Freigabegrenzen. |
| 78 | **JA** | Ultimate Legal Documents, Contracts & Compliance Studio GODMODE | 50 | Vertragsentwürfe, Quellenprüfung, Länderbezug, Compliance, Datenschutz, rechtliche Freigaben; kein Ersatz für Anwalt. |
| 79 | **JA** | Ultimate Cloud, Server, DevOps & Infrastructure Studio GODMODE | 50 | Server, Cloud, DevOps, DNS, Backups, Deployment und Monitoring; optional Docker, Terraform, Grafana, Ansible, Kubernetes, GitHub. |
| 80 | **JA** | Ultimate Visual App Builder & No-Code Creation Studio GODMODE | 50 | Visueller No-Code/Low-Code-App-Builder, Prompt-to-App, editierbare UI, Full-Stack-Projekte, echte Builds und QA. |
| 81 | **JA** | Ultimate AI Model, Training & Fine-Tuning Studio GODMODE | 50 | Lokale KI-Modellverwaltung, Benchmarks, Hardwareprüfung, optional Ollama/LM Studio/Hugging Face, kontrolliertes Fine-Tuning. |
| 82 | **JA** | Ultimate Plugin, Extension & Marketplace Studio GODMODE | 50 | Vorhandenes Extension Marketplace & Evolution Hub aufwerten; Plugin-SDK, eigene Erweiterungen, Sandbox, sichere Installation, Projektisolation. |
| 83 | **JA** | Ultimate Quality Assurance, Testing & Reliability Studio GODMODE | 50 | Studioübergreifende reale Tests, Playwright/xUnit, Blender-/Unreal-Tests, Fehlernachweise, Regression und verpflichtender Release Guardian. |
| 84 | **JA** | Ultimate Device Fusion, Remote Control & Distributed Computing Studio GODMODE | 50 | Fusion von Windows-PC, Raspberry Pi und Smartphone, gesicherte Remote-Ausführung, Aufgabenverteilung, Blender/Unreal-Produktion und Freigaben. |
| 85 | **JA** | Ultimate System Performance, Optimization & Self-Healing Studio GODMODE | 50 | Messbare Systemdiagnostik, CPU/GPU/RAM, Blender-/Unreal-Engpässe, kontrollierte Reparatur, Rollback und Nachtests. |
| 86 | **NEIN** | Ultimate Customer Support, CRM & Service Desk Studio GODMODE | 50 | **Abgelehnt; keines der 50 Zusatzfeatures als Paket aktivieren. Zuvor separat bestätigte CRM-/Kommunikationsfunktionen gelten weiter.** |
| 87 | **JA** | Ultimate Automotive Design, Vehicle Engineering & Driving Simulation Studio GODMODE | 50 | Pkw/Lkw/Fahrzeugkonstruktion, 3D-Fahrzeugmodelle, Fahrphysik, Cockpits, Blender-/Unreal-Integration und QA. |
| 88 | **JA** | Ultimate Space, Astronomy & Aerospace Engineering Studio GODMODE | 50 | Astronomie, Raumfahrzeugdesign, Orbitalmechanik, wissenschaftliche Simulation; optional NASA GMAT, FreeCAD, Blender/Unreal. |
| 89 | **JA** | Ultimate E-Commerce, Digital Products & Online Store Studio GODMODE | 50 | Onlineshops, digitale Produkte, Checkout, Zahlungsadapter WooCommerce/Shopify/Stripe/PayPal, gesicherte Downloads, Rechtsprüfung. |
| 90 | **JA** | Ultimate Motion Capture, Character Animation & Virtual Production Studio GODMODE | 50 | Motion Capture, Rigging, Facial Animation, Retargeting, Blender-/Unreal-Integration und Animations-QA. |
| 91 | **JA** | Ultimate Materials, Textures & Photorealistic Rendering Studio GODMODE | 50 | PBR-Materialien, Texturen, Shader, fotorealistisches Rendering, VRAM-/Performanceprüfung, Blender-/Unreal-Integration. |
| 92 | **JA** | Ultimate Procedural World, Terrain & Environment Creation Studio GODMODE | 50 | Open-World-Terrain, PCG, World Partition, HLOD, Landschaften, Städte, Umweltsimulation und geprüfte Streaming-Performance. |
| 93 | **JA** | Ultimate Living World, NPC & Artificial Life Simulation Studio GODMODE | 50 | Persistente NPCs, Tagesabläufe, Beziehungen, MassEntity, StateTree, Smart Objects, projektisolierte Unreal-Open-World-QA. |
| 94 | **JA** | Ultimate Cinematic Lighting, Visual Effects & Destruction Studio GODMODE | 50 | Cinematic Lighting, Niagara-Partikel, Unreal Chaos Destruction, Blender-Simulation, Kameras, VFX, Render- und Performanceprüfungen. |

## 5. Fachliche Implementierungsanweisungen und Modulgrenzen

### 5.1 Bestehende Studios statt zusätzlicher Engines
Die Verbesserungen 42–94 sind **Aufwertungen und verbindliche Anforderungen** an das bestehende Trendo OS. Bestehende 54 Studio-Karten/StudioCatalog, PromptLibrary, Intel.AskAsync, Projektverwaltung, Workflows, Agenten und Desktop-Layout behalten eine klare gemeinsame Architektur. Ein separates Studio wird nur dann technisch angelegt, wenn dies anhand des Repos und der freigegebenen Navigation erforderlich ist. Spezifikation, Implementation und Abnahme getrennt dokumentieren.

### 5.2 Geprüfter Produktionsablauf
`Prompt → Auftrag mit Grenzen → Projektisolierung → Tool-/Lizenz-/Hardwarecheck → Vorschau/Plan → erforderliche Freigabe → echte Arbeitsschritte → Artefakte → QA/Logs → Nachbesserung → finale Freigabe → Export/Release`

### 5.3 Mindest-Nachweise
- Build-Protokoll und technische Tests für Software und Plugins.
- Projektdateien, Geometrietests und realer Blender-/Unreal-Import für 3D-, Game- und VFX-Assets.
- Quellen-/Datensatzherkunft und reproduzierbare Berechnung für Forschung, Analytics, Recht und Engineering.
- Verbindungs- und Berechtigungstests für Cloud, Fernzugriff, Zahlungs-, IoT-, Roboter- und Geräteschnittstellen.
- Testnachweis zu Ressourcen, RAM/VRAM, Framerate, Streaming und Fehlertoleranz für AAA-/Open-World-Ziele.

## 6. Verbesserung 94 – vollständiger Funktionskatalog (JA)

**Ultimate Cinematic Lighting, Visual Effects & Destruction Studio GODMODE – 50 Funktionen**

| Nr. | Funktion | Verbindliche Anforderung |
|---:|---|---|
| 1 | Prompt-to-VFX | Effekte aus natürlichsprachlichen Aufträgen planen |
| 2 | VFX ONE Workspace | Effektplanung, Livevorschau, Bearbeitung und QA an einem Ort |
| 3 | VFX Scene Architect | Szenen und Abhängigkeiten aufbauen |
| 4 | Cinematic Lighting Director | Filmische Beleuchtung |
| 5 | Dynamic Global Illumination | Engineunterstützte globale Beleuchtung |
| 6 | Volumetric Lighting | Atmosphärische Lichtstrahlen |
| 7 | Shadow Quality Guardian | Schatten und Leistung kontrollieren |
| 8 | Particle Effect Creator | Partikelsysteme für Funken und Staub |
| 9 | Fire Simulation | Feuereffekte |
| 10 | Smoke & Fog Creator | Rauch und Nebel |
| 11 | Explosion Director | Virtuelle Explosionen inszenieren |
| 12 | Lightning & Electricity | Blitz- und Energieeffekte |
| 13 | Water VFX | Spritzwasser, Wellen und Gischt |
| 14 | Weather Effects | Regen, Schnee, Sturm und Wind |
| 15 | Destruction Architect | Zerstörungsszenen strukturiert planen |
| 16 | Fracture System | Bauteile in Bruchstücke zerlegen |
| 17 | Debris Simulation | Trümmerbewegung simulieren |
| 18 | Building Destruction | Zerstörbare Gebäude für virtuelle Szenen |
| 19 | Terrain Destruction | Kontrollierte Terrainänderungen |
| 20 | Vehicle Damage VFX | Visuelle Fahrzeugschäden |
| 21 | Impact Effects | Treffer- und Aufpralleffekte |
| 22 | Magic & Energy Effects | Fiktive magische Kräfte |
| 23 | Character VFX | Effekte mit Figuren koppeln |
| 24 | Boss Battle VFX | Bosskampf-Effekte |
| 25 | Environmental Storytelling | Atmosphäre mit Umwelteffekten unterstützen |
| 26 | Cinematic Camera Fusion | Kameras mit Effektablauf abstimmen |
| 27 | Color Grading | Filmische Farbgebung |
| 28 | Post-Processing | Unterstützte Post-Processing-Effekte |
| 29 | Blender Simulation Fusion | Blender-Simulationsadapter |
| 30 | Unreal Niagara Fusion | Unreal Niagara über echten Adapter |
| 31 | Unreal Chaos Fusion | Unreal Chaos für passende Zerstörungssimulationen |
| 32 | Material Studio Fusion | Bestehende Materialien/Shader weiterverwenden |
| 33 | Audio Synchronization | Sound und visuelle Ereignisse abstimmen |
| 34 | Timeline Editor | Zeitachsen bearbeiten |
| 35 | VFX Reference Lock | Freigegebene Stil- und Szenenreferenzen sichern |
| 36 | Performance Optimizer | CPU/GPU/VRAM-Auswirkungen prüfen |
| 37 | VFX Asset Library | Getestete Effekte wiederverwendbar ablegen |
| 38 | VFX Versioning | Varianten und Stände versionieren |
| 39 | VFX QA Guardian | Echte Assets/Render/Engine-Tests durchführen |
| 40 | Controlled VFX Production | Freigegebene Produktionsjobs koordinieren |
| 41 | Hyperrealistic VFX Intelligence | Referenzgebundene visuelle Qualität |
| 42 | Cinematic Destruction Director | Filmisch inszenierte virtuelle Zerstörung |
| 43 | Physics-Aware VFX | Effekte an geeignete Physikdaten koppeln |
| 44 | Intelligent Effect Layering | Mehrere VFX-Ebenen koordinieren |
| 45 | Dynamic Environment Reaction | Virtuelle Welt reagiert auf Ereignisse |
| 46 | Procedural Disaster Scenarios | Sichere rein virtuelle Katastrophenszenen |
| 47 | Multi-Scale VFX Optimization | Effektdetail nach Entfernung/Hardware |
| 48 | Visual Fidelity Benchmark | Vergleich mit genehmigten Referenzen |
| 49 | Autonomous VFX Production Team | Bestehende Studio-Agenten koordinieren |
| 50 | Verified VFX Release Gate | Freigabe erst nach realen VFX-/Performanceprüfungen |

### VFX-Technik und Qualitätsabnahme
- Unreal Niagara/Chaos und Blender optional, keine verdeckten Installationen oder externen Uploads.
- Beispielfall AION: 30-m-Götterstatue, Blitz, Bodenriss, Trümmer, Staub, Kamerafahrt; Storybibel/Designlock respektieren.
- VFX-Artefakte: Szenen-/Projektdateien, Material-/Effektdefinitionen, Render-/Vorschau, Log, Leistungsmessung und Exportmanifest.
- Tests: Effekte laufen in Zielengine, korrekte Auslösung und zeitliche Synchronität, kein kritischer Kollisions-/Speicherfehler, FPS innerhalb festgelegter Budgets.
- Falls Hardware-/Lizenz-/Pluginvoraussetzung fehlt: eindeutig als BLOCKIERT oder nur Entwurf kennzeichnen.

## 7. Verbesserung 93 – vollständige bereits erstellte Spezifikation (JA)

**Die folgende Spezifikation wird vollständig eingebettet, damit der Master eine eigenständige Übergabedatei ist.**

# Trendo OS – Verbesserung 93
## Ultimate Living World, NPC & Artificial Life Simulation Studio GODMODE

**Status:** JA – für die Entwicklungsplanung freigegeben am 22.10.2026. **Nicht** als implementiert, getestet oder abgenommen kennzeichnen.

## Ziel und verbindliche Leitlinien
Ein intelligentes Studio zur Gestaltung, Produktion und Prüfung persistenter NPCs, virtueller Gesellschaften, Tierwelten, Verkehr und lebendiger Open Worlds. Ein **zentraler ONE Workspace** deckt Prompt, Planung, Simulation, Live-Vorschau, Tests, Verbesserungen und Export ab. Vorhandene Trendo-KI-, Agenten-, Game-, Story-, Blender-, Unreal-, Animation-, Welt-, Fahrzeug- und QA-Systeme werden wiederverwendet – keine parallele Engine.

**Beispielauftrag:** „Erstelle eine moderne Stadt mit intelligenten Bewohnern, Tagesabläufen, Freundschaften, Berufen und funktionierendem Verkehr.“

## 50 bestätigte Funktionen

| Nr. | Funktion | Verbindliche Anforderung |
|---:|---|---|
| 1 | Prompt-to-Living-World | Welten und Lebenssimulationen aus einfachen Beschreibungen planen |
| 2 | Living World ONE Workspace | NPCs, Verhalten, Ergebnisse und QA zentral verwalten |
| 3 | NPC Character Architect | Individuelle Figurenmerkmale definieren |
| 4 | Personality Intelligence | Unterschiedliche Persönlichkeiten modellieren |
| 5 | Daily Routine Director | Tages- und Wochenabläufe steuern |
| 6 | Needs & Motivation System | Bedürfnisse, Ziele und Prioritäten simulieren |
| 7 | NPC Memory | Relevante Begegnungen und Ereignisse speichern |
| 8 | Player Recognition | Spieler anhand tatsächlicher Spielereignisse wiedererkennen |
| 9 | Relationship Simulation | Beziehungen und Konflikte modellieren |
| 10 | Family & Social Networks | Familien und soziale Netzwerke darstellen |
| 11 | Dialogue Intelligence | Kontextabhängige Gespräche ermöglichen |
| 12 | Emotion & Reaction System | Nachvollziehbare Reaktionen steuern |
| 13 | Occupation Simulation | Arbeits- und Berufsabläufe modellieren |
| 14 | Shopping & Economy | Einkaufen, Handel und vereinfachte Wirtschaftskreisläufe |
| 15 | Home & Property Behavior | Wohnungen und Gebäude sinnvoll nutzen |
| 16 | Education & Learning | Lernen und Fähigkeiten darstellen |
| 17 | NPC Decision Engine | Entscheidungen anhand dokumentierter Regeln |
| 18 | Behavior Tree Integration | Unreal Behavior Trees über Adapter verwenden |
| 19 | StateTree Integration | Unreal StateTree unterstützen |
| 20 | Smart Object Interaction | Objekte und Einrichtungen als Interaktionsziele verwenden |
| 21 | Navigation Intelligence | Wegefindung und Navigation |
| 22 | Crowd Simulation | Große Menschenmengen performant simulieren |
| 23 | Pedestrian Traffic | Fußgängerverkehr abbilden |
| 24 | Vehicle Traffic Fusion | Verkehrssysteme einbinden |
| 25 | Wildlife Intelligence | Tiere und Lebensräume modellieren |
| 26 | Creature Behavior | Gegner und Fantasiekreaturen steuern |
| 27 | Combat NPC Intelligence | Taktische Gegnerreaktionen definieren |
| 28 | NPC Perception | Sehen, Hören und Spielreize |
| 29 | Dynamic Events | Regelbasierte dynamische Ereignisse |
| 30 | Mission & Story Fusion | NPCs mit genehmigten Missionen verbinden |
| 31 | Weather Response | Verhalten bei unterschiedlichem Wetter |
| 32 | Day & Night Behavior | Tageszeitabhängige Handlungen |
| 33 | World Persistence | Relevante Weltzustände sichern |
| 34 | Savegame Integration | NPC-Daten zuverlässig speichern/laden |
| 35 | Blender Character Fusion | Freigegebene Charaktere und Animationen integrieren |
| 36 | Unreal AI Integration | Unreal Gameplay- und KI-Systeme verbinden |
| 37 | NPC Debugging | Entscheidungswege und Fehler nachvollziehbar machen |
| 38 | Population & Performance | Detailstufen und Rechenlast abstimmen |
| 39 | Living World QA Guardian | Verhalten, Persistenz und Performance real testen |
| 40 | Controlled Living World Production | Freigegebene Produktionsabläufe koordinieren |
| 41 | Persistent Life Intelligence | Ereignisse mit langfristigen Folgen speichern |
| 42 | Long-Term Character Development | Figurenentwicklung über Zeit |
| 43 | Social Consequence Engine | Konsequenzen von Spielerentscheidungen |
| 44 | Autonomous Society Simulation | Gesellschaft innerhalb definierter Regeln simulieren |
| 45 | Smart NPC Conversation Memory | Vergangene Gespräche situationsbezogen berücksichtigen |
| 46 | Adaptive World Population | NPC-Dichte an Gebiet und Hardware anpassen |
| 47 | Background Life Simulation | Entfernte NPCs abstrahiert fortschreiben |
| 48 | Intelligent NPC Animation Fusion | Bewegung, Handlung und Umgebung abstimmen |
| 49 | Living World Director Team | Vorhandene Story-/Unreal-/Animation-/QA-Agenten nutzen |
| 50 | Verified Living World Release Gate | Veröffentlichung erst nach echten Tests |

## ONE-Workspace-Ablauf
1. **Prompt/Brief:** gewünschte Welt, Population, Interaktionen, Plattform und Leistungsbudget erfassen.
2. **Plan:** NPC-Archetypen, Ziele, Beziehungen, Tagesabläufe, Ereignisse und Datenmodelle vorschlagen; fehlende Voraussetzungen markieren.
3. **Validierung/Freigabe:** bestehende Spielbibel, Design Locks und Benutzerentscheidungen prüfen.
4. **Produktion:** vorhandene Blender- und Unreal-Adapter für Assets, Animation, StateTree/Behavior Trees, Smart Objects, Navigation und MassEntity/MassGameplay nutzen, sofern verfügbar und kompatibel.
5. **Simulation:** Nahbereich detailliert; mittlere Entfernung vereinfacht; ungeladene Regionen durch ereignisbasierte/abstrahierte Zustandsänderungen fortführen.
6. **Live-Nachweise:** Nur echte laufende Prozesse als aktiv zeigen; erzeugte Dateien, Logs, Simulationen und Messwerte dokumentieren.
7. **QA und Iteration:** Verhalten, Erinnerung, Dialog, Performance und Save/Load testen; Fehler mit reproduzierbaren Schritten an Fachstudios zurückgeben.
8. **Handoff/Release:** Versioniertes, bearbeitbares Projektartefakt plus Testbericht und bekannte Einschränkungen bereitstellen.

## Technische Architektur und Schnittstellen
- Bestehende Trendo-Projektverwaltung, Projektisolation, PromptLibrary, KI-Kern, Agentenverwaltung, QA-Studio 83 und ONE-Workspace-Rahmen wiederverwenden.
- **Unreal-Adapter (optional):** MassEntity/MassGameplay, StateTree, Behavior Trees, Smart Objects, AI Perception, Navigation, World Partition und projektabhängige Gameplay-Komponenten; tatsächliche API-/Versionskompatibilität vor Implementierung prüfen.
- **Blender-Adapter (optional):** vorhandene Figuren, Rigging, Animationen und Export-Pipeline, mit echter Prüfung in Unreal.
- Datenschema für NPC-IDs, Archetypen, Rollen, Bedürfnisse, Beziehungen, Ereignisse, Erinnerungen, Aufgaben, Positionen und persistente Zustände; versionierbar, mit Migrations- und Savegame-Tests.
- Deterministische Regeln für zentrale Lebenslogik; generative KI nur in klar begrenzten, überprüfbaren Funktionen, z. B. Dialogentwürfen. Keine Abhängigkeit von dauerhafter Online-KI für jeden NPC.
- Messbare Budgets für CPU, GPU, RAM, Tick-Zeit, Simulationsdichte und Ladezeiten. Werte werden aus Testläufen erhoben, nicht angenommen.

## Projektregeln für AION und GENESIS
**AION:** Gegenwart 2026; versteinert angreifende Götter, bestehende Story und genehmigte Figuren respektieren. Bewohner reagieren nachvollziehbar auf die Ereignisse, erkennen den Helden anhand gespeicherter Begegnungen und interagieren mit Missionen.

**GENESIS:** Erste spielbare Existenz beginnt 2026, eine kontinuierliche Welt ohne Epochenwahl; nach dem Tod genehmigte Wahlmöglichkeiten Jenseits/Wiedergeburt im bestehenden zeitlichen Verlauf. Langfristige Beziehungen, Familien, Berufe, gesellschaftliche Entwicklung und Persistenz unterstützen. Keine eigenmächtige Änderung der geschützten Story C00–C21 einschließlich genehmigter Ausnahmen.

**Strenge Isolation:** AION und GENESIS haben getrennte Projektordner, Regeln, Speicherstände, Wissenskontexte und Freigaben. Keine ungefragte Übernahme von Assets oder Storyelementen. Externe Assets nur innerhalb der genehmigten Lizenz- und Kostenregeln, bei GENESIS kostenlos oder selbst erstellt.

## Abnahmekriterien – tatsächlich nachzuweisen
- Ein Beispiel-NPC folgt einem definierten Tagesplan, reagiert auf ein Ereignis und setzt nach Speichern/Laden seinen Zustand konsistent fort.
- Mindestens ein vernetztes Beziehungsszenario ändert sich durch eine reale Spielerhandlung und bleibt persistent.
- Ein Interaktionsszenario mit Smart Object sowie ein Navigationsszenario werden im ausführbaren Testlevel erfolgreich geprüft.
- NPC-Nah-/Fernsimulation und World-Partition-Übergänge haben nachvollziehbare State- und Performance-Tests.
- Dialogerinnerungen beziehen sich auf nachgewiesene Ereignisse; fehlender Kontext wird nicht erfunden.
- Fehlerfälle (nicht erreichbares Ziel, blockierter Weg, fehlende Animation, beschädigter Save) werden kontrolliert behandelt.
- Blender-/Unreal-Ergebnisse haben überprüfbare Dateien, Importprotokolle und QA-Nachweise; nicht verfügbare Adapter werden als nicht verfügbar ausgewiesen.
- Zielhardware, Benchmark-Szenen, tatsächliche Messwerte, Fehlertickets und Status offen/bestanden/blockiert sind dokumentiert.
- **Release Gate:** Keine Freigabe bei kritischen offenen Fehlern, ungetesteten verpflichtenden Anforderungen oder fehlenden Artefakten.

## Schutzregeln
- Keine zweite NPC-/KI-Engine, keine Fake-Agenten oder erfundenen Testergebnisse.
- Keine Story- oder Charakteränderungen ohne ausdrückliche Zustimmung.
- Freigabe vor externen Downloads, bezahlten Diensten, Datenübertragungen und produktiven Eingriffen.
- Verbesserung 35 bleibt offen. Abgelehnte Verbesserungen – einschließlich 86 – bleiben NEIN. Die bestehende **120-Stunden-Forensikregel bleibt unverändert**.

## Umsetzungsstatus
Diese Markdown-Datei ist die genehmigte **Anforderungsspezifikation zur Übergabe an das Entwicklungssystem**. Sie ist **kein Beleg** für fertige Implementierung, erfolgreich ausgeführte Tests oder Produktivfreigabe.


## 8. Verbesserung 42 – bestehender ausführlicher Entwicklungsauftrag (JA)

Der bereits ausgearbeitete Auftrag Nr. 42 ist vollständig angehängt, um den bestätigten Ausgangsprozess zu erhalten.

# TRENDO OS – VERBESSERUNG 42
## Universal Studio Prompt-to-Action GODMODE – verbindlicher Claude-Code-Auftrag

**Auftraggeber:** Trendo OS  
**Ziel-Repository:** `manuelbrandner85/Trendo-OS`  
**Stand der Beauftragung:** 22.10.2026  
**Status:** Verbesserung 42 einschließlich aller 20 Funktionen **JA – zur Implementierung freigegeben**. Dies ist **kein Nachweis**, dass sie bereits implementiert sind.

> **Arbeitsbefehl an Claude Code:** Implementiere Verbesserung 42 in der **bestehenden** Trendo-OS-Codebasis. Untersuche zuerst den tatsächlichen Repository-Stand und passe sämtliche Namen, Dateipfade, Schnittstellen und Implementierungen an den real gefundenen Code an. Erfinde weder vorhandene Komponenten noch erfolgreiche Testergebnisse. Kein Rewrite des Betriebssystems, keine zweite KI-Engine, keine Umgehung bestehender Freigaben. Liefere funktionsfähigen, getesteten Code statt einer bloßen Präsentation oder Demo mit vorgetäuschten Ergebnissen.

## 1. Nicht verhandelbare Projektregeln

1. **Bestehende Architektur erhalten:** Vorhandene **54 Studioeinträge** im `StudioCatalog` und bestehende UI-/Navigations-, Projekt-, Sicherheits-, Dateiverwaltungs- und Persistenzmechanismen nutzen. Falls Anzahl oder Namen im aktuellen Branch abweichen, Abweichung dokumentieren; nichts blind auf 54 hardcoden.
2. **Wiederverwendung vor Neubau:** Nachweislich vorhandene `PromptLibrary`, `Intel.AskAsync`, Briefdatenmodelle, `StudioCatalog`, KI-/Blender-/Unreal-Adapter, Projektisolation und vorhandene QA-/Forensikmechanismen zuerst finden und wiederverwenden. Falls ein Symbol fehlt oder anders heißt: realen Gegenpart benennen und adaptieren, nicht eine vermeintliche Implementierung vortäuschen.
3. **Lokale KI zuerst:** Bereits eingestelltes lokales KI-System verwenden. Cloud-/Drittanbieter nur nach expliziter, widerrufbarer Nutzererlaubnis und transparenter Offenlegung von Kosten, Daten und Zweck. Keine stillen Netzwerk-Fallbacks.
4. **Menschliche Kontrolle:** Versand, Zahlung, Veröffentlichung, Kontenänderungen, Sicherheitskonfiguration, Boot-/Systemeingriffe, Gerätebefehle und Maschinensteuerung werden **nie** durch bloße Prompt-Absicht freigegeben. Vor dem konkreten Seiteneffekt ist eine zu Ziel, Inhalt, Umfang und Gültigkeitsdauer passende menschliche Bestätigung nötig.
5. **Deterministische Fachprüfung:** Technische Berechnungen, Einheiten, Konstruktionswerte, Statik, Finanzrechnungen, Formeln, Geometrie, Prüfwerte usw. müssen – soweit fachlich möglich – mit verifizierbaren deterministischen Fachwerkzeugen berechnet und geprüft werden. KI darf Annahmen vorschlagen, aber keine unbelegten Mess-/Rechenergebnisse als Fakten ausgeben.
6. **Keine Phantom-Ergebnisse:** Niemals „fertige Datei“, „gerendertes Video“, „gesendete E-Mail“, „gebautes Modell“, „ausgeführt“, „getestet“ oder „veröffentlicht“ melden, wenn die reale Aktion, Datei bzw. QA nicht nachgewiesen ist. Ergebnisstatus unterscheidet `planned`, `awaiting_input`, `awaiting_approval`, `running`, `produced`, `verified`, `failed`, `cancelled`, `unsupported`.
7. **Projektisolation und Datenschutz:** Jeder Auftrag bleibt im expliziten Projekt-/Studio-Kontext. Pfade kanonisieren, relative/absolute Pfadfluchten und Symlink-Umgehungen blockieren, geheimhaltungsbedürftige Inhalte minimieren, Berechtigungen serverseitig prüfen.
8. **Bestandsentscheidungen respektieren:** Verbesserungen **23, 25, 34 sowie Language Studio 2.0 bleiben NEIN**. Verbesserung **35 bleibt OFFEN** und darf nicht stillschweigend aktiviert werden. Alle bisherigen bestätigten Module, ausdrücklich **41**, bleiben bestehen und dürfen nicht regressieren.
9. **120-Stunden-Forensikregel unverändert:** Die im Projekt bereits vereinbarte 120-Stunden-Forensikregel einschließlich ihrer realen Speicher-, Zugriffsschutz-, Lösch- und Ablaufsemantik aus vorhandenem Code/Projektvorgaben übernehmen. **Nicht** eigenmächtig als andere Frist interpretieren, verkürzen oder ausdehnen. Falls Implementierung oder genaue Semantik unklar sind, als Blocker kennzeichnen und unverändert lassen, bis geklärt.
10. **Keine pauschale Aktivierung gefährlicher Fähigkeiten:** Adapter können implementiert, aber risikoreiche Ausführung bleibt bei fehlendem Berechtigungs-/Freigabe-Backend deaktiviert (`unsupported` oder `awaiting_approval`).

## 2. Verbindliche Zielkette

`Nutzerprompt → Studioauflösung → strukturierter validierter Auftrag → gezielte Rückfragen bei tatsächlich fehlenden Angaben → Policy-/Capability-Prüfung → Auftragsplanung → menschliche Freigabe falls erforderlich → echter fachspezifischer Studio-Adapter → persistiertes, bearbeitbares Artefakt → deterministische und studienspezifische QA → Vorschau → Änderungsrunden → Export/Datei bzw. gesondert freigegebene Aktion`

**Kein Übergang darf simuliert werden.** Jede Stufe hat maschinenlesbare Status-, Fehler- und Provenienzinformationen. Prompt-Text ist keine Autorität zur Umgehung der Rechteprüfung.

## 3. Die 20 obligatorischen Funktionsbereiche

1. **Universal Prompt Entry:** Einheitliche Eingabe pro Studio mit Anhängen, Sprache, Kontext und verlustfreier Verlaufshistorie.
2. **Studio Context & Routing:** Zuordnung zum aktuellen Studio oder bewusstes Routing auf ein anderes Studio, stets mit sichtbarer Begründung.
3. **Intent & Action Parser:** Strukturiertes, versioniertes Auftragsobjekt statt freier, unkontrollierter Tool-Aufrufe.
4. **Schema Validation:** Studio-, Aktions- und Dateiformat-spezifische Validierung mit verständlichen Fehlermeldungen.
5. **Smart Clarifications:** Nur fehlende, entscheidungsrelevante Daten nachfragen; Teilantworten fortsetzen können.
6. **PromptLibrary Integration:** Bestehende Promptvorlagen und Fachinstruktionen referenzieren und versioniert anwenden.
7. **Existing AI Orchestration:** `Intel.AskAsync` bzw. reale vorhandene KI-Abstraktion verwenden; keine zweite Model-Routing-/Conversation-Engine.
8. **Local-First Provider Policy:** Anbieterberechtigungen, Datenschutz, Kostenhinweis, explizite Einwilligung, Offline-Verhalten und Widerruf.
9. **Studio Capability Registry:** Für jeden bestehenden Studioeintrag eindeutige unterstützte Aktionen, Adapter, Outputtypen und Capability-Level deklarieren.
10. **Adapter Execution Layer:** Echte typisierte Adapter und ausführbare Aktionen, mit Cancellation, Timeouts, Fehlerbehandlung und Ressourcenlimits.
11. **Editable Artifact Workspace:** Persistierte, editierbare Zwischen- und Endartefakte mit Projektbezug und Wiederaufnahme.
12. **Live Progress & Evidence:** Wahrheitsgemäßer Fortschritt, Schritte, Tool-Ergebnisse, Protokolle und Dateinachweise, keine erfundenen Prozente.
13. **Deterministic Verification Hub:** Fachrechner/Validatoren mit Einheiten, Eingangsannahmen, Versionsstand und reproduzierbarem Prüfergebnis.
14. **Specialized QA:** Fachspezifische Lint-, Schema-, Render-, Dokument-, Laufzeit-, Geometrie-, Medien- oder Formatprüfungen.
15. **Preview Engine:** Echte Vorschau aus erstelltem Artefakt (oder sichtbare Kennzeichnung, wenn Vorschau unmöglich).
16. **Revision Loop:** Gezielt änderbare Artefakte, Diff/Versionen, Undo/Redo nach realer Unterstützung, QA nach Änderungen erneut ausführen.
17. **Export & File Handoff:** Wirklich existierende Dateien, prüfbare Pfade/Checksummen, Formate, Importierbarkeit und Download-/Öffnen-Aktion.
18. **Human Approval Gateway:** Aktionsgebundene Freigaben für Senden, Bezahlen, Publizieren, Geräte/Maschinen, Sicherheit, Boot und andere Seiteneffekte; Ablehnung/Timeout sicher behandeln.
19. **Project Isolation, Security & Forensics:** Sandbox, Berechtigungen, Redaction, Protokolle und die bestehende 120-Stunden-Forensikregel ohne Regression.
20. **Testing, Telemetry & Recovery:** Unit-, Integrations-, E2E-, Sicherheits-, Abbruch-/Recovery-Tests und nachvollziehbare Betriebsdiagnose ohne sensitive Datenlecks.

## 4. Architekturvertrag (am Codebestand ausrichten)

Nutze vorhandene Patterns und DI-Lifecycle der Anwendung. Die folgenden Typen sind **semantische Soll-Schnittstellen**, keine Behauptung über bestehende Klassennamen:

```text
StudioPromptRequest
  requestId, projectId, studioId, prompt, attachments[], locale,
  requestedAction?, privacyMode, provenance
StudioActionSpec
  schemaVersion, studioId, actionId, parameters{}, requiredInputs[],
  outputKinds[], sideEffectClass, capabilityRequirements[]
StudioExecutionContext
  projectWorkspace, userIdentity, effectivePermissions,
  providerConsent, cancellationToken, resourceBudget
StudioActionPlan
  normalizedRequest, validationFindings[], clarificationQuestions[],
  steps[], approvalRequirements[], estimatedResources?
StudioArtifact
  artifactId, projectId, format, filePath, editability,
  producer, dependencies[], sha256?, version, previewRef?, qaReportRef?
StudioExecutionResult
  status, stepEvidence[], artifacts[], errors[], warnings[],
  approvals[], qaResults[], timestamps
```

Konzipiere eine **gemeinsame Workflow-Orchestrierung** mit `IStudioActionAdapter` (oder existierendem Äquivalent), `IStudioCapabilityProvider`, `IArtifactValidator`, `IApprovalService` und `IArtifactStore`. Adapter müssen Fähigkeiten explizit deklarieren; fehlen sie, darf der Auftrag **nicht** erfolgreich enden. Vorhandene Services bevorzugen, keine parallelen Datenbanken, Promptstores oder KI-Provider-Manager einführen.

**Persistenter Zustandsautomat:** `Draft → Validating → NeedsClarification | Planned → NeedsApproval | Executing → Produced → QA → Verified | RevisionRequested | Failed | Unsupported`; Abbruch von ausführbaren Zuständen aus ermöglichen. Freigabestatus muss vor jedem risikoreichen Side Effect **erneut** durch vertrauenswürdigen Code geprüft werden (TOCTOU vermeiden). Retries dürfen externen Seiteneffekt nicht doppelt ausführen; Idempotenzschlüssel und deduplizierte Aktionen.

**Security Boundary:** Das Modell liefert Daten bzw. vorgeschlagene Aktionen, keine Shell-, Browser-, Git-, Zahlungs-, Geräte- oder Netzwerkprivilegien. Tool-Aufrufe nur durch registrierte, parametergeprüfte und autorisierte Fachadapter. Externe Dokumente/Prompts sind untrusted; Prompt Injection darf keine Freigabe ersetzen.

## 5. Abdeckung aller Studioeinträge

1. Enumeriere **alle im aktuellen `StudioCatalog` tatsächlich gefundenen Einträge** mit stabiler `studioId`, UI-Titel, Kategorie, existierenden Services, vorgeschlagenen Aktionen, erlaubten Eingaben, realem Adapter und Outputformaten.
2. Erzeuge daraus `studio-capability-matrix.md` als **vollständige, überprüfbare Abdeckungsmatrix**. Zieleinträge sind nach Projektvorgabe 54; stimmt der Code nicht überein, melde die Differenz und wahre die echten IDs.
3. **Kein blindes „alle 54 funktionsfähig“.** Je Studio Capability-Reife explizit ausweisen: `ready`, `partial`, `blocked`, `not_implemented`; `ready` nur mit real ausgeführtem End-to-End-Test.
4. Baue die gemeinsame Pipeline so, dass alle Studios Prompt-Erfassung, Validierung, Status, Rückfragen, Vorschau/Artefaktlinks (soweit zutreffend) und Fehlermeldungen verwenden; fachspezifische Ausführung nur dort freischalten, wo ein realer Adapter vorhanden ist.
5. Keine 54 duplizierten KI-Prompts als Ersatz für funktionierende Studio-Adapter. Profile dürfen Daten/Regeln enthalten, aber der gemeinsame Kern bleibt geteilt.

## 6. Priorisierte vertikale Implementierung

**P0 – Briefstudio**  
Vorhandene Briefdatenmodelle wiederverwenden. Empfänger/Absender, Betreff, Form, Anrede, Sprache, Brieftext, Datum, Unterschriftenplatz, Layout prüfen; strukturierter editierbarer Brief, echte Vorschau, PDF/DOCX-Export **nur wenn im System nachweislich unterstützt** bzw. durch überprüften Exportadapter. Test: Brief erzeugen, ändern, speichern, neu öffnen und Export auf Dateiexistenz und Inhalt prüfen.

**P0 – Office**  
Dokumente, Tabellen, Präsentationen nur mit echten vorhandenen bzw. integrierten Bibliotheken; editierbare Quelldateien; Formeln über Tabellen-Engine evaluieren, keine frei erfundenen Ergebnisse. Re-Open-Tests, Formatprüfungen, Formeln und Export-Checks.

**P0 – E-Mail**  
Entwurf, Empfänger, Betreff, Anhänge, Vorschau; keine Nachricht ohne konkrete Sendefreigabe verschicken. Versandprovider/Konto nur nach verifizierter vorhandener Integration und Berechtigung; nachweisbare Message-ID oder präziser Fehlerstatus. Idempotenz.

**P0 – Business**  
Kunden-/Angebots-/Rechnungs-/Analyseartefakte nur mit belastbaren Datenquellen und validen Rechenregeln; Zahlungs- und steuerrelevante Beträge deterministisch mit Decimal/Einheiten/Währungen; keine ungeprüfte Rechts-/Steuerberatung, keine Rechnung als „gebucht“ markieren ohne tatsächliche Transaktion. Zahlungsaktionen extra freigeben.

**P1 – Software**  
Projektgebundene Quelltextänderungen über diffbasierte Vorschläge, Tests, Build und Review; kein eigenmächtiger Push/Merge/Deployment. Kontrollierte Workspaces, Syntax-/Buildprüfungen, Git-Status, keine Überschreibung fremder Arbeit.

**P1 – 3D / Blender**  
Vorhandene Blender-Adapter nutzen, modellierbare Szenendatei erzeugen, Geometrie/Materialien/Koordinaten/Skalen prüfen, Blender-Protokolle und gerenderte Vorschau nur bei tatsächlichem Lauf ausgeben. Kein „fotorealistisch“ ohne messbare Prüfung bzw. Nutzerabnahme.

**P1 – Games / Unreal**  
Bestehende Unreal-Schnittstelle und Projektisolation verwenden; Assets importieren, tatsächliche Projektänderungen dokumentieren; automatisierte Build-/Asset-Validierung und optional Editor-/Playtest-Nachweis, soweit verfügbar. Keine AAA+- oder Spiel-fertig-Behauptungen ohne reale Qualitäts- und Funktionsabnahme.

**P1 – Medien**  
Bild, Audio, Video über echte lokale Werkzeuge/Adapter; Eingangslizenzen, Render-/Transcode-Logs, Format, Laufzeit, Abspielbarkeit und Artefaktexistenz prüfen; kein fingierter Renderstatus.

**P2 – restliche katalogisierte Studios**  
Profile, Schemas und Adapter nach Fachgruppe schrittweise ergänzen. Ingenieurwesen, Elektronik, Wissenschaft, CNC, Transport, Gesundheit o. ä. nur mit geeigneten Fachprüfern, nachvollziehbaren Modellannahmen und klarer Trennung von Entwurf und sicherheitskritischer physischer Ausführung. Ohne geeignete Prüfmittel: `blocked`/`partial`, keine unkontrollierten Maschinenbefehle.

## 7. Sicherheits- und Freigabematrix

| Operation | Vor Ausführung erforderlich | Ohne Freigabe |
|---|---|---|
| Nur lokaler Entwurf / Vorschau / validierte Berechnung | Normale Projektberechtigung | Darf gemäß Projektpolicies laufen |
| E-Mail, Messenger, Kontaktaufnahme | Empfänger, Inhalt, Anhänge, Versandkonto und einmalige Sendefreigabe | Entwurf halten |
| Zahlung, Bestellung, finanzielle Buchung | Betrag, Währung, Empfänger, Konto, Transaktionsdetails und explizite Aktionfreigabe | Keine Transaktion |
| Veröffentlichung / Upload / Deployment / Git-Merge | Zielsystem, Sichtbarkeit, konkrete Änderung und Freigabe | Lokal bleiben |
| Löschen, Datenexport, Rechteänderung, Sicherheit | Exakte Ressource, Folgen und erhöhte Bestätigung | Blockieren |
| Boot, Treiber, OS-Konfiguration, Geräte, Robotik, CNC, Fahrzeug, Maschine | Eignungs-/Interlock-Prüfung, menschliche Freigabe und vorhandener sicherer Controller | Keine physische Aktion |
| Externer KI-Anbieter | Informierte opt-in-Einwilligung, Datenumfang, Kosteninformation | Nur lokal/unsupported |

**Wichtig:** Ein Dialogfeld allein ist kein Sicherheitsmechanismus. Freigaben sind einmalig bzw. eng begrenzt, rollen-/projektgebunden, serverseitig verifiziert, mit Ablauf, Widerruf und Auditspur. Keine Dauerfreigabe aus einem einzigen vagen „Ja“ ableiten.

## 8. UI-/UX-Vorgaben für Trendo OS

- Bestehende Avalonia-/Desktop-Navigation und visuellen Stil integrieren, keine eigenständige zweite App.
- Gleicher Ablauf in allen Studios: Promptfeld, aktuelle Studio-Fähigkeiten, Auftragsübersicht, Rückfragen, Freigabeansicht, Fortschritt, editierbares Ergebnis, QA-Befunde, Vorschau, Datei/Export.
- Frei lesbare Zustände `Noch nicht gestartet`, `Rückfrage`, `Freigabe nötig`, `Wird wirklich ausgeführt`, `Datei erstellt`, `QA bestanden`, `Fehlgeschlagen`, `Nicht unterstützt`.
- Keine blockierende UI; Cancel/Retry bei sicheren Aktionen, verständliche Fehlermeldungen, Tastaturbedienung, Screenreader-Labels, ausreichend große Interaktionsziele.
- Fähigkeit pro Studio transparent markieren. Nicht implementierte Aktionen nicht als funktionsfähige Schaltflächen darstellen.
- Keine realitätsfernen Fortschrittsbalken: nur messbarer Fortschritt oder indeterminierter Aktivitätsindikator.

## 9. Implementierungsphasen mit konkreten Ergebnissen

### Phase A – Pflicht-Audit, bevor Code geändert wird
- Repo/Branch/Commit-SHA dokumentieren; Build-Anweisungen, Tests, CI, Architektur, `StudioCatalog`, `PromptLibrary`, `Intel.AskAsync`, Briefmodelle, Blender/Unreal, Projektisolation und Forensikregeln suchen.
- Ausgangszustand builden und bekannte Fehler festhalten.
- `docs/improvement-42/repository-audit.md`, `studio-capability-matrix.md`, `architecture-decision.md` erstellen.
- Entscheidungsmatrix prüfen: 23/25/34/Language Studio 2.0 = NEIN; 35 = offen; 41 und alle JA bleiben erhalten.

### Phase B – Gemeinsamer Engine-neutrale Workflow-Kern
- Schemas, Validation, Statusautomat, Clarification, Capability Registry, Artifact Persistence, Cancellation, Evidence, Approval-Gateway und QA-Pipeline implementieren.
- Bestehendes KI-System nur per Adapter/Service nutzen. Migrationsarme Integration und rückwärtskompatible Schnittstellen bevorzugen.
- Persistenzänderungen versioniert mit Rollback-/Migrationspfad.

### Phase C – Acht priorisierte Studiogruppen
- Briefstudio → Office → E-Mail → Business → Software → 3D → Games → Medien.
- Pro Gruppe mindestens einen echten vollständigen Happy-Path und einen Failure-/Revision-/Approval-Path nachweisen; keine reine Mock-Abnahme.
- Bei fehlender externer App realen `unsupported`-Fall statt Fake-Ergebnis zeigen.

### Phase D – Vollständige Studioabdeckung und Härtung
- Alle katalogisierten Einträge in gemeinsamer UX und Matrix registrieren.
- Restliche fachspezifische Adapter nach vorhandenen Abhängigkeiten umsetzen; verbleibende Blocker namentlich dokumentieren.
- Sicherheits-, Berechtigungs-, Offline-, Kosten-, Projektisolations- und Forensik-Regressionssuite ausführen.

### Phase E – Übergabe
- Änderungen auf separatem Feature-Branch, nachvollziehbare Commits, PR mit Screenshots/Evidenzen sowie Test-/Build-Ausgaben.
- Keine unaufgeforderte Veröffentlichung/Deployment, kein direkter Merge ohne Freigabe.
- Bedienungsanleitung, Entwickleranleitung, Capability-Matrix, offene Punkte und Abnahmeprotokoll mitliefern.

## 10. Obligatorische Prüffälle

1. Brief erzeugen → speichern → ändern → wieder öffnen → echte Exportdatei prüfen.
2. Office-Tabelle mit bekannten Testdaten/Formeln: deterministisch erwartete Ergebnisse stimmen überein.
3. E-Mail ohne Sendefreigabe: exakt **null** versendete Nachrichten; mit Freigabe exakt eine und belegbar.
4. Cloudprovider ohne opt-in: kein Cloud-Request; verweigerte/entzogene Einwilligung bleibt wirksam.
5. Widersprüchlicher oder unvollständiger Auftrag führt zu gezielter Rückfrage, nicht zu geratenem Ergebnis.
6. Studio ohne Adapter: `unsupported`, keine Phantom-Datei, keine falsche Erfolgsmeldung.
7. Benutzer startet zwei Projekte: keine Artefakt-/Kontext-/Secret-Übertragung zwischen Workspaces.
8. Abbruch, Crash, Neustart und Retry: konsistenter wiederherstellbarer Zustand; kein doppelter Side Effect.
9. Manipulierter Anhang mit Prompt Injection umgeht weder Berechtigungen noch Freigaben.
10. Technische Berechnung: dokumentierte Einheiten, Grenzfälle, reproduzierbare deterministische Werte; KI-Abweichungen führen zu QA-Fehler.
11. Blender-/Unreal-Test: nur bei real verfügbarer Toolchain; Datei und Log nachweisen, sonst sauber `blocked`.
12. Änderungen an bestehenden Modulen 41 sowie 23/25/34/Language Studio 2.0/35 auf Regression bzw. unerlaubte Aktivierung prüfen.
13. 120-Stunden-Forensikregel anhand vorhandener Spezifikation inkl. Zugriff, Ablauf und Löschverhalten testen; keine selbst erfundene Semantik.
14. Risikoaktionen Boot, Maschine, Sicherheit, Zahlung, Publikation, Versand ohne Freigabe sicher blockieren.
15. Capability-Matrix muss jede echte `StudioCatalog`-ID genau einmal enthalten, keine Phantom-IDs und keine fehlenden IDs.

**Abnahmeprinzip:** Jeder Test erhält `passed`, `failed`, `blocked` oder `not_run`, reproduzierbaren Befehl, Testumgebung und Nachweise. `blocked` ist **nicht** `passed`.

## 11. Konkrete Liefergegenstände

- Produktionscode auf einem dedizierten Branch für Verbesserung 42, ohne bestehende Systeme auszutauschen.
- `docs/improvement-42/repository-audit.md`
- `docs/improvement-42/architecture-decision.md`
- `docs/improvement-42/studio-capability-matrix.md`
- `docs/improvement-42/security-approvals.md`
- `docs/improvement-42/test-and-evidence-report.md`
- `docs/improvement-42/user-guide.md`
- Nötige Migrationen, Konfiguration, verifizierte Tools/Abhängigkeiten, Unit-/Integrations-/E2E-Tests.
- PR/Änderungsübersicht mit tatsächlichen Pfaden, Commit-SHA, Build-/Testergebnissen, screenshots/evidence nur bei realer Erzeugung.
- Restblocker mit klarer Ursache, Auswirkungen und nächstem konkreten Entwicklungsschritt.

## 12. Fertigdefinition (Definition of Done)

Verbesserung 42 darf als **vollständig implementiert** bezeichnet werden, wenn **alle** folgenden Aussagen durch Artefakte und Tests belegbar sind:

- Ein gemeinsamer Prompt-to-Action-Workflow ist in die bestehende UI integriert und benutzt bestehende KI-/Prompt-/Projektservices statt Duplikaten.
- Alle tatsächlich vorhandenen Studioeinträge sind vollständig inventarisiert, profiliert und in der Matrix erfasst.
- Die acht priorisierten Studiogruppen verfügen über reale, verwendbare Fachadapter mit echter End-to-End-Ausführung und editierbarem Ergebnis, soweit die benötigten Tools installiert und zulässig sind.
- Jede als `ready` markierte Studioaktion besteht Validierung, QA, Vorschau und Artefaktprüfung.
- Verbleibende Studioaktionen stehen transparent auf `partial`/`blocked`/`not_implemented`; keine Gesamtfertigmeldung solange geforderte Funktionen noch fehlen.
- Lokale KI wird bevorzugt; kein Cloudzugriff ohne Zustimmung; risikoreiche Seiteneffekte sind technisch durch Freigaben abgesichert.
- Deterministische Fachprüfungen und Testnachweise liegen vor; keine erfundenen Ergebnisse.
- Bestehende Entscheidungen/Module und die 120-Stunden-Forensikregel bestehen die Regressionstests.
- Build, Tests und PR-Übergabedokumentation sind nachvollziehbar; bekannte Blocker offen ausgewiesen.

## 13. Exakte Ausgabeanforderung an Claude Code

Starte mit einem knappen **Ist-Stand-Audit mit echten Repository-Pfaden und Commit-SHA**, anschließend implementiere den Code inkrementell. Zeige nach jeder Phase:

```text
Phase:
Geprüfter Branch/Commit:
Reale geänderte Dateien:
Wiederverwendete vorhandene Komponenten:
Echte funktionierende Aktionen:
Blockierte/nicht implementierte Aktionen:
Sicherheits-/Freigabeprüfung:
Build- und Testbefehle:
Tatsächliches Testergebnis / Logs / Artefaktpfade:
Bekannte Fehler / nächste Phase:
```

**Beginne mit Phase A und setze danach Phase B–E um, soweit Codezugriff, Entwicklungsumgebung und Werkzeuge dies tatsächlich zulassen.** Frage nur bei echten fachlichen oder sicherheitsrelevanten Blockern; niemals künstlich stoppen, weil eine einzelne Pfad- oder Klassenbezeichnung vom Auftrag abweicht. Dokumentiere jede Abweichung ehrlich. Keine Behauptung, die Software sei komplett fertig, bevor die obige Definition of Done tatsächlich erfüllt ist.

---
**Ende des autorisierten Build-Auftrags für Verbesserung 42.**


## 9. Entwicklungsübergabe – Arbeitsweise für Claude Code

1. Zuerst Repository, Dateien und vorhandene Funktionen lesen; nicht blind neu implementieren.
2. Alle **JA** als genehmigte fachliche Spezifikationspakete behandeln; für Nr. 1–40 und den exakten Wortlaut Nr. 41–92 vorhandene Originalbeschlüsse/Spezifikationen heranziehen, statt Details zu erfinden.
3. Alle **NEIN** respektieren; insbesondere 23, 25, 34, Language Studio 2.0 und 86. Punkt 35 bleibt offen.
4. Pro Studio eine Anforderungs-/Adapter-/Test-Matrix erstellen; bereits implementierte Features ermitteln und Dubletten vermeiden.
5. Technische Umsetzung inkrementell in bestehenden Architekturpfaden, mit Tests und menschlicher Freigabe für gefährliche Aktionen.
6. Änderungen in Git versionieren, Test- und Belegartefakte erzeugen und vollständig zwischen PLANNED, IMPLEMENTED, TESTED und ACCEPTED unterscheiden.
7. Nichts als vollständig/fertig bezeichnen, solange nicht nachweislich real umgesetzt und abgenommen.

---
**Ende der konsolidierten Masterdatei. Alle JA-/NEIN-/OFFEN-Entscheidungen sind erfasst; nicht verfügbare ursprüngliche Katalogtexte sind ausdrücklich als extern beizuziehen markiert.**