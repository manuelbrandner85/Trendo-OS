# Geld – Testplan

Studio: Geld & Rechnungen (SPEC-11) · Bereich: Business & Geld · Seite: `finanzen` · Stand: 0.9.110 (Stand 0eb9d52)

17 Tests sprechen dieses Programm an.

- E-Rechnung-Oberfläche: fehlende Angaben verständlich, nichts gebucht; vollständig → XRechnung neben der Rechnung, Einnahme gebucht, eigene Angaben gemerkt
- Geld: Übersicht mit Saldo und Diagramm, Budget-Warnung, Kategorie in der Tabelle ändern wird gelernt, Steuerberater-ZIP
- SPEC-11 (UI): Buchung eintragen, Rechnung erstellen, Jahresgewinn wird angezeigt
- SPEC-11 F1/F2: Haushalts-Kategorien erkennen; Zuordnung des Nutzers wird gelernt und hat Vorrang
- SPEC-11 F3/F5: Logo in der Rechnung, ZIP für den Steuerberater mit Buchungen, Übersicht und Rechnungen
- SPEC-11 U1/F4: Monatswerte, Saldo, Budget mit Warnstufen
- SPEC-11: Bank-CSV (deutsches Format, Anführungszeichen) wird gelesen, Kategorien geraten
- SPEC-11: Netto/MwSt, Monats- und Jahresübersicht stimmen; Export-Import ohne Verlust; doppelte übersprungen
- SPEC-11: Rechnung – fortlaufende Nummer, Summen, Kleinunternehmer-Hinweis, keine doppelte Nummer
- Studio-Ordnung: Übersicht zeigt Unterteilungen und Anzahl; „Zuletzt benutzt“ erscheint nach dem Öffnen eines Studios und nicht bei der Suche
- T-0301: Ring-Klick filtert, Suche, Buchung ändern + Rückgängig, Monat blättern, Fixkosten, Kontoauszug erst Vorschau dann Ja, Regel anlegen
- T-0694 UI: Angebot → Rechnung, Rechnung aus Beschreibung (auch für Claude), Storno mit eigener Nummer; neue Buchung ist rückgängig machbar
- T-0694 UI: Auswertung als PDF, XLSX und CSV, DATEV-Buchungsstapel; Kredit-Rechner zeigt Rate und Restschuld 0,00 € und speichert den Tilgungsplan; Einfach blendet DATEV aus
- T-0694 UI: Kontoauszug öffnen (CAMT.053) → Vorschau mit Kontrolle der Salden und Vorschlag „Rechnung bezahlt“ → Ja übernimmt Buchungen und markiert die Rechnung; MT940 derselben Buchungen wird als doppelt erkannt; unbekannte CSV fragt nach den Spalten
- T-0694 UI: leeres Programm → Meine Firma → neue Rechnung mit zwei Steuersätzen → fehlende Pflichtangabe wird genau benannt → festschreiben → PDF, ZUGFeRD-PDF und XRechnung als echte Dateien; Zahlung zuordnen; Rückgängig und Wiederholen
- T-0698 Werkzeug Studio: Studio-Aktion über den vorhandenen Studio-Ablauf (echter Adapter SPEC-11) legt eine Rechnung im Projekt an, bucht nichts
- § 19 UStG (UI): Kleinunternehmer-Prüfung auf Geld- und Business-Seite
