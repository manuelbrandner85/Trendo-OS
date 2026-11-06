# STUDIO-LETTER-01 – Briefe schreiben: Funktionskatalog

Funktionen nach den Vorbildern (Microsoft Word mit Serienbrief und Umschlägen, LibreOffice Writer, Canva, Briefvorlagen nach DIN 5008 – nur Funktionen, Clean-Room), Stand 05.11.2026.
„Stand“: umgesetzt = im Code mit Test; „Test“ nennt die Testklasse.

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| BR-01 | Brief nach DIN 5008 (Form A/B), Informationsblock, Folgeseiten „Seite n von N“ | DIN 5008, Word | umgesetzt | LetterDinTests |
| BR-02 | Kontakt einsetzen, Form wechseln, Vorschau | Word | umgesetzt | LetterDin_Ui |
| BR-03 | Assistent in drei Schritten, Alles leeren mit Rückgängig | Word Briefassistent | umgesetzt | LetterWizard_Ui |
| BR-04 | Vorlagen-Galerie mit Gruppen (Kündigung, Widerspruch, Widerruf …) | Word Vorlagen | umgesetzt | LetterProTests |
| BR-05 | 20 Designs mit Vorschaubild, Farben, Kopf-Layouts, Schriften | Canva | umgesetzt | LetterSendUiTests |
| BR-06 | PDF ohne Zusatzprogramm, Word-Datei | Word | umgesetzt | LetterDesignPdfTests |
| BR-07 | Exportieren mit Ort und Format | Word | umgesetzt | LetterSendUiTests |
| BR-08 | Senden per E-Mail mit PDF-Anhang (nur Entwurf), Drucken | Outlook/Word | umgesetzt | LetterSendUiTests |
| BR-09 | Umschlag DIN lang zum Brief | Word „Umschläge“ | umgesetzt | LetterSendUiTests |
| BR-10 | Fristen berechnen, in den Kalender | – | umgesetzt | LetterProTests |
| BR-11 | Serienbrief aus CSV und aus Adressbuch-Gruppe | Word Serienbrief | umgesetzt | LetterProUiTests |
| BR-12 | Unterschrift zeichnen oder als Bild | Adobe Fill & Sign | umgesetzt | LetterF3Tests |
| BR-13 | Falz- und Lochmarken | DIN 5008 | umgesetzt | LetterF3Tests |
| BR-14 | Rechtschreibung beim Tippen, eigenes Wörterbuch | Word | umgesetzt | LetterF3UiTests |
| BR-15 | Faktenwächter, Rückfragen je Kategorie | – | umgesetzt | LetterQaTests |
| BR-16 | Papierkorb für Briefe, Akten, Briefköpfe | – | umgesetzt | BriefePapierkorb |
| BR-17 | Kein Programmname auf dem Blatt | – | umgesetzt | LetterNoBrandingUiTests |
| BR-18 | Papierschacht wählen | Word Druckdialog | teilweise (Druckdialog des Systems) | – |
| BR-19 | Briefart erkennen (Widerspruch, Widerruf, Kündigung, Mahnung, Reklamation, DSGVO-Auskunft, Antrag, Bewerbung) mit Frist und Grundlage (Claude-Ausbau 5) | Kanzleisoftware | umgesetzt | LetterOfficeTests, LetterOffice_Ui |
| BR-20 | Pflichtangaben nach DIN 5008 und Briefart prüfen, typische Anlagen eintragen | DIN 5008 | umgesetzt | LetterOfficeTests, LetterOffice_Ui |
| BR-21 | Frist erst nach „Ja“ in den Trendo-Kalender (Erinnerung zwei Tage vorher) | – | umgesetzt | LetterOffice_Ui |
| BR-22 | Wiedervorlage je Akte (Datum, Notiz, „+ 2 Wochen“, erledigt) | Kanzlei-Wiedervorlage | umgesetzt | LetterOfficeTests, LetterOffice_Ui |
| BR-23 | „Steht an“: Fristen und Wiedervorlagen aller Akten, überfällige zuerst | Kanzlei-Fristenbuch | umgesetzt | LetterOfficeTests, LetterOffice_Ui |
| BR-24 | Postausgangsbuch (Datum, Empfänger, Betreff, Versandweg, Akte, Nachweis) mit CSV; Versand mit Nachweis setzt die Akte auf „versendet“ | Kanzlei-Postausgangsbuch | umgesetzt | LetterOfficeTests, LetterOffice_Ui |
