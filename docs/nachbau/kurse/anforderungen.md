# Kurse erstellen – Anforderungen (Clean-Room)

Freigabe: 79 neue Studios am 09.11.2026, Etappe 0.9.113 „Wissen & Lernen“ am 26.11.2026 (T-0779). Kategorie: Wissen & Lernen. Id `STUDIO-COURSE-01`, Programm `kurse-programm`.
Eigene Worte, kein fremder Code, keine fremden Texte, Symbole oder Grafiken. Keine Downloads oder Testversionen.

## Vorbilder (nur Bedienkonzept)

| Vorbild | Was es gut macht | Lizenz / Zugang | Quelle (Stand 26.11.2026) |
|---|---|---|---|
| Articulate Rise 360 | Lektionen aus Bausteinen (Text, Liste, Hinweis, Bild, Quiz), Weiter erst nach bestandenem Quiz, Ausgabe für Lernplattformen | proprietär | iSpring „Rise vs Storyline 2026“ (ispringsolutions.com/blog/articulate-rise-vs-storyline), Capterra-Bewertungen Articulate 360 |
| Moodle (Kurs, Aktivitätsabschluss) | Kapitel/Abschnitte, Abschlussbedingungen, Fortschritt je Person, SCORM-Aktivität | GPL-3.0 (nur Clean-Room) | docs.moodle.org (SCORM FAQ) |
| Teachable | Kurs → Kapitel → Lektionen, Fortschrittsbalken je Lernendem | proprietär | Produktbeschreibungen in Vergleichsartikeln (Capterra „Articulate 360 alternatives“) |
| Adapt Learning | offenes Autorenwerkzeug, Ausgabe als SCORM | GPL-3.0 (nur Clean-Room, kein Code) | npmjs.com/package/adapt_framework |
| SCORM 1.2 | Paket = ZIP mit imsmanifest.xml; Laufzeit-API LMSInitialize/LMSSetValue/LMSCommit/LMSFinish, cmi.core.lesson_status, score.raw, suspend_data | Standard (ADL) | edutechwiki.unige.ch/en/SCORM_1.2, pipwerks.com „A Simple SCORM Example“, scorm.com Golf-Beispiele |

Austauschformat: **SCORM 1.2** (von Moodle, ILIAS und fast allen Lernplattformen gelesen) und eine statische Webseite (eine HTML-Datei). H5P-Paket nicht gebaut (bräuchte die H5P-Laufzeit).
Wiederverwendet: Quiz aus „Quiz erstellen“ (`Quiz`, `QuizSpiel`, HTML-Spieler), Malkurse des Künstlerstudios (`Kreativ.Courses`) als Vorlage „Malkurs aus dem Künstlerstudio“.

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stand |
|---|---|---|---|---|---|
| KU-01 | Gliederung | Kapitel und Lektionen anlegen, umbenennen, verschieben, löschen (Rechtsklick), Rückgängig | UI `Course_editor_view_files` | Teachable, Moodle | erfüllt |
| KU-02 | Bausteine | Text, Überschrift, Liste, Hinweis, Bild (eingebettet), Quiz; verschieben/löschen; Dauer je Lektion | UI `Course_editor_view_files` | Rise | erfüllt |
| KU-03 | Quiz einbinden | Quiz-Datei aus „Quiz erstellen“ (.tdoc), GIFT oder Fragen-Text, oder das gerade offene Quiz; fehlerhaftes Quiz wird abgelehnt | UI `Course_editor_view_files` | Rise (Knowledge Check) | erfüllt |
| KU-04 | Kursansicht | wie Lernende: Person wählen, Gliederung mit ✓ und Schloss, Lektion lesen, Quiz spielen, „Lektion abschließen“ | UI `Course_editor_view_files` | Rise, Teachable | erfüllt |
| KU-05 | Fortschritt je Person | erledigte Lektionen, bestes Quiz-Ergebnis je Quiz, Prozent, Quiz-Schnitt, abgeschlossen; der Reihe nach; Quiz ab x % | Core `Course_progress`, UI | Moodle-Abschluss | erfüllt |
| KU-06 | CSV | Fortschritt aller Lernenden (Semikolon, UTF-8 mit BOM) | Core `Course_progress`, UI | Moodle-Bericht | erfüllt |
| KU-07 | Webseite | eine HTML-Datei, ohne Internet, Gliederung, Fortschritt im Browser je Name, gleicher Quiz-Spieler, Lektion erst nach bestandenem Quiz; keine lokalen Pfade, keine Lernenden im Export | Core `Scorm_package`; Edge-Beleg | Rise „Web“ | erfüllt |
| KU-08 | SCORM 1.2 | ZIP mit imsmanifest.xml (Namensräume, ein SCO, masteryscore) und index.html; Seite meldet Status, Punkte, Lesezeichen, Stand an die API | Core `Scorm_package`; 7-Zip + Edge (ohne Plattform) | SCORM 1.2 | teilweise – Lauf in einer echten Lernplattform nicht geprüft |
| KU-09 | Gliederung aus Text | „# Kapitel“, „## Lektion“, Absätze, „- “ Liste, „! “ Merksatz | Core `Course_progress`, Adapter `Kurs_Adapter` | – | erfüllt |
| KU-10 | KI-Fachaktion | „kurs.lektion“: Lektion aus Text der KI im offenen Kurs, Vorschau, ein Rückgängig-Schritt | UI `Ki_lesson_into_open_course` (ersetzt), `Real_local_ki` (Ollama) | Rise AI (Konzept) | erfüllt |
| KU-11 | Ziehen und Ablegen | Bilder, Quiz-Dateien, .md/.txt auf die Lektion | UI `Course_editor_view_files` | – | erfüllt |
| KU-12 | Vorlagen | leer, Beispiel „Gut präsentieren“, Malkurs aus dem Künstlerstudio | UI `Pages`, Core `Course_progress` | – | erfüllt |

## Nicht gebaut
- H5P-Paket, xAPI/cmi5, Video- und Ton-Bausteine, Zertifikat als PDF, Kurse verkaufen (Teachable-Shop), gemeinsamer Fortschritt mehrerer Geräte.
