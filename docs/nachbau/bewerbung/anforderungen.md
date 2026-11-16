# Bewerbung & Lebenslauf – Anforderungen (Clean-Room)

Freigabe: 79 neue Studios am 09.11.2026, „Ja, weiter“ am 15.11.2026 (T-0671). Kategorie: Arbeiten & Büro. Id `STUDIO-CV-01`, Programm `bewerbung-programm`.

## Vorbilder (nur Verhalten und Bedienung, kein Code, keine Vorlagen-Grafiken)

| Vorbild | Warum | Lizenz / Zugang | Quelle (Stand 15.11.2026) |
|---|---|---|---|
| Reactive Resume | Editor links, Live-Vorschau rechts, Layouts, Export PDF/DOCX/JSON | MIT – trotzdem nur Bedienkonzept übernommen, kein Code | github.com/AmruthPillai/Reactive-Resume, rxresu.me, docs.rxresu.me |
| Europass-Lebenslauf | Sprachen nach GER (A1–C2), feste Abschnitte | öffentliche EU-Seite | europass.europa.eu |
| Canva-Lebenslauf | Farbakzente, Seitenleiste mit Kontakt/Kenntnissen | proprietär, nur Produktseite | canva.com |

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stufe |
|---|---|---|---|---|---|
| CV-01 | Lebenslauf als Daten | Person, Stationen (Art, Titel, Firma, Ort, von/bis, Aufgaben), Ausbildung, Kenntnisse, Sprachen mit GER, Interessen, Layout, Akzent – als JSON speicherbar | Core `Cv_tracker` (JSON hin/zurück) | Reactive Resume | 1 |
| CV-02 | Datumsformat | MM.JJJJ, M/JJJJ, JJJJ-MM, JJJJ; leer/„heute“ = läuft; Unsinn wird erkannt | Core `Cv_dates` | Europass | 1 |
| CV-03 | Prüfung | fehlende/ungültige E-Mail und Telefonnummer, Wohnort, falsches Datum, Ende vor Beginn, Lücken ab 3 Monaten (auch bis heute), Länge > 2 Seiten, GER-Stufe | Core `Cv_check`, UI `Cv_editor` (Tippen → Prüfung) | Personaler-Praxis | 1 |
| CV-04 | Layouts | klassisch (Datumsspalte), modern (farbige Seitenleiste), kompakt; Live-Vorschau neben dem Editor | Core `Cv_pdf_docx`, UI `Cv_editor` (Layout/Akzent) | Reactive Resume, Canva | 1 |
| CV-05 | Export | PDF (PDFsharp, A4, Seitenumbruch, Seitenzahlen), DOCX über „Trendo Text“, ATS-Text | Core `Cv_pdf_docx`, `Cv_ats_and_keywords`; UI Exporte | Reactive Resume | 1 |
| CV-06 | ATS-Fassung | reiner Text, Standard-Überschriften, keine Tabellen/Tabs, neueste Station zuerst | Core `Cv_ats_and_keywords` | ATS-Leitfäden | 1 |
| CV-07 | Stichwort-Abgleich | Fachwörter der Anzeige: schon drin / fehlt noch (Akronyme wie SAP zählen) | Core `Cv_ats_and_keywords`, Adapter-Test | Jobportale | 2 |
| CV-08 | Anschreiben | eigener Entwurf aus Stelle + Lebenslauf ohne erfundene Angaben ([Klammern]); KI über vorhandenen KI-Kern mit Regel „nichts erfinden“; Gruß/Anrede nicht doppelt; DIN-5008-Brief über Brief-Kern; Übergabe an „Briefe schreiben“ | Core `Cv_letter`, UI `Cv_letter_mappe_list` (KI-Ersatz) | Bewerbungsratgeber | 1 |
| CV-09 | Bewerbungsmappe | Deckblatt (wahlweise) + Anschreiben + Lebenslauf + Anlagen (PDF oder Bild) als eine PDF; fehlende Anlage meldet sich; Arbeitsordner wird gelöscht | Core `Cv_mappe`, UI `Cv_letter_mappe_list` | übliche Online-Bewerbung | 1 |
| CV-10 | Übersicht | Bewerbungen mit Status (vorgemerkt … zurückgezogen), Frist, Notiz; Fristen in 7 Tagen, Nachfassen nach 14 Tagen; CSV | Core `Cv_tracker`, UI `Cv_letter_mappe_list` | Bewerbungs-Tabellen | 1 |
| CV-11 | Nichts senden | Trendo versendet keine Bewerbung; Adapter ohne Nebenwirkung | Adapter-Test `Lebenslauf_Adapter` | Projektregel | 1 |

## Nicht gebaut (nächste Schritte)
- Import aus LinkedIn/JSON Resume, mehrere Lebensläufe je Akte, Sprachen der Oberfläche des Lebenslaufs (Englisch) – Oberfläche bleibt Deutsch.
