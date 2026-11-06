# SYS-05 – Abläufe (Automatisch erledigen): Funktionskatalog Profi-Ebene n8n / Node-RED

> **Status: APPROVED_SPEC.** Vorschläge N1–N7 vom Nutzer am 06.11.2026 im Chat freigegeben („Alle ja“). Freigegeben heißt: Umfang festgelegt – nicht ACCEPTED. Kein eigenes Studio: n8n ist die Profi-Ebene in „KI & Automatik → Abläufe“. n8n wird wegen seiner Lizenz (Sustainable Use License) **nicht mitgeliefert**, sondern erst nach Ja aus der offiziellen npm-Quelle in den Werkzeugordner geladen.

| ID | Funktion | Beschreibung | Abnahmekriterium | Stand im Code | Test |
|---|---|---|---|---|---|
| AL-N1 | n8n als Programm | Einrichten (npm, eigener Ordner, kein global), Starten/Stoppen, Editor öffnen | Laden erst nach „Ja, einrichten“; nur 127.0.0.1, Telemetrie aus; nur eigener Prozess wird beendet | umgesetzt, Box getestet | N8nTests.Setup_local_only, N8nWorkspace_Ui |
| AL-N2 | Trendo KI | Eine OpenAI-kompatible Adresse (`127.0.0.1:47800/v1`) für Ollama, LM Studio, llama.cpp, LocalAI; Zugang in n8n eintragen | Modell „anbieter/modell“ wird an den Anbieter weitergeleitet; ohne Schlüssel 401 | umgesetzt, Box getestet | AutomationApiTests, N8nTests.Ki_route |
| AL-N3 | Trendo-Aktionen und -Ereignisse | Meldung, Termin, Notiz, Brief-Entwurf (nie senden), Trendo-Ablauf starten; Ereignisse druck-fertig, frist-bald, ablauf-fertig, test | Rechteprüfung + Protokoll; Webhooks nur auf diesem PC; unfreigegebener Ablauf läuft nicht echt | umgesetzt, Box getestet | AutomationApiTests, N8nTests.Hooks_local, N8nWorkspace_Ui |
| AL-N4 | Vorlagen je Studio | Mail sortieren, Rechnung → Termin, Druck fertig → Handy, Nachrichten zusammenfassen, Fristen-Erinnerung, Fotos ordnen | Alle gültig; „wirkt nach außen“ markiert | umgesetzt, Box getestet | N8nTests.Templates_valid |
| AL-N5 | Claude baut Abläufe | Claude legt `<name>.n8n.json` ab, Trendo zeigt sie als Fluss, Anlegen nach Ja | Prüfung meldet fremde Dienste, Befehls-Bausteine, doppelte Namen | umgesetzt, Box getestet | N8nTests.Check_review, N8nWorkspace_Ui |
| AL-N6 | Austausch Trendo ↔ n8n | Trendo-Ablauf als n8n-Ablauf mit gleichem Auslöser; n8n startet Trendo-Abläufe | Auslöser täglich/Intervall/Datei/Hand bleiben gleich | umgesetzt, Box getestet | N8nTests.From_trendo |
| AL-N7 | Node-RED als freie Alternative | Apache-2.0, gleiche Einrichtung nur lokal | uiHost 127.0.0.1 | umgesetzt, Box getestet (Einrichtung); Mitliefern im Setup offen (Sitzung A) | N8nTests.Setup_local_only |

PC-Prüfung mit echtem n8n offen (Einrichten lädt mehrere hundert MB).
