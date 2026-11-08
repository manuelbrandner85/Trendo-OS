# Schnittmuster nach Maß mit FreeSewing – 08.11.2026

Alle Studios, Runde 7, SPEC-08 „Kleidung & Produkte entwerfen“.

**FreeSewing 4.10.2** (MIT, stabil seit 13.09.2026). Das Projekt ist auf GitHub archiviert, wird aber auf Codeberg aktiv weiterentwickelt.
- **Pakete:** `@freesewing/core`, `models` und `plugin-theme` sowie 11 Schnitte: T-Shirt (teagan), Tanktop (aaron), Hemd (simon), Pullover (sven), Kapuzenpulli (hugo), Hosen-Grundschnitt (titan), Chino (charlie), Wickelhose (waralee), Bleistiftrock (penelope), Tellerrock (sandy), Damen-Oberteil-Grundschnitt (bella).
- **Einrichtung:** erst nach Klick per npm, feste Fassung, ohne Installationsskripte. Tatsächlich geladen: 33 Pakete, **2,1 MB**. In der Oberfläche stand zuerst „ca. 15 MB“; das ist korrigiert auf „ca. 3 MB“.

## Was die Nutzerin oder der Nutzer sieht
Im Programm „Kleidung & Produkte“ gibt es unter den Maßen den Bereich **„Schnittmuster nach Maß (FreeSewing)“**:
- **Grundmaße:** Brust, Taille und Hüfte kommen aus dem Programm. Die Körperform richtet sich nach „Damengrößen“.
- **Freiwillige Maße:** Halsumfang, Oberarmumfang, Innenbeinlänge, Schulterbreite und Rückenlänge.
- **Auswahl:** Schnitt und Nahtzugabe (0, 1 oder 1,5 cm). Der Knopf heißt **„Schnittmuster erzeugen“**.

Das Ergebnis ist eine SVG-Datei in Millimetern zum Drucken im Maßstab 1:1 oder für einen Schneideplotter. Trendo zeigt außerdem:
- die Größe des Schnittbogens,
- **welche Maße aus der Standardgröße ergänzt wurden** (auf Deutsch),
- den Druckhinweis „100 %, nicht an Seite anpassen“.

## Umsetzung
- **Startskript** `trendo-schnitt.mjs` (eigener Code, ES-Modul):
  - Es lädt den gewählten Schnitt und liest dessen Pflichtmaße (`patternConfig.measurements`).
  - Aus `@freesewing/models` wählt es die Standardgröße mit dem nächstliegenden Brustumfang (Frau oder Mann).
  - Damit ergänzt es nur fehlende Maße und meldet sie.
  - Dann setzt es den Schnitt mit Nahtzugabe und schreibt die SVG-Datei.
  - Die Schnittschnittstelle habe ich vorher im heruntergeladenen Quellcode **gelesen**, nicht ausgeführt.
- **C#** `Sewing`:
  - Umrechnung cm → mm (Schulterneigung bleibt in Grad).
  - 38 Maßnamen auf Deutsch.
  - Eingabeprüfung: nur bekannte Schnitte, Brustumfang 40–200 cm, Maße 0–300 cm, Nahtzugabe 0–30 mm.
  - Die temporäre Maßdatei wird danach gelöscht.

## Tests
- **`SewingTests`** (5 Tests, grün):
  - Umrechnung
  - Antwort lesen samt deutschem Kurztext
  - Übergabe aller Argumente; Maßdatei in mm, danach gelöscht
  - Katalog und Einrichtung
  - verständliche Meldungen bei allen Fehlerfällen
- **`Sewing_Ui`** (1 Oberflächentest, grün):
  - Ohne Einrichtung erscheint der Einrichten-Knopf.
  - Mit (nachgebildetem) FreeSewing entsteht das T-Shirt mit Halsumfang 39 cm → 390 mm und Herrengröße.
  - Ergänzte Maße werden auf Deutsch genannt; der Knopf „Schnittmuster öffnen“ erscheint.
  - Ein unbekannter Schnitt wird abgelehnt.
- Der bisherige SPEC-08-Oberflächentest bleibt grün.
- **Echtlauf `SewingRealTests.Real_draft`** (T-Shirt und Hose, Brust 92 / Taille 76 / Hüfte 100): **NOT RUN**. Er wartet auf die Freigabe des Nutzers, weil heruntergeladener Code ausgeführt würde. Der Ordner liegt geparkt; ohne Freigabe wird der Test übersprungen.

## Nicht geprüft (NOT RUN)
- Der echte Lauf (siehe oben).
- Ausdruck und Probeschnitt.
- Kachelung auf A4-Seiten. Bisher entsteht ein großer Bogen als SVG.
