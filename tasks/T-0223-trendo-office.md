# T-0223: Eigenes Trendo Office (Text, Tabelle, PDF) – Nachbau mit REA

**Auftrag (Manuel, 2026-10-25):** „Mitliefern + nachbauen“, zuerst Office (Text, Tabelle, PDF). „Nutze für den Nachbau REA.“
**Branch:** claude/T-02xx
**Vorbild:** die mitgelieferten Programme aus dem Vollpaket (T-0222): LibreOffice 26.2.4, SumatraPDF 3.6.1, qpdf 12.4.2.

## Vorgehen
1. **Funktionsumfang mit REA ermitteln** (nur die ausgelieferten Dateien aus `artifacts/vollpaket/staging/`, keine fremden Server):
   - `open_binary`/`inspect_artifact` auf die Programmdateien und Pakete: Menübefehle, unterstützte Formate, Filter, Druck- und Exportwege.
   - `inspect_pe_resources` für Menüs, Dialoge und Symbole von SumatraPDF.
   - qpdf: Befehle und Optionen aus dem Programm (Zusammenfügen, Teilen, Drehen, Verschlüsseln, Reparieren).
   - Ergebnis: Funktionsliste mit Evidence-IDs in `reports/t0223-office/funktionen.md` – getrennt nach „beobachtet“, „abgeleitet“ und „unbekannt“.
2. **Auswahl:** die wichtigsten Funktionen für den Alltag (Brief, Lebenslauf, Haushaltsbuch, Rechnung, PDF-Formular) – Liste zur Freigabe an Manuel.
3. **Bauen** in bestehenden Modulen (kein zweiter Office-Kern): Trendo Text (DOCX/ODT lesen und schreiben), Trendo Tabelle (XLSX/ODS, Formeln), Trendo PDF (ansehen, zusammenfügen, teilen, drehen, Formulare). Nur Bibliotheken mit freier Lizenz.
4. **Abnahme:** dieselben Testdateien in Trendo Office und im Original öffnen und speichern; Unterschiede als Bericht.
5. Erst wenn ein Teil reicht, ersetzt er im Studio das Original (Original bleibt im Vollpaket auswählbar).

**Fertig, wenn:** Funktionsliste mit REA-Belegen, freigegebene Auswahl, eigene Programme mit Tests, Vergleichsbericht.
**Nicht tun:** keinen fremden Programmcode übernehmen (MPL/GPL-Code nur mit Lizenzprüfung), keine Marken (Namen, Symbole) der Originale verwenden.
