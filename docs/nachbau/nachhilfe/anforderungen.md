# Nachhilfe mit KI – Anforderungen (Clean-Room)

Studio `STUDIO-TUTOR-01`, Seite `nachhilfe-programm`, Kategorie Wissen & Lernen. Freigabe des Nutzers am 26.11.2026 (Etappe 0.9.113, Paket T-0782).

## Vorbilder (nur Verhalten und Bedienung, kein Code, keine Texte, keine Symbole)

| Vorbild | Quelle | Datum | belegte Arbeitsweise |
|---|---|---|---|
| Khanmigo (Khan Academy) | „How We Built AI Tutoring Tools“, blog.khanacademy.org/how-we-built-ai-tutoring-tools | 07.03.2024 | Rückfragen statt Lösung; wer hängt, bekommt den nächsten Schritt; Rückmeldung je Schritt |
| Khanmigo (Fremdtest) | Common Sense Media, KI-Risikobewertung Khanmigo, institute.commonsensemedia.org/risk-assessments/khanmigo | aktualisiert 08.08.2024 | Gespräche auf Lernaktivitäten begrenzt; Fakten können falsch sein |
| Khanmigo (Studie) | KQED MindShift „Students didn't get answers from Khanmigo …“, kqed.org/mindshift/66662 | Abruf 26.11.2026 | Nur Rückfragen ohne spürbare Hilfe → Lernende springen ab: Hilfe muss in Stufen nützen |
| Khan Academy Mastery | Hilfeartikel „How do Khan Academy's Mastery levels work?“ (support.khanacademy.org, Artikel 5548760867853) | nur Suchvorschau, Abruf 26.11.2026 | Stufen je Fähigkeit (versucht, vertraut, sicher, gemeistert), steigen und fallen; Kursfortschritt zählt nur sichere Stufen |
| Duolingo | „How we learn how you learn“, blog.duolingo.com/how-we-learn-how-you-learn | 14.12.2016 | Gedächtnis-Halbwertszeit je Lernstoff; das am ehesten Vergessene wird zuerst wiederholt |
| Duolingo | „Guide to Duolingo Practice Hub“, blog.duolingo.com/guide-to-duolingo-practice-hub | 18.02.2026 | eigener Übungsbereich mit Fehler-Wiederholung |
| ANTON | App-Store-Eintrag „ANTON – Lernen – Schule“ (apps.apple.com, id1180554775) | Abruf 26.11.2026 | Lernpfad nach Klassenstufe und Lehrplan, werbefrei; Kritik: wenig Erklärung bei Fehlern |
| simpleclub | App-Store-Eintrag „simpleclub – Die Lernapp“ (apps.apple.com, id1089299986) | Abruf 26.11.2026 | persönlicher Lernplan, KI-Tutor, Anzeige des Verständnis-Stands, Karteikarten |

Recherche am 26.11.2026 (Websuche, öffentliche Seiten). Khan- und Codecademy-Hilfeseiten verweigerten den direkten Abruf (403), dort nur Suchvorschau.

### Offene Alternativen (nur zur Einordnung, nichts übernommen)

| Projekt | Lizenz | Quelle | Umgang |
|---|---|---|---|
| Oppia | Apache-2.0 | github.com/oppia/oppia (LICENSE) | nicht benutzt |
| Kolibri (Learning Equality) | MIT | github.com/learningequality/kolibri (LICENSE) | nicht benutzt |
| Moodle | GPL-3.0 | github.com/moodle/moodle (COPYING.txt) | nur Clean-Room |
| H5P (PHP-Kern) | GPL-3.0 | github.com/h5p/h5p-php-library | nur Clean-Room |
| Anki | AGPL-3.0 | github.com/ankitects/anki | nur Clean-Room; FSRS selbst stammt aus Open Spaced Repetition (MIT) und liegt schon in Trendo (SPEC-06) |
| GCompris | AGPL (Projektseite) | gcompris.net | nur Clean-Room |

## Vorhandenes genutzt
- Wiederholungsplaner FSRS-5 aus SPEC-06 (`src/Trendo.Core/W6/Fsrs.cs`), Karteikarten (`LearnBox`) als Ziel „In Karteikarten übernehmen“.
- Programm-Hülle (`ProgramSpec`, `ProgramDocument`, AtomicFile-Speichern, Notsicherung alle 20 s, Rückgängig), KI-Kette mit `ProgramSpec.KiActions` (T-0774).
- KI nur auf diesem PC: derselbe `IntelligenceCore`, auf lokale Quellen beschränkt (`AgentKi("lokal")`), neu `ProgramSpec.KiLocalOnly` für die Auftragszeile.
- ODT/DOCX-Schreiber aus „Texte & Tabellen“ (`OdtFormat`, `DocxFormat`), Kinderschutz `ChildWrite` über die Export-Wege der Hülle.

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Ergebnis |
|---|---|---|---|---|---|
| NH-01 | Lernpfad je Fach und Klasse | Mathe, Deutsch, Englisch; Themen passend zur Klasse (±1) | Core `T0782_Nachhilfe.Path_per_subject_and_grade` | ANTON | erfüllt |
| NH-02 | Übungen in beliebiger Menge | Mathe-Themen erzeugen Aufgaben (Einmaleins, schriftlich addieren, Teilen mit Rest, Brüche kürzen, Prozent, Gleichungen, Pythagoras); Deutsch/Englisch feste eigene Aufgaben | Core `Mastery_and_schedule` (Generator), `Path_per_subject_and_grade` | Khan, ANTON | erfüllt |
| NH-03 | Prüfung der Antwort | Zahl mit Komma/Punkt, Bruch (nicht gekürzt = Hinweis, zählt nicht), „3 R 2“, Text ohne Groß/klein und mit ae/ä | Core `Checking` | Khan (Rückmeldung) | erfüllt |
| NH-04 | Sokratischer Modus | Lösungsweg erst nach einem eigenen Versuch; Hinweise in Stufen jederzeit; im Kinderprofil nicht abschaltbar | Core `Socratic_mode`, UI `Tutor_click_to_file`, `Tutor_drop_context_child` | Khanmigo, KQED-Studie | erfüllt |
| NH-05 | Beherrschung je Thema | neu → versucht → vertraut → sicher → gemeistert (zwei gute Runden an verschiedenen Tagen), fällt bei schwacher Runde | Core `Mastery_and_schedule` | Khan Mastery | erfüllt |
| NH-06 | Wiederholungsplan | nach 5 Übungen FSRS-Schritt; Abstand wächst bei Erfolg, kurz nach Fehlern; fällige Themen zuerst, „Heute wiederholen“ | Core `Mastery_and_schedule`; Menüband „Heute wiederholen“ | Duolingo | erfüllt |
| NH-07 | Fortschritt | Anteil sicherer Themen im Lernpfad in %, Balken und Statuszeile | UI (Bild), Core `Fortschritt` über `Mastery_and_schedule` | Khan | erfüllt |
| NH-08 | KI erklärt (nur lokal) | Frage an die KI auf diesem PC; Antwort ohne Links; verrät sie die Lösung einer offenen Übung → zurückgehalten; die Frage an die KI enthält die Lösung nicht | Core `Ki_filter`, UI `Tutor_ask_filtered` (Claude-Ersatz nie gefragt) | Khanmigo | erfüllt |
| NH-09 | KI-Fachaktion | Auftragszeile → Plan `uebung.erzeugen` → Vorschau ändert nichts → „Ja, behalten“ → Übung im Dokument, genau ein Rückgängig-Schritt; nur lokale KI | UI `Tutor_ki_exercise_local_only`, Echtlauf `Real_local_ki` (`reports/T-0782/`) | Trendo-Kette | erfüllt |
| NH-10 | KI darf Fortschritt nicht ändern | `dokument.setzen` auf Fortschritt, Verlauf, sokratisch, Übungen gesperrt; Übung ohne Antwort abgelehnt | UI `Tutor_ki_rejects` | Kinderschutz | erfüllt |
| NH-11 | Kinderprofil-tauglich | keine Links (aus KI-Texten, eigenen Übungen und CSV entfernt bzw. abgelehnt), keine Sendewege, nur lokale KI, Export über die Hülle mit `ChildWrite` | Core `Ki_filter`, `Own_items_csv_sheet`; UI `Tutor_drop_context_child` | T-0764 | erfüllt |
| NH-12 | Eigene Übungen | Frage, Antworten (mit „\|“), Lösungsweg, Hinweis; verratende Hinweise fallen weg | Core `Own_items_csv_sheet` | simpleclub (eigene Karteikarten) | erfüllt |
| NH-13 | Fremdformat ein/aus | CSV (Semikolon, UTF-8 mit BOM) aus/nach LibreOffice Calc; Arbeitsblatt als ODT und DOCX | Core `Own_items_csv_sheet`, UI `Tutor_click_to_file`, Fremdprogramm-Beleg `reports/T-0782/fremdprogramm-libreoffice.log` | – | erfüllt |
| NH-14 | Bedienung | Eingabetaste prüft, Kürzel (Strg+J Hinweis, Strg+L Lösungsweg, Strg+Umschalt+N/P), Rechtsklick am Thema, CSV ablegen, Einfach/Profi | UI `Tutor_click_to_file`, `Tutor_drop_context_child` | – | erfüllt |
| NH-15 | In Karteikarten übernehmen | eigene Übungen des Themas als Karten in SPEC-06 | – (nur von Hand geprüft im Code) | simpleclub | teilweise (kein eigener Test) |
| NH-16 | Elternbereich, Meldungen an Eltern | – | – | Khanmigo | fehlt (nicht freigegeben) |
| NH-17 | Prüfungssimulation mit Notenprognose | – | – | simpleclub | fehlt |

## Bewusst nicht gebaut
- Kein Chat mit anderen Personen, kein Versand, keine Konten, keine Internet-KI – auch nicht für Erwachsene in diesem Studio.
- Keine Inhalte der Vorbilder; alle Aufgaben, Hinweise und Erklärungen sind eigene.
