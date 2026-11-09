# Funktionsliste: Briefe schreiben

Studio STUDIO-LETTER-01 · Briefe schreiben · Bereich Arbeiten & Büro

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

| Nr | Funktion | Stand laut Katalog | Test |
|---|---|---|---|
| BR-01 | Brief nach DIN 5008 (Form A/B), Informationsblock, Folgeseiten „Seite n von N“ | umgesetzt | LetterDinTests |
| BR-02 | Kontakt einsetzen, Form wechseln, Vorschau | umgesetzt | LetterDin_Ui |
| BR-03 | Assistent in drei Schritten, Alles leeren mit Rückgängig | umgesetzt | LetterWizard_Ui |
| BR-04 | Vorlagen-Galerie mit Gruppen (Kündigung, Widerspruch, Widerruf …) | umgesetzt | LetterProTests |
| BR-05 | 20 Designs mit Vorschaubild, Farben, Kopf-Layouts, Schriften | umgesetzt | LetterSendUiTests |
| BR-06 | PDF ohne Zusatzprogramm, Word-Datei | umgesetzt | LetterDesignPdfTests |
| BR-07 | Exportieren mit Ort und Format | umgesetzt | LetterSendUiTests |
| BR-08 | Senden per E-Mail mit PDF-Anhang (nur Entwurf), Drucken | umgesetzt | LetterSendUiTests |
| BR-09 | Umschlag DIN lang zum Brief | umgesetzt | LetterSendUiTests |
| BR-10 | Fristen berechnen, in den Kalender | umgesetzt | LetterProTests |
| BR-11 | Serienbrief aus CSV und aus Adressbuch-Gruppe | umgesetzt | LetterProUiTests |
| BR-12 | Unterschrift zeichnen oder als Bild | umgesetzt | LetterF3Tests |
| BR-13 | Falz- und Lochmarken | umgesetzt | LetterF3Tests |
| BR-14 | Rechtschreibung beim Tippen, eigenes Wörterbuch | umgesetzt | LetterF3UiTests |
| BR-15 | Faktenwächter, Rückfragen je Kategorie | umgesetzt | LetterQaTests |
| BR-16 | Papierkorb für Briefe, Akten, Briefköpfe | umgesetzt | BriefePapierkorb |
| BR-17 | Kein Programmname auf dem Blatt | umgesetzt | LetterNoBrandingUiTests |
| BR-18 | Papierschacht wählen | teilweise (Druckdialog des Systems) | – |
| BR-19 | Briefart erkennen (Widerspruch, Widerruf, Kündigung, Mahnung, Reklamation, DSGVO-Auskunft, Antrag, Bewerbung) mit Frist und Grundlage (Claude-Ausbau 5) | umgesetzt | LetterOfficeTests, LetterOffice_Ui |
| BR-20 | Pflichtangaben nach DIN 5008 und Briefart prüfen, typische Anlagen eintragen | umgesetzt | LetterOfficeTests, LetterOffice_Ui |
| BR-21 | Frist erst nach „Ja“ in den Trendo-Kalender (Erinnerung zwei Tage vorher) | umgesetzt | LetterOffice_Ui |
| BR-22 | Wiedervorlage je Akte (Datum, Notiz, „+ 2 Wochen“, erledigt) | umgesetzt | LetterOfficeTests, LetterOffice_Ui |
| BR-23 | „Steht an“: Fristen und Wiedervorlagen aller Akten, überfällige zuerst | umgesetzt | LetterOfficeTests, LetterOffice_Ui |
| BR-24 | Postausgangsbuch (Datum, Empfänger, Betreff, Versandweg, Akte, Nachweis) mit CSV; Versand mit Nachweis setzt die Akte auf „versendet“ | umgesetzt | LetterOfficeTests, LetterOffice_Ui |

## Fehlende Funktionen laut Vorbild-Analyse

- keine als fehlend markiert oder keine Analyse vorhanden

## Teilweise vorhanden

- ◐ Papierschacht und Drucker: im Druckdialog des Systems, nicht in Trendo
