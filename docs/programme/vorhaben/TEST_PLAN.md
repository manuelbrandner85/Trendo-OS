# Vorhaben – Testplan

Studio: Neues Projekt starten (X-11) · Bereich: Arbeiten & Büro · Seite: `vorhaben` · Stand: 0.9.110 (Stand 0eb9d52)

10 Tests sprechen dieses Programm an.

- T-0308: Gantt – Balken ziehen verschiebt um Tage, Kante ziehen verlängert, Punkt auf anderen Balken = Abhängigkeit mit Nachrücken, Rückgängig, Beginn/Ende/„kommt nach“ im Feld
- T-0717 Vorhaben: Zeitplan – Rückgängig/Wiederholen der Hülle (vorher eigener Stapel ohne Wiederholen) über mehrere Schritte, gespeichert nach dem Schließen
- T-0725 Echtlauf Neues Projekt: LibreOffice Calc öffnet die Schritte-CSV (Semikolon, UTF-8), Ical.Net liest die Kalenderdatei; Messwerte: LibreOffice öffnet das Diagramm-PNG
- T-0725 Neues Projekt: Beispiel-Vorhaben → „Schritte als Tabelle (.csv)“ und „Zeitplan als Kalenderdatei (.ics)“ landen in der Mappe; Ical.Net liest jeden Schritt mit Zeitraum
- Vorhaben: „Beispiel ansehen“ legt ein Beispielprojekt an und öffnet den Planer mit Übersicht, Liste, Tafel, Zeitplan, Dateien
- Vorhaben: „In den Kalender“ trägt die Fälligkeiten ein, zweites Mal ohne Doppelte, erledigter Schritt verschwindet wieder aus dem Kalender
- X-11 (UI): Vorhaben anlegen, Schritt abhaken, Fortschritt steigt
- X-11 F1/F2/U1/U2: Vorhaben mit Ring, Kanban verschieben, Zeitplan mit Meilenstein, Dateien des Vorhabens
- X-11 F1/U1: Vorhaben mit Startdatum – Fälligkeiten, Meilenstein, Kanban-Zustände, alte Listen lesbar
- X-11: Jedes Vorhaben legt Ordner, Startdateien und Schrittliste an; Abhaken und Fortschritt
