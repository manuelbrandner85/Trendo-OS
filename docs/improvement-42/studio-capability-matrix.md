# Studio-Abdeckung – Verbesserung 42

Erzeugt aus `CapabilityCatalog` und den wirklich registrierten Adaptern. `ready` steht nur an einem Adapter, dessen Beleg ein vorhandener Test ist. Einträge ohne Adapter sind `not_implemented`.

| Studio-ID | Titel | Bereich | Vorhandene Bausteine | Vorgesehene Aktion | Eingaben | Adapter | Ausgabe | Reife | Beleg | Blocker |
|---|---|---|---|---|---|---|---|---|---|---|
| STUDIO-LETTER-01 | Briefe schreiben | Arbeiten & Büro | Letters, LetterArchive, LetterCase, PdfExport | Brief als DOCX entwerfen | Absender, Empfänger, Betreff, Text | brief.schreiben | DOCX, optional PDF | partial | PromptActionTests.Brief_wird_geschrieben_und_wieder_gelesen | PDF nur, wenn LibreOffice (soffice) vorhanden ist. |
| SYS-10 | Texte & Tabellen | Arbeiten & Büro | Ooxml, Csv, Pdf, Writing | Dokument oder Tabelle mit geprüfter Formel | Text oder Zellen | tabelle.rechnen | DOCX, XLSX | partial | PromptActionTests.Tabelle_rechnet_formel | Nur Grundrechenarten und SUMME. LibreOffice rechnet die Datei nicht nach. |
| SYS-09 | E-Mails & Termine | Arbeiten & Büro | MailCenter | Entwurf, Versand nur mit Freigabe | Empfänger, Betreff, Text | mail.senden, mail.entwurf | EML-Entwurf | partial | PromptActionTests.Mail_senden_schickt_nichts, PromptActionTests.Mail_entwurf_wird_gelesen | Versand ist nicht angeschlossen. Trendo schickt selbst keine E-Mail. |
| X-11 | Neues Projekt starten | Arbeiten & Büro | ProjectStore | Projekt anlegen | Projekt-ID | keiner | Projektordner | not_implemented | — | Kein Fachadapter registriert. |
| SYS-02 | Kleine Helfer & Musik | Arbeiten & Büro | Calc, NoteStore, FileShelf | Notiz oder Rechnung | Text oder Ausdruck | keiner | Notiz, Zahl | not_implemented | — | Kein Fachadapter registriert. |
| STUDIO-ONLINE-BUSINESS-01 | Online verkaufen | Business & Geld | BusinessStudio | Geschäftsplan rechnen | Preis, Kosten, Menge | geschaeft.bewerten | Markdown | partial | PromptActionTests.Geschaeft_bewertet_ohne_buchung | Nur eine Schätzung aus deinen Zahlen. Kein Shop, keine Zahlung, keine Rechtsberatung. |
| SPEC-11 | Geld & Rechnungen | Business & Geld | Finance, Ledger | Rechnung mit Decimal rechnen, nicht buchen | Verkäufer, Käufer, Positionen | rechnung.schreiben, geld.zahlen | HTML-Entwurf | partial | PromptActionTests.Rechnung_rechnet_und_bucht_nicht, PromptActionTests.Geld_zahlt_nichts | Zahlung und Buchung sind nicht angeschlossen. Keine Steuerberatung. |
| CORE-08 | Videos & Fotos bearbeiten | Kreativ & Medien | MediaStudio, ffmpeg | Datei wirklich umwandeln | Quelldatei im Projekt | keiner | Mediendatei | not_implemented | — | Kein Fachadapter registriert. |
| SPEC-15 | Live streamen | Kreativ & Medien | Live-Studio der App | Sendungsplan | Titel, Ablauf | keiner | Plan | not_implemented | — | Kein Fachadapter registriert. |
| SPEC-08 | Kleidung & Produkte entwerfen | Kreativ & Medien | Größen- und Schnittlogik der App | Maße prüfen | Maße | keiner | Zuschnitt | not_implemented | — | Kein Fachadapter registriert. |
| X-07 | Freie Bilder & Klänge finden | Kreativ & Medien | OpenSource | Quelle mit Lizenz merken | Suchwort | keiner | Liste | not_implemented | — | Kein Fachadapter registriert. |
| CORE-12 | KI-Anweisungen schreiben | Kreativ & Medien | PromptLibrary | Vorlage füllen | Vorlage, Platzhalter | keiner | Text | not_implemented | — | Kein Fachadapter registriert. |
| CORE-05 | 3D-Modelle erstellen | 3D, Spiele & VR | Scene3D, Mesh3D, Studio3D | Modelldatei, Render nur mit Blender | Form und Maße | modell.bauen, modell.blender | OBJ, optional Blend | partial | PromptActionTests.Modell_schreibt_obj_und_vorschau, PromptActionTests.Modell_startet_blender_nicht | Blender wird aus dem Auftrag nicht gestartet. OBJ und die eingebaute Vorschau brauchen es nicht. |
| CORE-06 | Spiele bauen | 3D, Spiele & VR | GameStudio | Browser-Spiel, Unreal nur wenn installiert | Vorlage, Name | keiner | HTML oder uproject | not_implemented | — | Kein Fachadapter registriert. |
| SPEC-04 | VR & AR gestalten | 3D, Spiele & VR | XrModels | Raumskizze | Maße | keiner | Beschreibung | not_implemented | — | Kein Fachadapter registriert. |
| SPEC-09 | Gegenstände einscannen | 3D, Spiele & VR | PhotoScan, PointCloud | Scan aus echten Bildern | Bilder | keiner | Punktwolke | not_implemented | — | Kein Fachadapter registriert. |
| SPEC-01 | Wohnung planen | Technik & Fachgebiete | Flächenrechnung der App | Fläche aus Maßen | Länge, Breite | keiner | Zahl mit Einheit | not_implemented | — | Kein Fachadapter registriert. |
| SPEC-02 | Schaltungen berechnen | Technik & Fachgebiete | Elektronik-Rechner der App | Widerstand und LED prüfen | Spannung, Strom | keiner | Zahl mit Einheit | not_implemented | — | Kein Fachadapter registriert. |
| SPEC-03 | Statik nachrechnen | Technik & Fachgebiete | Balkenrechnung der App | Balken mit Annahmen prüfen | Last, Länge, Profil | keiner | Prüfwert | not_implemented | — | Kein Fachadapter registriert. |
| SPEC-10 | Geräte überwachen | Technik & Fachgebiete | Messwert-Ansichten der App | Messreihe zeigen | Werte | keiner | Tabelle | not_implemented | — | Kein Fachadapter registriert. |
| SPEC-13 | Routen & Karten | Technik & Fachgebiete | Routenlogik der App | Strecke aus Punkten | Punkte | keiner | Länge | not_implemented | — | Kein Fachadapter registriert. |
| SPEC-14 | Fräsen & Fertigen | Technik & Fachgebiete | Drehzahlrechnung der App | Nur rechnen, keine Maschine starten | Werkzeug, Material | keiner | Drehzahl | not_implemented | — | Keine Fräs- oder Maschinenbefehle aus einem Prompt. |
| SPEC-16 | Auto & Fahrten | Technik & Fachgebiete | Fahrtenlogik der App | Verbrauch rechnen | Strecke, Liter | keiner | Kosten | not_implemented | — | Kein Fachadapter registriert. |
| CORE-09 | Kameras & Roboter steuern | Technik & Fachgebiete | Robotik-Modelle der App | Keine Bewegung aus einem Prompt | keine | keiner | keine | not_implemented | — | Keine Kamerasteuerung und keine Roboterbefehle aus einem Prompt. |
| SPEC-05 | Messwerte auswerten | Wissen & Lernen | Science | Messreihe auswerten | Werte | keiner | Kennzahl | not_implemented | — | Kein Fachadapter registriert. |
| SPEC-06 | Lernen mit Karteikarten | Wissen & Lernen | Karteikarten der App | Karte anlegen | Vorderseite, Rückseite | keiner | Karte | not_implemented | — | Kein Fachadapter registriert. |
| SPEC-07 | Texte übersetzen | Wissen & Lernen | bestehendes Basisstudio, kein 2.0-Ausbau | keine neue Übersetzung über diesen Auftrag | keine | keiner | keine | not_implemented | — | Language & Localization Studio 2.0 bleibt abgelehnt. Kein neuer Übersetzungsadapter. |
| CORE-10 | Wissen merken | Wissen & Lernen | Knowledge | Notiz ins eigene Wissen | Text | keiner | Eintrag | not_implemented | — | Kein Fachadapter registriert. |
| SYS-08 | Sicher recherchieren | Wissen & Lernen | Research, nur Lesen | Seite nur lesen | Adresse | keiner | Lesetext | not_implemented | — | Kein Fachadapter registriert. |
| CORE-02 | KI fragen | KI & Automatik | IntelligenceCore.AskAsync | Frage an die lokale KI | Frage | keiner | Antwort | not_implemented | — | Kein Fachadapter registriert. |
| CORE-03 | KI-Team einsetzen | KI & Automatik | Agents | Rolle um Rat fragen | Aufgabe | keiner | Text | not_implemented | — | Kein Fachadapter registriert. |
| X-05 | Kostenlose KI finden | KI & Automatik | KiSources, nur Loopback | Lokale Quelle prüfen | keine | keiner | Status | not_implemented | — | Kein Fachadapter registriert. |
| SYS-05 | Aufgaben automatisieren | KI & Automatik | WorkflowEngine | Ablauf planen | Schritte | keiner | Ablauf | not_implemented | — | Kein Fachadapter registriert. |
| X-01 | Großprojekte umsetzen | KI & Automatik | bestehende Studios | Studios nur nennen | Ziel | keiner | Plan | not_implemented | — | Kein Fachadapter registriert. |
| CORE-07 | Webseite bauen | Entwickeln | DevStudio | Projektvorlage anlegen, nicht veröffentlichen | Vorlage, Name | software.anlegen, software.veroeffentlichen | Quelltext | partial | PromptActionTests.Software_legt_dateien_an, PromptActionTests.Software_veroeffentlicht_nichts | Veröffentlichen und Push sind nicht angeschlossen. Es wird nichts hochgeladen. |
| X-03 | Apps zusammenklicken | Entwickeln | PageBuilder | Seitenentwurf | Inhalt | keiner | HTML | not_implemented | — | Kein Fachadapter registriert. |
| X-06 | Projekte von GitHub holen | Entwickeln | OpenSource | Nur anzeigen, nicht mergen | Name | keiner | Hinweis | not_implemented | — | Kein Fachadapter registriert. |
| X-04 | Trendo erweitern | Entwickeln | ExtensionCatalog | Erweiterung entwerfen | Name | keiner | Entwurf | not_implemented | — | Kein Fachadapter registriert. |
| X-10 | Programme nachbauen | Entwickeln | CleanRoom | Nur eigene Beschreibung | Verhalten | keiner | Entwurf | not_implemented | — | Kein Fachadapter registriert. |
| CORE-01 | Desktop einrichten | System & Sicherheit | Shell, StartMenu, Design 41 | keine zweite Oberfläche | keine | keiner | keine | not_implemented | — | Desktop, Startmenü und Fenster bleiben beim laufenden Design-41-Auftrag. |
| SYS-01 | Startmodus wählen | System & Sicherheit | OsMode | Modus nur anzeigen | keine | keiner | Hinweis | not_implemented | — | Kein Fachadapter registriert. |
| CORE-11 | Daten schützen | System & Sicherheit | ApprovalGate, Secrets, Backup, Rbac | Freigabe bleibt beim bestehenden Tor | keine | keiner | keine | not_implemented | — | Kein zweites Freigabe- oder Geheimnissystem. |
| SPEC-12 | System auf Gefahren prüfen | System & Sicherheit | ForensicsLab, 120 Stunden | Forensik unverändert lassen | keine | keiner | keine | not_implemented | — | Verbesserung 34 bleibt abgelehnt. Die 120-Stunden-Regel wird nicht angetastet. |
| SYS-03 | Surfen & Herunterladen | System & Sicherheit | Browser-Lesemodus | Nichts im Hintergrund laden | Adresse | keiner | Hinweis | not_implemented | — | Kein Fachadapter registriert. |
| SYS-04 | Programme installieren | System & Sicherheit | DependencyManager, nur prüfen | Nichts installieren | Name | keiner | Prüfergebnis | not_implemented | — | Kein Fachadapter registriert. |
| SYS-06 | Dateien teilen | System & Sicherheit | ShareServer | Freigabe nur mit bestehendem Tor | Pfad | keiner | Hinweis | not_implemented | — | Kein Fachadapter registriert. |
| SYS-07 | Geräte erkennen | System & Sicherheit | Devices | Geräte nur nennen | keine | keiner | Liste | not_implemented | — | Kein Fachadapter registriert. |
| SYS-11 | Programme starten | System & Sicherheit | ExternalApps | Programm nur über die bestehende Prüfung | Name | keiner | Hinweis | not_implemented | — | Kein Fachadapter registriert. |
| SYS-12 | Schrift & Farben anpassen | System & Sicherheit | UserSettings, Design 41 | Darstellung nicht aus einem Prompt umbauen | keine | keiner | keine | not_implemented | — | Design 41 bleibt. Kein Prompt ändert das Theme. |
| SYS-13 | Qualität prüfen | System & Sicherheit | Quality, SelfTest | Vorhandene Prüfung starten | keine | keiner | Protokoll | not_implemented | — | Kein Fachadapter registriert. |
| X-02 | Einrichten & Reparieren | System & Sicherheit | SetupPacks, Heal | Nur anzeigen, Verbesserung 35 bleibt offen | keine | keiner | Hinweis | not_implemented | — | Verbesserung 35 ist nicht entschieden und wird nicht aktiviert. |
| X-08 | Handy verbinden | System & Sicherheit | Companion | Verbindung nicht aus einem Prompt öffnen | keine | keiner | Hinweis | not_implemented | — | Kein Fachadapter registriert. |
| CORE-04 | PC, Pi & Handy verbinden | System & Sicherheit | Fusion | Verbund nur anzeigen | keine | keiner | Hinweis | not_implemented | — | Kein Fachadapter registriert. |
| X-09 | Leistung anzeigen | System & Sicherheit | ResourceMonitor, SystemMetrics | Messwerte lesen | keine | keiner | Messwert | not_implemented | — | Kein Fachadapter registriert. |

Anzahl: 54 Katalogeinträge, 11 registrierte Aktionen.
