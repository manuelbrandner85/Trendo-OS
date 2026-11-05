# Briefe schreiben – Umsetzungsstand

Studio: Briefe schreiben (STUDIO-LETTER-01) · Bereich: Arbeiten & Büro · Seite: `brief` · Stand: 0.9.36 (Stand 6bd5d2b)

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

0 von 18 Funktionen getestet (noch kein Testlauf eingelesen).
