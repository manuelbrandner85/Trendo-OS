# Abläufe (Automatisch erledigen) – Umsetzungsstand

Studio: Aufgaben automatisieren (SYS-05) · Bereich: KI & Automatik · Seite: `ablaeufe` · Stand: 0.9.36 (Stand df8e056)

Stufen: Geplant · In Arbeit · Implementiert · Getestet · Blockiert. „Getestet“ nur, wenn der zugehörige Test im letzten echten Lauf bestanden hat.

| Nr | Funktion | Stand | Test |
|---|---|---|---|
| AL-N1 | n8n als Programm | Implementiert | `N8nTests.Setup_local_only, N8nWorkspace_Ui` |
| AL-N2 | Trendo KI | Implementiert | `AutomationApiTests, N8nTests.Ki_route` |
| AL-N3 | Trendo-Aktionen und -Ereignisse | Implementiert | `AutomationApiTests, N8nTests.Hooks_local, N8nWorkspace_Ui` |
| AL-N4 | Vorlagen je Studio | Implementiert | `N8nTests.Templates_valid` |
| AL-N5 | Claude baut Abläufe | Implementiert | `N8nTests.Check_review, N8nWorkspace_Ui` |
| AL-N6 | Austausch Trendo ↔ n8n | Implementiert | `N8nTests.From_trendo` |
| AL-N7 | Node-RED als freie Alternative | Implementiert | `N8nTests.Setup_local_only` |

0 von 7 Funktionen getestet.
