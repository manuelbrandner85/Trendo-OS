# E-Rechnung (XRechnung) im Studio „Geld & Rechnungen“ – 08.11.2026

Alle Studios, Runde 6, SPEC-11. Bibliothek: **ZUGFeRD-csharp 18.0.1** (Apache-2.0, NuGet). Sie wird im Setup mitgeliefert. Es gibt kein Fremdprogramm und keinen Download zur Laufzeit.

## Was die Nutzerin oder der Nutzer sieht
In der Karte „Rechnung schreiben“ gibt es den aufklappbaren Bereich **„Auch als E-Rechnung (XRechnung)“** mit diesen Feldern:
- eigene USt-IdNr. oder Steuernummer
- eigene E-Mail
- IBAN
- E-Mail des Kunden
- Käuferreferenz bzw. Leitweg-ID für Behörden

Der Knopf **„Rechnung + E-Rechnung (XRechnung) erstellen“** macht drei Dinge:
- Er schreibt die gewohnte lesbare Rechnung und daneben `xrechnung-<nummer>.xml`.
- Er liest die Datei sofort wieder ein und vergleicht die Bruttosumme.
- Er bucht die Einnahme.

Die eigenen Angaben merkt sich Trendo nur lokal im Datenordner (`finanzen/e-rechnung.json`).

Fehlt eine Pflichtangabe, nennt Trendo jede einzeln in Alltagssprache, zum Beispiel „IBAN fehlt oder ist ungültig.“ In diesem Fall wird nichts geschrieben und nichts gebucht.

## Inhalt der Datei (in Tests nachgeprüft)
- **Format:** UN/CEFACT CII, Version 2.3, Profil XRechnung (Guideline-ID enthält `urn:xeinkauf.de:kosit:xrechnung`, gesetzt von der Bibliothek).
- **Parteien:**
  - Verkäufer mit Anschrift, USt-IdNr. (`VA`) oder Steuernummer (`FC`) und E-Mail (`EM`).
  - Käufer mit Anschrift und E-Mail.
- **Steuer:**
  - Regelbesteuerung: Kategorie `S` mit 19 %.
  - Kleinunternehmer (§ 19 UStG): Kategorie `E` mit 0 € USt und Befreiungsgrund im XML.
- **Zahlung:** SEPA-Überweisung mit geprüfter IBAN (echte Modulo-97-Prüfung, DE genau 22 Zeichen) und Zahlungsziel 14 Tage.
- **Summen:** Beispiel 468,00 € netto + 88,92 € USt = 556,92 € brutto. Beim Wiedereinlesen ergibt sich dieselbe Summe.

## Tests
- `ERechnungTests` (Kern) – 9 Tests, grün:
  - Anschrift zerlegen
  - Summen
  - Hin- und Rückweg
  - XML-Inhalt
  - Kleinunternehmer
  - fehlende Pflichtangaben
  - IBAN-Prüfsumme
  - fehlerhafte Positionen
  - Dateiname und Wurzelelement
- `ERechnungUi` (Oberfläche) – 1 Test, grün:
  - Ohne IBAN gibt es eine verständliche Meldung und keine Buchung.
  - Mit allen Angaben gibt es eine XML neben der Rechnung, 190,40 € brutto, eine gebuchte Einnahme und gemerkte Angaben.
- Die bisherigen Geld-Tests laufen weiter grün: Haushaltsbuch, SPEC-11, Kleinunternehmer, Teil 4 Business – zusammen 7 Oberflächentests.

## Hinweis zur Bibliothek
Laut Paketbeschreibung beendet der Hersteller die Pflege am **30.11.2026**. Ab dem 1.12.2026 gilt das Paket als „Legacy“. Die Version 18.0.1 bleibt unter Apache-2.0 nutzbar. Der Nachfolger FactoorSharp ist kommerziell und wird **nicht** genutzt.

Trendo hält die Bibliothek bewusst in einer einzigen Datei (`src/Trendo.Core/ERechnung.cs`). Kommt eine neue XRechnung-Version, die 18.0.1 nicht mehr schreibt, kann dort ein eigener CII-Schreiber an die Stelle der Bibliothek treten. Prüfung und Oberfläche bleiben dabei unverändert.

## Nicht geprüft (NOT RUN)
- Amtliche Prüfung mit dem **KoSIT-Validator** (Java, Schematron-Regeln). Dafür wäre ein Download und das Ausführen eines Fremdprogramms nötig, also nur mit Freigabe.
- Empfang durch ein echtes Buchhaltungsprogramm oder ein Behördenportal (ZRE/OZG-RE).
- ZUGFeRD-PDF (PDF/A-3 mit eingebetteter XML). Bisher entsteht nur die reine XRechnung-XML.
