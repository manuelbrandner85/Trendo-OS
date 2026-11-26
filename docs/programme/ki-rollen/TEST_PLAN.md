# KI-Rollen und Teams – Testplan

Studio: KI-Team einsetzen (CORE-03) · Bereich: KI & Automatik · Seite: `ki-rollen` · Stand: 0.9.110 (Stand 0eb9d52)

11 Tests sprechen dieses Programm an.

- CORE-03 UI: Rolle wählen, starten, Team-Ablauf, Speichern über Freigabe
- CORE-03: 12 Rollen mit eigenem Auftrag, Aufgabe nutzt Rollenanweisung
- CORE-03: Speichern nur mit Schreibrecht, nur nach Freigabe, nur im Projekt; Claude-Code-Auftrag ehrlich
- CORE-03: Team gibt Ergebnis weiter, Aufgaben sind je Projekt getrennt
- I2/I4: KI-Rollen als Karten (Auftrag, Schreibrecht), Wahl markiert; Ablauf-Vorlagen als Karten, ohne Ablauf Leerzustand, Vorlage legt Ablauf an
- T-0698 UI +5: KI-Team – Rollen geben Dateien weiter, Prüfer gibt zurück, Ergebnis-Dateien im Projekt
- T-0717 KI-Rollen: Auftrag – Rückgängig/Wiederholen, Notsicherung, Karte, Zurückholen
- T-0717 KI-Rollen: neue eigene Rolle – Rückgängig/Wiederholen, Notsicherung, Karte öffnet das Formular wieder
- T-0741 ECHT Ollama: KI-Team über die Oberfläche – Stopp, Zwischenstand, Fortsetzen (nur mit TRENDO_TEAM_ECHT)
- T-0741 UI +1: Fortschritt je Rolle live, Stopp mitten im Lauf, Zwischenstand speichern, Team-Lauf fortsetzen bis fertig
- T-0741 UI +2: Rechercheur fragt vor dem Internet-Abruf – erst nach „Zustimmen“ wird gelesen, Ergebnis landet in der Übergabe-Datei
