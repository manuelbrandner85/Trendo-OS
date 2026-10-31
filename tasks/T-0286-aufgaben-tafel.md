# T-0286 – Aufgaben als Tafel (Offen · In Arbeit · Erledigt)

- Freigabe: Manuel im Chat am 31.10.2026 („Erst Programme umbauen“).
- Vorher: Liste mit Häkchen, keine Zwischenstufe, Aufgaben nicht bearbeitbar (nur löschen und neu anlegen).
- Jetzt:
  - Drei Spalten mit Karten (Titel, Priorität, Fälligkeit – überfällig rot –, Liste, Beschreibung).
  - Karte mit der Maus in eine andere Spalte ziehen; Häkchen setzt auf „Erledigt“.
  - Klick öffnet rechts das Bearbeiten-Feld (Titel, Fällig, Priorität, Spalte, Liste, Beschreibung, Löschen).
  - Listen als Filter mit Zahl der offenen Aufgaben.
  - Rückgängig (Knopf und Strg+Z) für Anlegen, Ziehen, Häkchen, Bearbeiten und Löschen.
- Kern: `TodoItem` um `Working` (in Arbeit) und `Notes` erweitert – alte Dateien laden unverändert; `Stage`, `Calendar.SetStage`, `GetTodo`; „erledigt“ beendet „in Arbeit“. ICS: STATUS:IN-PROCESS und DESCRIPTION hin und zurück.
- Tests: neu `TodoBoard_Ui` (echtes Mausziehen, Bearbeiten, Filter, Rückgängig; Kern Spalten und ICS); bestehende Kalender-/Büro-Tests grün.
