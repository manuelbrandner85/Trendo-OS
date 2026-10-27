# T-0228: Trendo Tabelle – eigener Nachbau von LibreOffice Calc (C1–C8)

**Freigabe:** „Ja, Stufe 1 komplett frei (T1–T10, C1–C8, P1–P6)“ (2026-10-25). Der Funktionsumfang ist mit REA aus dem mitgelieferten LibreOffice ermittelt: `reports/t0223-office/funktionen.md`.

**Ziel:** Trendo Tabelle ersetzt im Studio „Texte & Tabellen“ das Original für den Alltag. Leitlinie: das Maximum, aber leicht verständlich.

**Branch:** claude/T-02xx

**Stufen:**
| Stufe | Inhalt | Funktionen |
|---|---|---|
| A | Dokumentmodell (`SheetDoc`): Blätter, Zellen, Formate, Spaltenbreiten. Formel-Rechner mit deutschen und englischen Namen, Komma als Dezimalzeichen, `;` als Trenner, Fehlerwerte, Zirkelbezug. Sortieren und Filtern. | C2, C3, C4, C5 (Rechnen), C6 (Modell) |
| B | Dateien: XLSX lesen und schreiben (Blätter, Formeln, Zahlenformate, Fett, Füllfarbe, Rahmen, Spaltenbreiten), ODS lesen, CSV ein und aus | C1 |
| C | Oberfläche: Tabellenraster mit Zellbearbeitung, Formelzeile, Blatt-Reiter, Werkzeugleiste | C2–C6 |
| D | Diagramme (Balken, Linie, Kreis), PDF und Drucken | C7, C8 |

**Fertig, wenn:**
- [ ] jede Stufe Tests hat, darunter ein Abgleich gegen LibreOffice Calc (Datei hin und zurück)
- [ ] gezielte Tests während der Arbeit grün sind und der Volltest am Schluss grün ist

**Nicht in Stufe 1:** Makros, Pivot, Datenbanken, Formular-Entwurf
