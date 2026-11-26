# Quiz erstellen – Anforderungen (Clean-Room)

Freigabe: 79 neue Studios am 09.11.2026, Etappe 0.9.113 „Wissen & Lernen“ am 26.11.2026 (T-0779). Kategorie: Wissen & Lernen. Id `STUDIO-QUIZ-01`, Programm `quiz-programm`.
Eigene Worte, kein fremder Code, keine fremden Texte, Symbole oder Grafiken. Keine Downloads oder Testversionen – nur öffentliche Doku und Fachberichte.

## Vorbilder (nur Bedienkonzept)

| Vorbild | Was es gut macht | Lizenz / Zugang | Quelle (Stand 26.11.2026) |
|---|---|---|---|
| Kahoot! | Spielmodus: große Antwortkacheln, Zeit je Frage, schnell = mehr Punkte, Auswertung am Ende | proprietär (Web/App) | G2-Vergleich Quizlet vs Kahoot (g2.com/compare/quizlet-vs-kahoot), Jotform „Kahoot alternatives 2026“ |
| Quizlet | Lernkarten → Test mit Auswahl-, Wahr/Falsch- und Zuordnungsfragen | proprietär | Jotform „Quizlet vs Kahoot“ (eu.jotform.com/blog/quizlet-vs-kahoot), WifiTalents „quiz maker software“ |
| Moodle-Test (Fragensammlung) | Fragetypen Auswahl, Mehrfach mit Prozent, Wahr/Falsch, Kurzantwort, Zuordnen, fehlendes Wort; Austausch über das Klartextformat GIFT | GPL-3.0 (nur Clean-Room, kein Code) | docs.moodle.org/401/en/GIFT_format, docs.moodle.org/400/en/GIFT |
| H5P (Quiz/Question Set) | eigenständige Lerninhalte, die in jeder Seite laufen | Kern-Bibliothek GPL-3.0 (h5p-php-library), Inhaltstypen teils MIT – nur Clean-Room | github.com/h5p/h5p-php-library, moodledev.io Third-party-Credits |
| ActivePresenter / Articulate Quizmaker | Quiz für Lernplattformen (SCORM), Druckfassung | proprietär | atomisystems.com „Best Quiz Software 2026“ (Stand 03.04.2026) |

Offene Alternativen: Moodle (GPL-3.0), H5P (GPL-3.0/MIT gemischt) – beides nur als Verhaltensvorbild, kein Code übernommen.
Austauschformat: **GIFT** gewählt (Klartext, von Moodle direkt importierbar, in der Moodle-Doku vollständig beschrieben). QTI wurde nicht gebaut – deutlich aufwändiger (XML, viele Profile) und für Moodle nur über Zusatzmodule.
Wiederverwendet: Lern-Kern SPEC-06 (`LearnBox`): Karteikarten → Auswahlfragen (falsche Antworten aus demselben Stapel wie „Quiz mit Auswahl“), Quiz → Karteikarten, Tippfehler-Toleranz `LearnBox.CheckTyped` für Lücken.

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stand |
|---|---|---|---|---|---|
| QZ-01 | Fragetypen | Auswahl, Mehrfachauswahl, Wahr/Falsch, Lückentext ([Lösung], Alternativen mit „\|“), Zuordnen; Art nachträglich ändern | Core `Scoring`, UI `Quiz_editor_to_files` | Moodle, Quizlet | erfüllt |
| QZ-02 | Editor statt Formular | Fragen als Karten auf der Arbeitsfläche: Text, Antworten, Kreis/Kästchen für „richtig“, Paare, Erklärung; Rechtsklick (duplizieren, verschieben, Art, löschen); Rückgängig | UI `Quiz_editor_to_files` | Kahoot-Editor | erfüllt |
| QZ-03 | Punkte | Punkte je Frage; Mehrfach: richtige Treffer minus falsche Kreuze, nie unter 0; Lücken und Paare anteilig | Core `Scoring` | Moodle-Bewertung | erfüllt |
| QZ-04 | Zeit | Zeit je Quiz und je Frage; abgelaufen = 0 Punkte; Tempo-Bonus bis +50 % | Core `Scoring`, `Game`; UI `Quiz_play` (Tick-Handler direkt, Timer headless nicht) ; Edge-Beleg (Zeitbalken läuft im Browser) | Kahoot | erfüllt (Timer im Fenster nur indirekt) |
| QZ-05 | Mischen | Fragen und Antworten mischen (Startwert reproduzierbar), Wahr/Falsch bleibt geordnet | Core `Game` | Kahoot, Moodle | erfüllt |
| QZ-06 | Spielmodus mit Auswertung | große Antwortknöpfe, Tasten 1–9, Eingabe prüft, Rückmeldung mit Lösung und Erklärung, Auswertung mit Prozent, Punkten, Zeit je Frage, bestanden ab x % | UI `Quiz_play` | Kahoot | erfüllt |
| QZ-07 | Prüfung | fehlende richtige Antwort, Lückentext ohne Lücke, doppelte Zuordnung, Zeit außerhalb 0–3600, leerer Text; Ausgabe mit Fehler verweigert | Core `Checks`, UI `Quiz_editor_to_files` | eigene Regel | erfüllt |
| QZ-08 | HTML zum Weitergeben | eine Datei, ohne Internet und fremde Skripte, gleicher Spielablauf, Daten sicher eingebettet | Core `Html_and_pdf`; Edge-Beleg `fremdprogramme.log` | H5P | erfüllt |
| QZ-09 | PDF zum Ausdrucken | A4, Kästchen, Lücken, Zuordnungsliste A–…, Lösungsblatt, Seitenzahlen | UI `Quiz_editor_to_files` (PDFium-Text), Beleg pdftotext + qpdf | Druckfassung | erfüllt |
| QZ-10 | GIFT für Moodle | Export aller Typen mit Escapes; mehrere Lücken → je Lücke eine Frage; Import fremder GIFT-Dateien (Prozent, {T}, Kurzantwort, Zuordnen), Zahlenfragen gemeldet | Core `Gift_roundtrip`, UI `Quiz_import_and_drop` | Moodle-Doku | teilweise – Import in einem echten Moodle nicht geprüft (kein Moodle auf dem PC) |
| QZ-11 | Karteikarten | Stapel → Auswahlfragen, CSV „Frage;Antwort“ ablegen, Quiz → Karteikarten | UI `Quiz_import_and_drop` | Quizlet | erfüllt |
| QZ-12 | KI-Fachaktion | „quiz.fragen“: Fragen aus Text (Feld „Text für die KI“ oder Auftrag), Vorschau, „Ja, behalten“ = ein Rückgängig-Schritt | UI `Ki_questions_into_open_quiz` (ersetzt), `Real_local_ki` (Ollama) | Quizlet „Magic Notes“ (Konzept) | erfüllt |
| QZ-13 | Ziehen und Ablegen | GIFT/Fragen-Text/CSV werden Fragen, .txt/.md ohne Fragen wird Text für die KI | UI `Quiz_import_and_drop` | – | erfüllt |
| QZ-14 | Bedienstufen, Kürzel | Einfach zeigt 4 Befehle + 2 Ausgaben; Strg+Umschalt+N, Strg+D, F5 in der Menüleiste | UI `Quiz_editor_to_files` (Stufe Einfach), Bild | Windows-Programme | teilweise – Kürzel nicht einzeln getestet |

## Nicht gebaut
- Live-Mehrspieler (Spielende mit eigenen Handys wie bei Kahoot) – bräuchte einen Server; QTI-Export; Bilder und Ton in Fragen; Freitextfragen mit Bewertung.
