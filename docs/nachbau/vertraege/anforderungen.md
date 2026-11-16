# Verträge-Ordner – Anforderungen (Clean-Room)

Freigabe: 79 neue Studios am 09.11.2026, „Ja, weiter“ am 15.11.2026 (T-0671). Kategorie: Arbeiten & Büro. Id `STUDIO-CONTRACTS-01`, Programm `vertraege-programm`.

## Vorbilder (nur Bedienkonzept)

| Vorbild | Warum | Lizenz / Zugang | Quelle (Stand 15.11.2026) |
|---|---|---|---|
| Volders | Vertragsassistent: Laufzeit, Erinnerung vor der Kündigungsfrist | proprietär | netzwelt.de (Testbericht), finanztip.de (Forum), apps.apple.com |
| Aboalarm | Kündigungsschreiben, Fristen | proprietär | netzwelt.de/aboalarm/testbericht.html, test.de (Online-Kündigung) |
| Finanzguru | Fixkosten nach Kategorien | proprietär | Produktseite |

Wiederverwendet: Kalenderdatei über `Trendo.Core.Sys.Ics.Export` (Kalender „E-Mails & Termine“), Brief-Vorlage „Kündigung“ aus `LetterKit`, PDF über `LetterPdf`, Übergabe an „Briefe schreiben“. Keine Rechtsberatung, nichts wird versendet.

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stufe |
|---|---|---|---|---|---|
| CON-01 | Vertrag erfassen | Name, Anbieter, Anschrift, Kategorie, Kundennummer, Beginn, Mindestlaufzeit, Verlängerung, Frist (Tage/Wochen/Monate), zum Monatsende, Kosten + Zahlweise, Dokumente, Notiz | UI `Contracts` | Volders | 1 |
| CON-02 | Spätestens kündigen bis | rückwärts vom Vertragsende: Monate über den Folgetag (30.06./3 M → 31.03.; 28.02./1 M → 31.01.; 31.12./1 M → 30.11.; Schaltjahr), Wochen und Tage genau | Core `Contracts_deadline` | BGB-Fristrechnung (Praxis) | 1 |
| CON-03 | Nächstes Ende | Mindestlaufzeit endet am Tag vor dem Jahrestag; verpasst → nächste Verlängerung; unbefristet zum Monatsende oder taggenau; „endet von selbst“; ungültiger Beginn meldet sich | Core `Contracts_end` | Vertragspraxis | 1 |
| CON-04 | Fristen-Ampel | rot ≤ 14 Tage, gelb ≤ 60, grün mehr, grau gekündigt/abgelaufen; Liste sortiert nach Dringlichkeit | Core `Contracts_ampel_costs`, UI `Contracts` | Aboalarm | 1 |
| CON-05 | Kosten | pro Monat/Jahr (Quartal, Halbjahr, Jahr umgerechnet, einmalig zählt nicht), nach Kategorie, gekündigte zählen ab Ende nicht, „gespart“ | Core `Contracts_ampel_costs`, UI Kennzahlen | Finanzguru | 1 |
| CON-06 | Erinnerungen | Kalenderdatei: ganztägig am letzten Kündigungstag, Alarm X Tage vorher (Standard 14), von Ical.Net lesbar | Core `Contracts_ics`, UI `Contracts` | Volders | 1 |
| CON-07 | Kündigungsschreiben | Brief-Vorlage „Kündigung“ mit Kundennummer, „zum <Ende>, hilfsweise nächstmöglich“, Bitte um Bestätigung; an „Briefe schreiben“ oder als PDF | Core `Contracts_letter`, UI `Contracts` | Aboalarm | 1 |
| CON-08 | Als gekündigt markieren | Ende wird gespeichert, Ampel grau, Kosten fallen ab Ende weg; rückgängig möglich | UI `Contracts` | Volders | 1 |
| CON-09 | Nichts senden | Trendo kündigt und versendet nichts; Adapter ohne Nebenwirkung | Adapter-Test `Vertrag_Adapter` | Projektregel | 1 |

## Nicht gebaut (nächste Schritte)
- Verträge aus Kontoauszügen erkennen (Bankanbindung), Sonderkündigungsrecht-Assistent, Faxversand (bewusst nicht).
