# T-0261 – Wissenschaft & Forschung als echtes Programm „Messdaten & Auswertung“

- Freigabe: Manuel im Chat am 31.10.2026 („alles Offene in jedem Studio umsetzen“, Masterprompt GODMODE 131)
- Vorbilder (Funktionsumfang, öffentlich dokumentiert): LabPlot, JASP, Excel-Diagramme; kein Code übernommen
- Umgesetzt: Datentabelle zum Eintippen (MeasureGrid), Diagramm (Punkte/Linie/Balken/Histogramm/Boxplot), Ausgleich (Gerade, Parabel, Exponential, Potenz) mit R², Kennzahlen, Vergleich (Welch, gepaart, Korrelation), neue Spalten (Glättung, z-Werte), CSV laden/speichern, Bericht, SVG, Live-Rechner (Chi², Stichprobe, Unsicherheit)
- Gefunden und behoben: Absturz der Seite bei genau zwei Wertepaaren (Ausgleich); alte Tabelle stahl nach dem Speichern den Fokus; Dateifelder in schmalen Spalten ragten aus dem Fenster (Pfadzeile jetzt umbrechend)
- Tests: Core 3 (Ausgleich gegen Physik: Pendel 2π/√g, Abkühlung, exakte Parabel), App 2 (echte Tastatur in der Tabelle) + T-0201-Dateiauswahl umgestellt
