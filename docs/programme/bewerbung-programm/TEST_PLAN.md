# Bewerbung & Lebenslauf – Testplan

Studio: Bewerbung & Lebenslauf (STUDIO-CV-01) · Bereich: Arbeiten & Büro · Seite: `bewerbung-programm` · Stand: 0.9.114 (Stand 99cbb5c)

8 Tests sprechen dieses Programm an.

- Arbeiten & Büro A: alle drei Studios haben eine eigene Seite und öffnen ihre Vorlagen
- Bewerbung: Anschreiben – Entwurf ohne KI, Stichwörter der Anzeige, mit KI (Ersatz im Test), an „Briefe schreiben“; Mappe mit Anlage; Übersicht mit Frist
- Bewerbung: Prüfen, Feld tippen → Prüfung meldet falsches Datum, Station löschen und Rückgängig, Layout wählen, Exporte PDF/DOCX/ATS
- STUDIO-CV-01 Adapter: Lebenslauf aus Stichpunkten prüfen, ATS-Fassung und Abgleich – ohne Nebenwirkung, Pflichtfeld Name
- T-0705 Absturz in allen sieben Büro-Studios: Änderung per Klick, Sicherungstakt (≤ 20 s), Fenster stirbt ohne Speichern – das neue Fenster bietet „Wiederherstellen“ an und bringt jede Änderung zurück
- T-0705 Echtlauf Bewerbung: Zeugnis-PDF von LibreOffice als Anlage ablegen, Lebenslauf als Word und PDF, Mappe als PDF – LibreOffice öffnet das DOCX, qpdf prüft PDF und Mappe (Anlage hinten)
- T-0705 Formulare, Verträge, Bewerbung, Karten: Datei auf die Arbeitsfläche ziehen öffnet bzw. hängt an; Rechtsklick am Vertrag schreibt die Kündigung als PDF
- T-0705 Klick bis Datei – Bewerbung: leerer Lebenslauf, „Namen eintragen“ holt das Namensfeld, Name getippt, Export PDF und Word enthalten ihn
