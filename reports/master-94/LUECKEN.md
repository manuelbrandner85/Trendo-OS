# Lücken 42–94

Stand der Lücken: Branch vor dem Nachzug von `origin/main` `2eef442` (Merge #237, Design 41). Die Paketbewertung unten ist damit nicht neu vermessen. Masterdatei `docs/spec/MASTER_BIS_94.md`, SHA-256 `76f963654854b380679b4821e10e3ba283d596705d023b63975cc71fcd1ce6b2`, 59976 Bytes, unverändert übernommen.

Der Status gilt für das Paket im Master, nicht für eine ältere Studio-Funktion.

- **APPROVED_SPEC:** Beschluss ja. Das Paket ist in diesem Stand nicht gebaut.
- **IN_PROGRESS:** Es gibt Code auf `main` oder einen offenen PR für einen Teil. Offene PRs sind nicht gemergt und in diesem Lauf nicht ausgeführt.
- **IMPLEMENTED:** Der geforderte Umfang liegt auf `main`. Trifft auf keines der Pakete 42–94 zu.
- **TESTED:** IMPLEMENTED und mit Test für genau diesen Umfang. Ebenfalls keines der Pakete.
- **NEIN:** nicht bauen.

Ein älteres Studio (Briefe, 3D, Forensik, Handy) ist kein Beleg, dass das zugehörige Paket fertig ist. Katalog: 54 Einträge in `StudioCatalog` und `CapabilityCatalog`. Geprüft durch `PromptBarTests.Katalog_und_Abdeckung_stimmen`.

Keine zweite Engine. Der Ablauf ist `StudioWorkflow` in `src/Trendo.Core/PromptAction`. Offene PRs #244, #245, #247, #249–#257 hängen Adapter dort ein. PR #246 (`Trendo.Core/Website`) nennt `StudioWorkflow` im Diff nicht.

## Phase 1 dieses Auftrags

Seite „Ein Arbeitsplatz“, ein Adapter `arbeitsplatz.blatt` nur für X-11, Prüfung liest die geschriebene Datei. 53 Studios bleiben ohne Adapter. Das hebt 42 und 43 nicht auf TESTED. `dotnet test Trendo.sln -c Release` an diesem Stand: Api 15 bestanden, Core 526 bestanden und 10 übersprungen, App 215 bestanden, jeweils 0 Fehler. Einzelheiten: `tasks/T-0094-master-94.bericht.md`.

## Register

| Nr | Paket | Studio im Katalog | Status | Beleg | Größte Lücke |
|---:|---|---|---|---|---|
| 42 | Prompt-zu-Auftrag, 20 Bereiche | alle 54 | IN_PROGRESS | Kern auf `main` in #241, Tests `PromptActionTests`. Matrix `docs/improvement-42/studio-capability-matrix.md`. Phase 1 ergänzt nur X-11. | Acht Studiogruppen und die meisten der 20 Bereiche fehlen auf `main`. „Bereit“ steht bei keiner Aktion. |
| 43 | Ein Arbeitsplatz, 20 Funktionen | alle Studios, Oberfläche | IN_PROGRESS | Spec #242 und `docs/spec/IMPROVEMENT_43.md` (11 Punkte). Offen #243: Plan, Los, Vorschau, nicht gemergt. Phase 1: Seite „Ein Arbeitsplatz“. | Die 20 Funktionen sind im Master nicht einzeln ausformuliert. Punkte 3–8 (Webseite) sind nicht in #243. |
| 44 | Fachprogramme nur bei Bedarf | CORE-05, CORE-06, vorhandene Starts | APPROVED_SPEC | Kein Fusion-Modul auf `main`. Offen #251 und #253, Adapter bleiben ohne Blender/Unreal aus. | Kein geprüfter Universal-Adapter. Blender und Unreal sind hier nicht im PATH. |
| 45 | Office, 20 Funktionen | SYS-10 | IN_PROGRESS | Älteres Office auf `main` (`Sys10Tests`). Offen #245: eine SUMME-Tabelle, nicht gemergt. | Kein Textverarbeitungs-, Präsentations- oder Produktionspaket aus dem Prompt. |
| 46 | E-Mail, 20 Funktionen | SYS-09 | IN_PROGRESS | `MailCenter` auf `main`. Offen #247: Entwurf als Datei, Senden `CanPerformSideEffect = false`. | Kein Postfach, kein SMTP. Versand ist nicht angeschlossen. |
| 47 | Finanzen, 20 Funktionen | SPEC-11 | IN_PROGRESS | `Finance` auf `main` (`W5FinanceTests`). Offen #249: Rechnung rechnen, Zahlen blockiert. | Keine Buchung, keine Steuerprüfung. Der Zahlungsweg ist nicht entschieden, Zahlen bleibt aus. |
| 48 | Software, 23 Funktionen | CORE-07, X-10, X-03, X-06 | IN_PROGRESS | Dev-Studio auf `main`. Offen #250 (Vorlage, kein Push), #248 (Nachbau, nutzt `AskAsync`), #257 (Seite, GitHub nur erklären). | Kein Build-Debug-Release-Paket. Push und Veröffentlichung bleiben zu. |
| 49 | Blender und 3D, 25 Funktionen | CORE-05 | IN_PROGRESS | 3D-Studio auf `main` (#230, `W4Studio3DTests`). Offen #251: OBJ, Blender-Aktion blockiert ohne Programm. | Die 25 Funktionen sind nicht katalogisiert. Kein echter Blender-Lauf auf diesem Image. |
| 50 | Spiele, 40 Funktionen | CORE-06 | IN_PROGRESS | Spielestudio auf `main` (`W4GameTests`). Offen #253: Browser-Spiel, Unreal nur wenn installiert, Start blockiert. | Das Spielestudio arbeitet mit einem vom Benutzer gewählten Projektordner. AION und GENESIS haben keinen festen Ort. Die Trennung gilt nur, wenn so ein Projekt vorhanden ist. Kein Unreal-Lauf. |
| 51 | Film, 40 Funktionen | CORE-08, SPEC-15 | IN_PROGRESS | Medien- und Live-Studio auf `main` (#228, `LiveStreamTests`). Offen #254 (Bild umwandeln), #256 (Sendeplan, Senden blockiert). | Kein Schnitt-, Kamera- oder Effektpaket. Die 40 Funktionen stehen nur als eine Zeile im Master. |
| 52 | Bild und Grafik, 40 Funktionen | CORE-08, X-07 | IN_PROGRESS | Medienstudio auf `main`. Offen #254 und #256 (Lizenz nur merken, nicht holen). | Keine Bildbearbeitung, kein geprüftes Grafikpaket. |
| 53 | Musik, Audio, Stimme, 40 Funktionen | SYS-02, CORE-08 | APPROVED_SPEC | Abspielen gehört zum älteren Helfer. Kein Audio-Adapter in den offenen Prompt-PRs. #254 betrifft ein Bild. | Keine Produktion, keine Synchronisation, kein Stimm-Adapter. |
| 54 | Webseite, 40 Funktionen | CORE-07 | IN_PROGRESS | Offen #246, nicht gemergt, nicht an `StudioWorkflow` gehängt. Punkte 3–8 aus Verbesserung 43. | Veröffentlichung nur nach Freigabe ist dort nicht an das vorhandene Tor angeschlossen. Three.js-Einbindung hier nicht ausgeführt. |
| 55 | Architektur, 40 Funktionen | SPEC-01 | IN_PROGRESS | Flächenrechnung im älteren Studio. Offen #255: `wohnung.flaeche`. | Kein CAD, keine Zeichnung, kein 3D-Raum. |
| 56 | Konstruktion und Simulation, 60 Funktionen | SPEC-03 | IN_PROGRESS | Balkenrechnung im älteren Studio. Offen #255: `balken.pruefen`. | Kein FreeCAD, CalculiX oder OpenFOAM. Keine 60 Funktionen. |
| 57 | Elektronik, 50 Funktionen | SPEC-02 | IN_PROGRESS | Rechner im älteren Studio. Offen #255: `schaltung.rechnen`. | Kein KiCad, kein ngspice, kein Platinenlayout. |
| 58 | Forschung, 50 Funktionen | SPEC-05 | IN_PROGRESS | Messwerte im älteren Studio. Offen #255: `messung.auswerten`. | Kein Jupyter, keine Herkunftskette, keine reproduzierbare Studie. |
| 59 | Produkt und Mode, 50 Funktionen | SPEC-08 | IN_PROGRESS | Größen im älteren Studio. Offen #256: `groesse.pruefen`. | Kein CAD, kein Stoff, keine technische Prüfung über die Größe hinaus. |
| 60 | 3D-Scan, 50 Funktionen | SPEC-09 | APPROVED_SPEC | Fotos zu einem Modell auf `main` (#233, `PhotoScanTests`). Offen #256: `scan.starten` blockiert. | Meshroom, CloudCompare und das 50er-Paket fehlen. Der alte Scan ist nicht das Paket. |
| 61 | Fertigung, CNC, Druck, 50 Funktionen | SPEC-14 | IN_PROGRESS | Drehzahl im älteren Studio. Offen #255: Rechnen ja, `fraese.starten` mit `CanPerformSideEffect = false`. | Keine Maschine, kein PrusaSlicer, kein LinuxCNC. Bewegung bleibt aus. |
| 62 | Transport und Logistik, 50 Funktionen | SPEC-16 | IN_PROGRESS | Fahrtenkosten im älteren Studio. Offen #255: `fahrt.kosten`. | Keine Route, keine Fähre, keine Lenkzeiten, keine Simulation. |
| 63 | Sicherheit und Forensik, 50 Funktionen | SPEC-12 | APPROVED_SPEC | `ForensicsLab.MaxLife` ist 120 Stunden, Test `PromptActionTests.Forensik_120_Stunden`. Verbesserung 34 bleibt NEIN in `decisions.yaml`. | Kein neues Schutzpaket. Die Frist wird nicht geändert. Ein eigener Virenscanner wird nicht gebaut. |
| 64 | Karten, 50 Funktionen | SPEC-13 | IN_PROGRESS | Routenlogik im älteren Studio. Offen #255: `route.messen`. | Kein QGIS, kein GDAL, kein Digital Earth. |
| 65 | VR, AR, MR, 50 Funktionen | SPEC-04 | IN_PROGRESS | Ältere Raumskizze. Offen #256: `bildzeit.pruefen`. | Kein OpenXR, keine Brille, kein Unreal-VR. |
| 66 | Lernen, 50 Funktionen | SPEC-06 | IN_PROGRESS | Karteikarten im älteren Studio. Offen #256: `karte.anlegen`. | Language Studio 2.0 bleibt NEIN. SPEC-07 bekommt keinen neuen Adapter. Kein Kursystem. |
| 67 | Robotik, 50 Funktionen | CORE-09 | APPROVED_SPEC | Kamerabild und Roboter nur nach Freigabe auf `main` (#227, `CameraRobotTests`). | ROS 2, Gazebo und MoveIt fehlen. Das 50er-Paket ist nicht beschrieben außer der Masterzeile. |
| 68 | Daten und Kennzahlen, 50 Funktionen | keins, nahe SPEC-05 und SPEC-11 | APPROVED_SPEC | Kein BI-Studio im Katalog. | DuckDB, Polars und Superset sind nicht angebunden. Keine zweite Daten-Engine anlegen. |
| 69 | Buch und Satz, 50 Funktionen | keins, nahe SYS-10 und Briefe | APPROVED_SPEC | Kein Verlagsstudio. | Kein Pandoc, calibre oder Scribus. Kein EPUB-Paket. |
| 70 | Smart Home, 50 Funktionen | SPEC-10, SYS-07 | APPROVED_SPEC | Geräte nennen und Messwerte zeigen, älterer Stand. #255 zeigt nur eine Messreihe. | Kein Home Assistant, MQTT oder Matter. Keine Gerätebefehle. |
| 71 | Abläufe, 50 Funktionen | SYS-05 | IN_PROGRESS | `WorkflowEngine` auf `main`. Offen #257: `ablauf.planen`. | Kein n8n, kein Node-RED, keine API-Triggers mit Freigabe. |
| 72 | Persönliche Planung, 50 Funktionen | SYS-02, SYS-09 | APPROVED_SPEC | Notizen und Termine im älteren Stand. | Kein Lebensplan, kein Abgleich über Geräte. |
| 73 | Live und Veranstaltung, 50 Funktionen | SPEC-15 | IN_PROGRESS | Aufnahme und Senden nur nach Freigabe auf `main` (#228). Offen #256: Plan ja, `sendung.senden` blockiert. | Kein OBS, kein vMix, keine Regie. |
| 74 | Wissen, 50 Funktionen | CORE-10, SYS-08 | IN_PROGRESS | Wissensseite auf `main`. Offen #256: `wissen.merken`. Recherche bleibt lesen. | Keine semantische Suche, keine Mandantentrennung über den Projektordner hinaus. |
| 75 | Digitaler Zwilling, 50 Funktionen | SPEC-10 | APPROVED_SPEC | Nur Messwerte. | Kein OPC UA, kein OpenModelica, keine Anlage. |
| 76 | Medizinische Ansicht, 50 Funktionen | keins | APPROVED_SPEC | Kein Medizinstudio im Katalog. Speichern ist erlaubt, der Speicher ist nicht gebaut. | Lokal, verschlüsselt über den Secret Store (auf Windows DPAPI), Opt-in je Projekt, löschbar. Keine Cloud ohne eigene Freigabe. Keine klinische Diagnoseautomatik. |
| 77 | Agenten, 50 Funktionen | CORE-02, CORE-03 | APPROVED_SPEC | `IntelligenceCore.AskAsync` und `Agents` auf `main`. | Kein zweites Agentensystem. Das 50er-Paket ist nicht gebaut. |
| 78 | Verträge, 50 Funktionen | STUDIO-LETTER-01 | APPROVED_SPEC | Briefstudio mit Hinweis, keine Rechtsberatung. Offen #244 schreibt DOCX, nicht gemergt. | Kein Länderrecht, keine Compliance-Prüfung, kein Anwaltsersatz. |
| 79 | Server und DevOps, 50 Funktionen | SYS-04, SYS-06, X-06 | APPROVED_SPEC | Installieren nur prüfen, Teilen über das vorhandene Tor. #257 erklärt GitHub nur. | Kein Docker, Terraform, Grafana oder Kubernetes. Kein Deployment. |
| 80 | Apps ohne Code, 50 Funktionen | X-03 | IN_PROGRESS | PageBuilder auf `main`. Offen #257: `seite.schreiben`. | Kein vollständiger App-Baukasten, kein echter Build aus der Fläche. |
| 81 | Modelle und Feintuning, 50 Funktionen | X-05, CORE-02 | IN_PROGRESS | Lokale KI auf `main`. Offen #252: lokale KI, kostenlose Online-Dienste, Wahl. Nicht ausgeführt. | Kein Feintuning, kein Benchmark auf echter Hardware. Kostenpflichtige API bleibt optional und ist nicht vorausgesetzt. |
| 82 | Erweiterungen, 50 Funktionen | X-04 | IN_PROGRESS | `ExtensionCatalog` auf `main`. Offen #257: Entwurf. | Kein Marktplatz, keine Sandbox-Installation, kein SDK-Paket. |
| 83 | Qualität, 50 Funktionen | SYS-13 | APPROVED_SPEC | Qualitätsseite auf `main` (`Sys13Tests`). Phase 1 prüft nur die Arbeitsblatt-Datei. | Kein studioübergreifender Release-Wächter. QA-017, QA-019 und Codesignatur gelten nicht als bestanden. |
| 84 | Geräteverbund, 50 Funktionen | CORE-04, X-08 | APPROVED_SPEC | Handy per Kabel, WLAN und Bluetooth auf `main` (#240, `W7PhoneLinkTests`). | Keine verteilte Rechnung, kein Fernstart von Blender oder Unreal. Braucht echte Geräte. |
| 85 | Leistung und Reparatur, 50 Funktionen | X-09, X-02 | IN_PROGRESS | Messwerte auf `main`. Offen #257: `leistung.lesen`. X-02 bleibt wegen Verbesserung 35 ohne Aktion. | Selbstheilung darf 35 nicht einschalten. Rollback eines Studios ist nicht gebaut. |
| 86 | Kunden und Service, 50 Funktionen | keins als Paket | NEIN | Master, Abschnitt 1 und 4. Kein Eintrag `IMP-86` in `decisions.yaml`. Kein PR dafür gesehen. | Nichts aus dem Paket bauen. Ältere Kommunikationsfunktionen bleiben, was sie sind. |
| 87 | Fahrzeuge konstruieren, 50 Funktionen | SPEC-16 nur Kosten | APPROVED_SPEC | Kein Konstruktionsstudio. | Braucht Blender oder Unreal und eine Entscheidung, welches Fahrzeug gemeint ist. |
| 88 | Weltraum, 50 Funktionen | keins | APPROVED_SPEC | Kein Studio, kein NASA-GMAT-Adapter. | Reine Spec-Zeile plus der Master. Keine Bahnrechnung. |
| 89 | Onlineshop, 50 Funktionen | STUDIO-ONLINE-BUSINESS-01 | IN_PROGRESS | Business-Studio auf `main`. Offen #249: Bewertung rechnen, Zahlung blockiert. | Kein Shop, kein Stripe, kein PayPal, kein Checkout. Der Zahlungsweg ist nicht entschieden, Zahlen bleibt aus. |
| 90 | Motion Capture, 50 Funktionen | keins, nahe CORE-05 und CORE-06 | APPROVED_SPEC | Kein Capture-Studio. | Braucht Blender oder Unreal und Aufnahmegerät. |
| 91 | Materialien und Render, 50 Funktionen | nahe CORE-05 | APPROVED_SPEC | Kein Materialstudio. | Kein PBR-Paket, keine VRAM-Messung, kein Render-Lauf. |
| 92 | Welten und Gelände, 50 Funktionen | nahe CORE-06 | APPROVED_SPEC | Kein Welt-Studio. | World Partition, HLOD und Streaming sind nicht im Repo. |
| 93 | Lebendige Welt, 50 Funktionen | keins | APPROVED_SPEC | Vollständige Spec in Master Abschnitt 7. Kein Code, kein Test. | NPCs, Savegames und Unreal-KI fehlen. Ein Spielprojekt wählt der Benutzer. AION und GENESIS bleiben getrennt, sobald so ein Projekt existiert. |
| 94 | Licht, Effekte, Zerstörung, 50 Funktionen | keins | APPROVED_SPEC | Vollständige Spec in Master Abschnitt 6. Kein Code, kein Test. | Niagara, Chaos und Blender-Simulation fehlen. Ohne Engine nur als Entwurf kennzeichnen, nicht als fertigen Effekt. |

## Was nicht vermischt werden darf

- NEIN bleiben 23, 25, 34, Language Studio 2.0 und 86. `decisions.yaml` hat 23, 25, 34 und LANG-2.0 als REJECTED, 35 als UNDECIDED. Die Datei ist nicht erweitert worden.
- 35 wird nicht aktiviert. X-02 bleibt ohne Prompt-Aktion. Ob 35 je eingeschaltet wird, ist weiter offen.
- Zahlen (47, 89) bleibt aus. Welcher Zahlungsweg je angeschlossen wird, ist weiter offen.
- 76 darf Gesundheitsdaten speichern, sobald der Speicher die Regeln einhält: nur lokal, verschlüsselt im Secret Store (auf Windows DPAPI), Opt-in je Projekt, löschbar, keine Cloud ohne eigene Freigabe, keine klinische Diagnoseautomatik. Dieser Stand speichert keine Gesundheitsdaten.
- Spielstudios nutzen einen vom Benutzer gewählten Projektordner. Ein fester Ort für AION oder GENESIS ist nicht nötig. Die Trennung aus dem Master gilt erst, wenn so ein Projekt vorhanden ist. Dieser Stand ändert dort nichts.
- Abnahme für Blender, Unreal und LibreOffice ist der Windows-PC DESKTOP-9JVIIPU. Dieses Cloud-Image ist diese Abnahme nicht.
- Die 120-Stunden-Regel steht in `ForensicsLab.MaxLife`. Sie ist nicht geändert.
- #237 ist auf `origin/main`. Offen und vom Integrator in dieser Reihenfolge vorgesehen: #246, #248, #252, danach der Stapel #243 von unten (#243, #244, #245, #247, #249 und weiter). Sie ändern dieselben Adapterdateien. Dieser Branch hat sie noch nicht gezogen.
- Phase 1 hängt eine Seite an. Das Startmenü-Layout von Design 41 ist auf diesem Branch noch der Stand vor #237.
