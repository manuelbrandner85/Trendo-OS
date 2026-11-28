# Kurse erstellen – Testergebnisse

Studio: Kurse erstellen (STUDIO-COURSE-01) · Bereich: Wissen & Lernen · Seite: `kurse-programm` · Stand: 0.9.113 (Stand 1978f1a)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 10 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| T-0779 ECHT: lokale KI (Ollama) – Fragen aus einem Text ins offene Quiz und eine Lektion in den offenen Kurs, Rückgängig (nur mit TRENDO_T0779_ECHT) | übersprungen |
| T-0779 Einbindung: beide Studios im Katalog (Wissen & Lernen), Seite, Symbol, Fähigkeit, Profil, Team, Fachwissen, Bildbeschreibung; Vorlagen öffnen; genau eine goldene Hauptaktion | ✓ bestanden |
| T-0779 H2: „In Karteikarten übernehmen“ ist keine Aktion des Assistenten – weder Plan noch Probelauf (wie die Live-Vorschau beim Tippen) noch KI-Plan schreiben Karteikarten | ✓ bestanden |
| T-0779 KI Kurs: „Schreib eine Lektion“ → neue Lektion mit Überschrift, Liste, Merksatz im offenen Kurs, EIN Rückgängig-Schritt (KI ersetzt) | ✓ bestanden |
| T-0779 KI Quiz: „Mach Fragen aus dem Text“ → Vorschau zeigt die Fragen, Original unverändert → „Ja, behalten“ → EIN Rückgängig-Schritt „KI: …“, Rückgängig nimmt alle zurück (KI ersetzt) | ✓ bestanden |
| T-0779 Kurs: leere Vorlage → Text, Kapitel, Lektion, Bild und Quiz-Datei per Ziehen, Rechtsklick; Kursansicht mit Person, Quiz spielen, Lektion abschließen; HTML, SCORM, CSV | ✓ bestanden |
| T-0779 M2: KI setzt im Kurs weder Bildpfade („…/bloecke/0/bild“) noch Fortschritt („lernende/0/erledigt“) noch das ganze Kurs-Objekt mit Lernenden – abgelehnt, Kurs unverändert | ✓ bestanden |
| T-0779 M4/Niedrig: andere Aktionen zeigen den vollen Wert; zu viele KI-Fragen werden gemeldet; zu große Dateien abgelehnt; keine fremden Markennamen im Fachwissen | ✓ bestanden |
| T-0779 Quiz: Spielmodus per Klick und Tasten – Antwort wählen (1–4), prüfen (Eingabe), Rückmeldung, Lücke tippen, Zuordnen wählen, Auswertung mit Prozent; Zeitlimit läuft ab | ✓ bestanden |
| T-0779 Quiz: fremde GIFT-Datei laden (Knopf) und Text für die KI per Ziehen und Ablegen; CSV-Karteikarten werden Auswahlfragen; Quiz → Karteikarten | ✓ bestanden |
| T-0779 Quiz: leere Vorlage → erster Schritt, Frage tippen, Lücke, Zuordnen, richtige Antwort klicken, Rechtsklick duplizieren/löschen + Rückgängig → HTML, PDF (PDFium-Text), GIFT als Datei | ✓ bestanden |
