# Funktionsliste: Aufgaben automatisieren

Studio SYS-05 · Aufgaben automatisieren · Bereich KI & Automatik

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

| Nr | Funktion | Stand laut Katalog | Test |
|---|---|---|---|
| AL-N1 | n8n als Programm | umgesetzt, Box getestet | N8nTests.Setup_local_only, N8nWorkspace_Ui |
| AL-N2 | Trendo KI | umgesetzt, Box getestet | AutomationApiTests, N8nTests.Ki_route |
| AL-N3 | Trendo-Aktionen und -Ereignisse | umgesetzt, Box getestet | AutomationApiTests, N8nTests.Hooks_local, N8nWorkspace_Ui |
| AL-N4 | Vorlagen je Studio | umgesetzt, Box getestet | N8nTests.Templates_valid |
| AL-N5 | Claude baut Abläufe | umgesetzt, Box getestet | N8nTests.Check_review, N8nWorkspace_Ui |
| AL-N6 | Austausch Trendo ↔ n8n | umgesetzt, Box getestet | N8nTests.From_trendo |
| AL-N7 | Node-RED als freie Alternative | umgesetzt, Box getestet (Einrichtung); Mitliefern im Setup offen (Sitzung A) | N8nTests.Setup_local_only |

## Fehlende Funktionen laut Vorbild-Analyse

- ✗ Ablauf als Bausteine mit Linien
- ✗ Probelauf ohne echte Wirkung (Vorschlag 37)
- ✗ Protokoll je Lauf

## Teilweise vorhanden

- ◐ Mitliefern von Node-RED im Setup (Sitzung A); PC-Prüfung mit echtem n8n offen
