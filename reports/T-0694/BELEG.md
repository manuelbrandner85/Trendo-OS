# T-0694 Beleg – Geld & Rechnungen

Erzeugt am 16.11.2026 mit dem Kern dieses Zweigs (Skript im Sitzungs-Notizordner, nicht im Repo). Beispieldaten neutral (Erika Muster, Beispiel GmbH),
IBANs sind öffentliche Beispiel-IBANs mit gültiger Prüfziffer. Prüfsummen: `SHA256SUMS.txt`.

| Datei | Was | Geprüft mit |
|---|---|---|
| `belege/rechnung-RE-2026-001.pdf` | Rechnung, 3 Positionen, 19 % + 7 %, Rabatt | pdftotext 4.06 (fremdes Programm, Xpdf): alle Pflichtangaben im Text, `rohlog-pdftotext.txt` |
| `belege/rechnung-RE-2026-001-zugferd.pdf` | E-Rechnung ZUGFeRD/Factur-X (EN 16931), factur-x.xml eingebettet | pdftotext; eingebettete XML mit ZUGFeRD-csharp wieder gelesen: 1.715,08 / 319,14 / 2.034,22 € (`rohlog-kern.txt`) |
| `belege/xrechnung-RE-2026-001.xml` | XRechnung CII 2.3 | ZUGFeRD-csharp wieder gelesen (`rohlog-kern.txt`) |
| `belege/beispiel-camt053.xml`, `beispiel-mt940.sta` | handgeschriebene Auszüge nach Spezifikation | Anzahl/Summe passen zu Anfangs-/Endsaldo (`rohlog-kern.txt`) |
| `belege/auswertung-2026.pdf/.xlsx/.csv` | Auswertung | pdftotext; XLSX mit Trendos eigenem Leser wieder geöffnet |
| `belege/EXTF_Buchungsstapel_2026.csv` | DATEV-Buchungsstapel | nur Formatprüfung im Test – **ungeprüft gegen DATEV-Software** |
| `belege/tilgungsplan-10000-5-60.pdf/.csv` | 10.000 €, 5 %, 60 Monate: Rate 188,71 €, Restschuld 0,00 € | pdftotext; Gegenrechnung im Test |

**NICHT GETESTET:** PDF/A-3-Konformität und EN-16931-/XRechnung-Schematron (kein veraPDF, Mustang oder KoSIT-Validator auf diesem PC; Schemata nicht beigelegt), Öffnen der XLSX in Excel/LibreOffice (beides nicht installiert – **Nachtrag T-0776:** falsch für LibreOffice, es liegt im Trendo-Werkzeugordner; die XLSX öffnet dort mit nachgerechneten Formeln, `reports/T-0776/libreoffice-geld-xlsx/`; Excel bleibt ungeprüft), Import der DATEV-Datei in DATEV, Exporte echter Banken.
