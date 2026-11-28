# Geld – Arbeitsabläufe

Studio: Geld & Rechnungen (SPEC-11) · Bereich: Business & Geld · Seite: `finanzen` · Stand: 0.9.114 (Stand 99cbb5c)

Die Abläufe unten sind die Oberflächentests, die das Programm über den echten Eingabeweg bedienen (Klicks, Tastatur).

- T-0694 UI: Angebot → Rechnung, Rechnung aus Beschreibung (auch für Claude), Storno mit eigener Nummer; neue Buchung ist rückgängig machbar
- T-0694 UI: Auswertung als PDF, XLSX und CSV, DATEV-Buchungsstapel; Kredit-Rechner zeigt Rate und Restschuld 0,00 € und speichert den Tilgungsplan; Einfach blendet DATEV aus
- T-0694 UI: Kontoauszug öffnen (CAMT.053) → Vorschau mit Kontrolle der Salden und Vorschlag „Rechnung bezahlt“ → Ja übernimmt Buchungen und markiert die Rechnung; MT940 derselben Buchungen wird als doppelt erkannt; unbekannte CSV fragt nach den Spalten
- T-0694 UI: leeres Programm → Meine Firma → neue Rechnung mit zwei Steuersätzen → fehlende Pflichtangabe wird genau benannt → festschreiben → PDF, ZUGFeRD-PDF und XRechnung als echte Dateien; Zahlung zuordnen; Rückgängig und Wiederholen
