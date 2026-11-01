# T-0310 – Brief nach DIN 5008

- Freigabe: Manuel im Chat am 01.11.2026 (Auswahl „Brief nach DIN 5008“).
- Vorher: DIN-A4-Vorschau Form B mit Sichtfenster und Marken, aber nur eine Seite („… weiter auf Seite 2“), Empfänger nur von Hand, Serienbrief nur aus einer Tabelle.
- Jetzt (`MainWindow.LetterPro.cs`, Kern `Letters`):
  - Echte Seiten: Text bricht wie gedruckt um (gleiche Schrift und Breite), Folgeseiten ab 27 mm mit Kopfzeile, „Seite n von N“; Gruß, Unterschrift und Anlagen bleiben zusammen am Ende.
  - Form A (Anschrift ab 27 mm, Falzmarken 87/192 mm) oder Form B (45 mm, 105/210 mm) – in Vorschau und DOCX.
  - Informationsblock: Ihr Zeichen, Ihre Nachricht vom, Unser Zeichen, Ort und Datum – in Vorschau und DOCX.
  - Empfänger aus dem Adressbuch mit passender Anrede („Guten Tag …“, bei Firmen „Sehr geehrte Damen und Herren“).
  - Serienbrief an eine Adressbuch-Gruppe: Vorschau Brief für Brief (◀ ▶), Felder {Vorname}, {Nachname}, {Firma}, {Ort} …, Kontakte ohne Anschrift werden genannt und übersprungen; dann alle als DOCX.
- Vorschaubild: Render-Schalter `TRENDO_BRIEF=1`.
- Tests: `LetterDinTests` (Informationsblock, Anschrift/Anrede, Serienbrief aus Kontakten, Form A im DOCX), `LetterDin_Ui` (Kontakt einsetzen, Form wechseln, Informationsblock, Folgeseiten, Serienbrief-Vorschau und Speichern); alle bisherigen Brief-Oberflächentests (49) und Kern-Brieftests grün.
