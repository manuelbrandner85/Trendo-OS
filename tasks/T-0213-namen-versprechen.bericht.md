# T-0213 Bericht: Studionamen und Untertitel – Vorschlagsliste zur Entscheidung

**Auftrag:** Jeder Studioname verspricht nur, was das Studio heute kann. Nur Liste, kein Code; `StudioCatalog.cs` unverändert.
**Branch:** `claude/T-02xx` (Claude Code)
**Grundlage:** `StudioCatalog.cs` (Name, Untertitel), `requirements.yaml` (Status, Hinweise), `docs/spec/GAP_ANALYSIS.md`, Befunde der Aufträge T-0204…T-0212, Klick-Audit `reports/audit/`.
**Lesart:** A = Name/Untertitel anpassen (sofort möglich), B = Funktion ergänzen, damit der Name stimmt (Aufwand, Auftrag nötig). „passt“ = kein Handlungsbedarf erkennbar. Nicht jedes Studio wurde für diese Liste einzeln am PC durchgeklickt; „passt“ heißt: Register und Befunde zeigen keinen Widerspruch.

## Handlungsbedarf (Entscheidung Manuel)

| ID | Name heute | Untertitel heute | Was heute wirklich geht | Vorschlag A (ehrlicher Text) | Vorschlag B (ergänzen) |
|---|---|---|---|---|---|
| CORE-06 | Spiele bauen | Eigene Spiele bauen – mit der Unreal Engine. | Drei Browser-Spielvorlagen; Unreal nur erkennen/öffnen, kein Spielbau darin | „Kleine Spiele bauen“ – „Browser-Spiele aus Vorlagen gestalten; Unreal öffnen, wenn installiert.“ | T-0204: eigene Regeln, Level-Editor, Export, Unreal-Projekt anlegen |
| SPEC-15 | Live streamen | Livestreams und Sendungen planen und durchführen. | Planungsrechner (Bitrate, Ablauf); kein eigenes Senden | „Livestream planen“ – „Bitrate, Ablauf und Technik für deinen Stream planen.“ | OBS-Anbindung mit Freigabe (Verbesserung 73) |
| CORE-09 | Kameras & Roboter steuern | Kameras und Roboterarme steuern und auswerten. | Bildauswertung und Rechner; keine Live-Kamera, keine Robotersteuerung | „Kamera- & Roboterdaten auswerten“ – „Bilder auswerten und Roboterbewegungen planen.“ | Live-Kamera, Simulation zuerst (Verbesserung 67), reale Steuerung nur mit Freigabe |
| SPEC-09 | Gegenstände einscannen | Gegenstände und Räume einscannen und vermessen. | Punktwolken laden, bearbeiten, vermessen; keine Aufnahme | „3D-Scans bearbeiten“ – „Punktwolken laden, säubern und vermessen.“ | Aufnahme per Handy/Kamera (Photogrammetrie, Verbesserung 60) |
| CORE-07 | Webseite bauen | Webseiten und Apps programmieren. | Projektvorlagen anlegen, Ordner öffnen, Testen, Git-Stand; kein Editor, keine Vorschau im Studio | „Software-Projekte starten“ – „Vorlagen für Webseiten und Apps anlegen und testen.“ | T-0206: Editor, Live-Vorschau, „In VS Code öffnen“ |
| SPEC-07 | Texte übersetzen | Texte übersetzen und Programme eindeutschen. | Abgelehnt (SPEC-07 / Language Studio 2.0) – „Programme eindeutschen“ verspricht genau den abgelehnten Ausbau | Untertitel: „Texte mit der lokalen KI übersetzen.“ (nur Grundfunktion) | – (abgelehnt, nicht bauen) |
| SYS-06 | Dateien teilen | Dateien im Netzwerk teilen und von unterwegs zugreifen. | Ordnerfreigabe nur im Heimnetz mit Freigabe; „von unterwegs“ nicht vorhanden | Untertitel: „Ordner im Heimnetz teilen – nur mit deiner Freigabe.“ | Fernzugriff (Verbesserung 39/84) |
| CORE-04 | PC, Pi & Handy verbinden | Trendo auf PC, Raspberry Pi und Android gemeinsam nutzen. | PC-Seite fertig; Gegenstelle Pi/Android nur mit nachgebildeten Geräten getestet | Untertitel: „PC mit Raspberry Pi und Handy koppeln (Gegenstellen in Arbeit).“ | Pi-Edition und Android-Gegenstelle (Plan: A-Edition, I) |
| SYS-01 | Startmodus wählen | Erkennt das Gerät und startet Trendo passend. | PC-Seite fertig; Pi/Android-Gegenstelle fehlt | passt mit Zusatz „(PC)“ bis die Gegenstellen da sind | wie CORE-04 |
| X-08 | Handy verbinden | Verbindet Trendo mit deinem Handy per Kabel, WLAN oder Bluetooth. | Android über adb (Infos, Senden, Bildschirmfoto); WLAN/Bluetooth nur teilweise | Untertitel: „Android-Handy per Kabel verbinden; WLAN und Bluetooth im Aufbau.“ | T-0217-Stand prüfen, Bluetooth/WLAN vervollständigen |
| X-01 | Großprojekte umsetzen | Mehrere Studios arbeiten zusammen an einem Projekt. | Projektfusion und Übergaben; keine autonome Gesamtproduktion | passt – Untertitel nicht übertreiben („arbeiten zusammen“ ist zutreffend für Übergaben) | Verbesserung 42/43/77 |
| CORE-05 | 3D-Modelle erstellen | 3D-Modelle bauen und ansehen – mit Blender. | Eigene 3D-Teile, Vorschau, Export; Blender öffnen/exportieren, wenn installiert | Untertitel: „3D-Modelle bauen und ansehen – Blender optional.“ | – |
| SPEC-04 | VR & AR gestalten | Virtuelle Räume und 3D-Modelle für die Brille vorbereiten. | Vorbereitung/Export, keine Brillen-Vorschau | passt („vorbereiten“ ist ehrlich) | OpenXR-Vorschau (Verbesserung 65) |
| CORE-03 | KI-Team einsetzen | Mehrere KI-Helfer arbeiten gemeinsam an einer Aufgabe. | 12 Rollen; braucht laufende lokale KI | Untertitel: „Mehrere KI-Rollen arbeiten an einer Aufgabe (lokale KI nötig).“ | – |

## Ohne Handlungsbedarf (passt)

| ID | Name | Untertitel |
|---|---|---|
| STUDIO-LETTER-01 | Briefe schreiben | Schreibt Briefe an Ämter, Firmen und Vermieter – mit Vorlagen und Fristen. |
| SYS-10 | Texte & Tabellen | Texte, Tabellen und Präsentationen erstellen und bearbeiten. |
| SYS-09 | E-Mails & Termine | E-Mails, Kontakte und Termine an einem Ort. |
| X-11 | Neues Projekt starten | Ein Startpunkt für jedes Projekt – vom Dokument bis zum Video. |
| SYS-02 | Kleine Helfer & Musik | Kleine Helfer: Rechner, Notizen, Bildschirmfoto, Musik und Videos abspielen. |
| STUDIO-ONLINE-BUSINESS-01 | Online verkaufen | Geschäftsidee planen, gründen und online verkaufen. (Hinweis: Zahlung/Versand bleiben freigabepflichtig) |
| SPEC-11 | Geld & Rechnungen | Haushaltsbuch, Rechnungen, Kredite und Steuern im Blick. |
| CORE-08 | Videos & Fotos bearbeiten | Videos schneiden, Musik aufnehmen und Bilder gestalten. (T-0205 verbessert Bedienung) |
| SPEC-08 | Kleidung & Produkte entwerfen | Kleidergrößen, Schnitte und Produktideen. |
| X-07 | Freie Bilder & Klänge finden | Kostenlose Bilder, Klänge und Schriften finden. |
| CORE-12 | KI-Anweisungen schreiben | Gute Anweisungen für die KI schreiben und sammeln. |
| SPEC-01 | Wohnung planen | Räume ausmessen, Flächen berechnen und einrichten. |
| SPEC-02 | Schaltungen berechnen | Widerstände, LEDs und Schaltungen berechnen. |
| SPEC-03 | Statik nachrechnen | Balken, Kräfte und Materialien nachrechnen. |
| SPEC-10 | Geräte überwachen | Messwerte von Geräten und Sensoren beobachten. |
| SPEC-13 | Routen & Karten | Routen planen, Strecken messen und GPS-Dateien ansehen. |
| SPEC-14 | Fräsen & Fertigen | Fräsen, Drehzahlen und Werkstücke planen. |
| SPEC-16 | Auto & Fahrten | Verbrauch, Fahrtenbuch und Kosten rund ums Auto. |
| SPEC-05 | Messwerte auswerten | Messwerte auswerten und als Diagramm zeigen. |
| SPEC-06 | Lernen mit Karteikarten | Karteikarten, Vokabeln und Prüfungen üben. |
| CORE-10 | Wissen merken | Merkt sich, was du gelernt und gemacht hast. |
| SYS-08 | Sicher recherchieren | Sicher im Netz recherchieren – nur lesen. |
| CORE-02 | KI fragen | Dein Helfer, der Fragen beantwortet und Aufgaben erledigt. |
| X-05 | Kostenlose KI finden | Kostenlose KI-Modelle finden und als Ersatz nutzen. |
| SYS-05 | Aufgaben automatisieren | Wiederkehrende Aufgaben automatisch erledigen lassen. |
| X-03 | Apps zusammenklicken | Apps zusammenklicken – ganz ohne Programmieren. |
| X-06 | Projekte von GitHub holen | Freie Projekte von GitHub finden und nutzen. |
| X-04 | Trendo erweitern | Neue Funktionen für Trendo hinzufügen. |
| X-10 | Programme nachbauen | Programme rechtlich sauber neu entwickeln. |
| CORE-01 | Desktop einrichten | Fenster, Startmenü und Arbeitsfläche von Trendo. |
| CORE-11 | Daten schützen | Schützt deine Daten, Passwörter und Sicherungen. |
| SPEC-12 | System auf Gefahren prüfen | Prüft das System auf Gefahren und sichert Hinweise. |
| SYS-03 | Surfen & Herunterladen | Im Internet surfen und Dateien laden. |
| SYS-04 | Programme installieren | Programme installieren, aktualisieren und entfernen. |
| SYS-07 | Geräte erkennen | Drucker, Kameras und andere Geräte erkennen. |
| SYS-11 | Programme starten | Programme ohne Installation starten und verknüpfen. |
| SYS-12 | Schrift & Farben anpassen | Größere Schrift, Farben und einfache Bedienung. |
| SYS-13 | Qualität prüfen | Prüft, ob alles zusammen funktioniert. |
| X-02 | Einrichten & Reparieren | Richtet Trendo automatisch ein und repariert Fehler. |
| X-09 | Leistung anzeigen | Zeigt, was Speicher und Prozessor gerade tun. |

## Entscheidung nötig
Für jede Zeile der ersten Tabelle: **A** (Text ändern – Claude setzt es danach in `StudioCatalog.cs` um und passt die Tests an) oder **B** (Funktion ergänzen – eigener Auftrag). SPEC-07 nur A, weil der Ausbau abgelehnt ist.

## Nicht geprüft
Keine einzelne Bedienprüfung jedes „passt“-Studios am PC für diese Liste; Grundlage sind Register, Klick-Audit und Auftragsbefunde.
