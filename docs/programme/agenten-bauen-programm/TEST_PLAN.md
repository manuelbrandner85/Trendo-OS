# KI-Agenten bauen – Testplan

Studio: KI-Agenten bauen (STUDIO-AGENTS-01) · Bereich: KI & Automatik · Seite: `agenten-bauen-programm` · Stand: 0.9.114 (Stand 99cbb5c)

8 Tests sprechen dieses Programm an.

- KI-Agenten: Prüfung, Werkzeug mit Warnung, Speichern erst nach Ordnerwahl und nie überschreiben, Rolle im KI-Team, Export und Einlesen
- T-0698 ECHT Claude: Agent mit Claude über das eigene Abo (nur mit TRENDO_AGENT_ECHT und angemeldetem Claude Code)
- T-0698 ECHT Ollama: Agent mit lokalem Modell legt Datei an, liest sie, ergänzt sie (nur mit TRENDO_AGENT_ECHT)
- T-0698 UI +1: Leerzustand zeigt den ersten Schritt; „Agent starten“ (gold) → Agent legt echte Datei an, liest und verbessert sie, Protokoll live, Agent in der Liste
- T-0698 UI +2: Wirkung nach außen fragt in der Lauf-Ansicht – „Ablehnen“ blockiert, „Zustimmen“ führt über das Freigabe-Tor aus
- T-0698 UI +3: Stopp-Knopf in der Lauf-Ansicht beendet einen laufenden Agenten
- T-0698 UI +4: Abläufe – Baustein „Agent ausführen“ anhängen und Agent wählen (Zeitplan/Auslöser ohne zweiten Scheduler)
- T-0698 UI +5: KI-Team – Rollen geben Dateien weiter, Prüfer gibt zurück, Ergebnis-Dateien im Projekt
