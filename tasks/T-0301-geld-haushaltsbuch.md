# T-0301 – Geld als Haushaltsbuch

- Freigabe: Manuel im Chat am 01.11.2026 (Auswahl „Geld als Haushaltsbuch“).
- Vorher: Übersicht, Budgets und eine Tabelle der letzten 60 Buchungen; Kontoauszug wurde ohne Ansicht übernommen.
- Jetzt (`MainWindow.Haushaltsbuch.cs`, `DonutChart`, Kern `HouseholdBook`):
  - Monat blättern (◀ ▶ Heute), Einnahmen / Ausgaben / Übrig mit Sparquote.
  - Ausgaben als Ring je Kategorie mit Legende, Anteil und Budget-Balken; Klick auf Segment oder Legende filtert die Liste.
  - Buchungen des Monats mit Suche, Kategorie direkt ändern (wird gelernt), „Ändern“ in der Zeile (Datum, Text, Betrag, Kategorie), Stornieren wie bisher; Rückgängig für jede Änderung.
  - Fixkosten & Abos automatisch erkannt (monatlich, vierteljährlich, jährlich; gekündigte fallen heraus) mit Monatssumme und nächstem Termin.
  - Kontoauszug erst als Vorschau: geratene Kategorie je Zeile änderbar, schon vorhandene markiert, dann „Ja, übernehmen“ oder „Nein“; Änderungen werden gelernt.
  - Zuordnungsregeln sichtbar, anlegen, löschen, auf alle Ausgaben anwenden.
- **Verbesserung Bank-Import:** Empfänger/Auftraggeber wird mit übernommen („Stadtwerke – Abschlag“ statt nur „Abschlag“) – vorher ging er verloren und die Kategorie war oft falsch. `W5FinanceTests` angepasst.
- Vorschaubild: Render-Schalter `TRENDO_GELD=1`.
- Tests: `HouseholdBookTests` (Monat, Summen, Fixkosten inkl. Zufallstreffer und gekündigter Abos, Import-Vorschau, Ändern, Regeln), `Haushaltsbuch_Ui` (echter Klick in den Ring, Suche, Ändern + Rückgängig, Monat blättern, Fixkosten, Vorschau → Ja, doppelter Auszug gesperrt, Regel anwenden); `Teil4BusinessUiTests`, `W5_Spec11_Ui`, `W5_SmallBusiness_Ui`, `W5FinanceTests` grün.
