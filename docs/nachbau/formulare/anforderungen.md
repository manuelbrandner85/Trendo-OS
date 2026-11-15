# Formulare ausfüllen – Anforderungen (Clean-Room)

Freigabe: 79 neue Studios am 09.11.2026, „Ja, weiter“ am 15.11.2026 (T-0672). Kategorie: Arbeiten & Büro. Studio-Id `STUDIO-FORMS-01`, Programm `formulare-programm`.

## Vorbilder (nur Bedienkonzept, kein Code, keine Grafiken)

| Vorbild | Warum | Lizenz / Zugang | Quelle |
|---|---|---|---|
| Adobe Fill & Sign | Felder direkt auf der Seite, frei Text setzen, Häkchen/Kreuz/Datum, Unterschrift zeichnen, „Meine Angaben“ (Autofill) | proprietär | helpx.adobe.com Fill & Sign (öffentlich) |
| Xodo | Formularfelder füllen, Pflichtfelder, Glätten beim Speichern | proprietär | xodo.com Hilfe (öffentlich) |
| PDF-XChange Editor | Formular vorbereiten (Felder anlegen), Glätten | proprietär | Handbuch (öffentlich) |

Hinweis: aus öffentlicher Doku und Bedienkonzept abgeleitet (Stand 15.11.2026), ohne Live-Analyse mit REA.

Wiederverwendet: `PdfTools`/PDFsharp (Fonts), `PdfView` (Seitenbild), `SignaturePad` + `LetterSignature.ToPng` (gleiches Zeichenfeld wie Briefe und Trendo PDF, gleiche gespeicherte Unterschrift), `ISecretStore` für „Meine Angaben“. Neu: `src/Trendo.Core/Sys/PdfFormular.cs`.

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stufe |
|---|---|---|---|---|---|
| FO-01 | Felder lesen | Text, Ankreuzfeld, Auswahl, Liste, Optionsfeld (mit Knöpfen), Pflicht, schreibgeschützt, Lage auf der Seite, Hinweistext | Core `FormulareTests.Read_fields` | PDF 1.7 AcroForm (frei) | 1 |
| FO-02 | Ausfüllen | auf der Seite und in der Liste; Optionsfeld setzt /V und /AS je Knopf; falsche Auswahl wird gemeldet; neue Datei | Core `Fill_all_kinds`, UI `Fill_on_page_and_flatten` | Fill & Sign | 1 |
| FO-03 | Pflichtfelder | leere Pflichtfelder rot markiert und genannt; Ankreuzfeld „nein“ zählt als leer; Speichern warnt | Core `Required_missing`, UI `Fill_on_page_and_flatten`, `Profile_suggest` | Xodo | 1 |
| FO-04 | Glätten | „Fertig speichern“ brennt das Aussehen jedes Felds in die Seite, Felder verschwinden, Werte bleiben lesbar | Core `Flatten`, UI `Fill_on_page_and_flatten` (PDFium liest „Erika“) | Xodo „Flatten“ | 1 |
| FO-05 | Ohne Felder | Text, Datum, Häkchen, Kreuz frei setzen; Unterschrift zeichnen und setzen; einzeln entfernen | Core `Overlays_and_signature`, UI `Overlays_by_clicks` | Fill & Sign | 1 |
| FO-06 | Meine Angaben | Profil nur im Geheimnisspeicher; Zuordnung nach Feldname/Hinweistext (Vorname, PLZ + Ort, Straße + Hausnummer …); Vorschlag nur für leere Felder; IBAN mit Prüfziffer, maskiert, im Dokument nur als Platzhalter „@profil:iban“, eingesetzt erst beim Speichern; nie Werte im Protokoll | Core `Profile_suggestions`, UI `Profile_suggest` | Fill & Sign „Autofill“ | 1 |
| FO-07 | Formular vorbereiten | Felder anlegen (Text, Ankreuzfeld, Auswahl, Optionsfeld); doppelte Namen abgelehnt | Core `Prepare_form` | PDF-XChange „Formular vorbereiten“ | 1 |
| FO-08 | Original bleibt | Speichern schreibt neue Datei „…-ausgefuellt.pdf“ bzw. „…-fertig.pdf“ | UI `Fill_on_page_and_flatten` (Bytes gleich) | alle | 1 |
| FO-09 | KI-Zuordnung | Adapter `formular.zuordnen`: Feldnamen → Angaben, ohne Werte, keine Nebenwirkung | Core `FormulareTests.Adapter` | – | 1 |

## Grenzen (ehrlich)
- Die Unterschrift ist ein Bild, keine qualifizierte elektronische Signatur.
- XFA-Formulare (reine Adobe-Formulare ohne AcroForm) werden nicht gelesen – dann frei setzen.
- „Formular vorbereiten“ gibt es im Kern und für Tests; im Studio noch ohne eigene Oberfläche.
