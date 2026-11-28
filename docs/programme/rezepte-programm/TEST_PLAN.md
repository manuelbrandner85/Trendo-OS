# Rezepte & Ernährung – Testplan

Studio: Rezepte & Ernährung (STUDIO-RECIPE-01) · Bereich: Wissen & Lernen · Seite: `rezepte-programm` · Stand: 0.9.114 (Stand 9bc135a)

5 Tests sprechen dieses Programm an.

- T-0787 ECHT: lokale KI (Ollama) macht aus einem eingefügten Rezepttext ein Rezept im offenen Buch – Vorschau, „Ja, behalten“, Nährwerte vom Kern, Rückgängig (nur mit TRENDO_T0787_ECHT)
- T-0787 Fremddatei: gespeicherte Rezeptseite mit JSON-LD (@graph, HowToSection) über „Rezept öffnen …“ einlesen, Datei unverändert; Rechtsklick wählt anderes Lebensmittel; Einfach zeigt die Grundbefehle
- T-0787 KI schreibt ein Rezept ins offene Buch: Vorschau ändert nichts, „Ja, behalten“ legt es an (ein Rückgängig-Schritt, Nährwerte vom Kern), Rückgängig nimmt es heraus; Heilversprechen wird abgelehnt (KI ersetzt)
- T-0787 Rezepte: leeres Buch → „Erstes Rezept anlegen“ → Name, Zutaten, Schritte tippen → Tabelle mit Gramm/kcal → für 8 anzeigen und übernehmen (ein Rückgängig-Schritt) → PDF, JSON-LD, HTML, DOCX
- T-0787 Wochenplan und Einkaufsliste über die Oberfläche: Rezept aus Text, auf Tage legen, Liste zusammengefasst, abhaken, eigener Eintrag, Rechtsklick „auf Freitag“, Export PDF und CSV
