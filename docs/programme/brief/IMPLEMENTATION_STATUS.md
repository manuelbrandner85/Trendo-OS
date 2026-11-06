# Briefe schreiben – Umsetzungsstand

Studio: Briefe schreiben (STUDIO-LETTER-01) · Bereich: Arbeiten & Büro · Seite: `brief` · Stand: 0.9.36 (Stand df8e056)

Stufen: Geplant · In Arbeit · Implementiert · Getestet · Blockiert. „Getestet“ nur, wenn der zugehörige Test im letzten echten Lauf bestanden hat.

| Nr | Funktion | Stand | Test |
|---|---|---|---|
| BR-01 | Brief nach DIN 5008 (Form A/B), Informationsblock, Folgeseiten „Seite n von N“ | Implementiert | `LetterDinTests` |
| BR-02 | Kontakt einsetzen, Form wechseln, Vorschau | Implementiert | `LetterDin_Ui` |
| BR-03 | Assistent in drei Schritten, Alles leeren mit Rückgängig | Implementiert | `LetterWizard_Ui` |
| BR-04 | Vorlagen-Galerie mit Gruppen (Kündigung, Widerspruch, Widerruf …) | Implementiert | `LetterProTests` |
| BR-05 | 20 Designs mit Vorschaubild, Farben, Kopf-Layouts, Schriften | Implementiert | `LetterSendUiTests` |
| BR-06 | PDF ohne Zusatzprogramm, Word-Datei | Implementiert | `LetterDesignPdfTests` |
| BR-07 | Exportieren mit Ort und Format | Implementiert | `LetterSendUiTests` |
| BR-08 | Senden per E-Mail mit PDF-Anhang (nur Entwurf), Drucken | Implementiert | `LetterSendUiTests` |
| BR-09 | Umschlag DIN lang zum Brief | Implementiert | `LetterSendUiTests` |
| BR-10 | Fristen berechnen, in den Kalender | Implementiert | `LetterProTests` |
| BR-11 | Serienbrief aus CSV und aus Adressbuch-Gruppe | Implementiert | `LetterProUiTests` |
| BR-12 | Unterschrift zeichnen oder als Bild | Implementiert | `LetterF3Tests` |
| BR-13 | Falz- und Lochmarken | Implementiert | `LetterF3Tests` |
| BR-14 | Rechtschreibung beim Tippen, eigenes Wörterbuch | Implementiert | `LetterF3UiTests` |
| BR-15 | Faktenwächter, Rückfragen je Kategorie | Implementiert | `LetterQaTests` |
| BR-16 | Papierkorb für Briefe, Akten, Briefköpfe | Implementiert | `BriefePapierkorb` |
| BR-17 | Kein Programmname auf dem Blatt | Implementiert | `LetterNoBrandingUiTests` |
| BR-18 | Papierschacht wählen | In Arbeit | `–` |
| BR-19 | Briefart erkennen (Widerspruch, Widerruf, Kündigung, Mahnung, Reklamation, DSGVO-Auskunft, Antrag, Bewerbung) mit Frist und Grundlage (Claude-Ausbau 5) | Implementiert | `LetterOfficeTests, LetterOffice_Ui` |
| BR-20 | Pflichtangaben nach DIN 5008 und Briefart prüfen, typische Anlagen eintragen | Implementiert | `LetterOfficeTests, LetterOffice_Ui` |
| BR-21 | Frist erst nach „Ja“ in den Trendo-Kalender (Erinnerung zwei Tage vorher) | Implementiert | `LetterOffice_Ui` |
| BR-22 | Wiedervorlage je Akte (Datum, Notiz, „+ 2 Wochen“, erledigt) | Implementiert | `LetterOfficeTests, LetterOffice_Ui` |
| BR-23 | „Steht an“: Fristen und Wiedervorlagen aller Akten, überfällige zuerst | Implementiert | `LetterOfficeTests, LetterOffice_Ui` |
| BR-24 | Postausgangsbuch (Datum, Empfänger, Betreff, Versandweg, Akte, Nachweis) mit CSV; Versand mit Nachweis setzt die Akte auf „versendet“ | Implementiert | `LetterOfficeTests, LetterOffice_Ui` |

0 von 24 Funktionen getestet.
