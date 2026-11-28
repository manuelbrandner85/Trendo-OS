# KI-Agenten bauen – Arbeitsabläufe

Studio: KI-Agenten bauen (STUDIO-AGENTS-01) · Bereich: KI & Automatik · Seite: `agenten-bauen-programm` · Stand: 0.9.113 (Stand 1978f1a)

Die Abläufe unten sind die Oberflächentests, die das Programm über den echten Eingabeweg bedienen (Klicks, Tastatur).

- T-0698 UI +1: Leerzustand zeigt den ersten Schritt; „Agent starten“ (gold) → Agent legt echte Datei an, liest und verbessert sie, Protokoll live, Agent in der Liste
- T-0698 UI +2: Wirkung nach außen fragt in der Lauf-Ansicht – „Ablehnen“ blockiert, „Zustimmen“ führt über das Freigabe-Tor aus
- T-0698 UI +3: Stopp-Knopf in der Lauf-Ansicht beendet einen laufenden Agenten
- T-0698 UI +4: Abläufe – Baustein „Agent ausführen“ anhängen und Agent wählen (Zeitplan/Auslöser ohne zweiten Scheduler)
- T-0698 UI +5: KI-Team – Rollen geben Dateien weiter, Prüfer gibt zurück, Ergebnis-Dateien im Projekt
