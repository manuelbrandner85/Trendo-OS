# T-0707 ECHT: eine Kette, lokale KI im offenen Dokument

Datum: 2026-11-16 15:21 · KI: Ollama 127.0.0.1:11434 · Modell: qwen3:14b (Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.)
Bedienung über die Oberfläche (Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt.

## Bücher schreiben (buch-programm)

- Auftrag: Schreib für Kapitel 1 einen kurzen Absatz (drei Sätze) über einen alten Leuchtturmwärter. Ersetze den Text des Buchs dafür durch die Überschrift „# Kapitel 1“ und diesen Absatz.
- Plan (   verstanden über: KI (Ollama, qwen3:14b), 45 s): Inhalt im Dokument ändern – Stelle: text, Neuer Inhalt: # Kapitel 1

Der alte Leuchtturmwärter, Herr Müller, hatte seit 30 Jahren die Nachtwache. Seine Augen, müde von Jahrzehnten des Dienstes, schimmerten noch immer im Licht der Laterne. Obwohl die moderne Technik ihn ersetzt hatte, blieb er bis zum Ende seiner Tage am Ort seiner Pflicht.
- Datei nach „Ja, behalten“: buch-programm-nach-ki.tdoc · SHA-256 60531b764da6340202c9a0249a56e5d7c8875b4dc7a89efaeca51d1c71cec5ec
- Rückgängig: Dokument wieder wie vorher = True
- Daten danach (Ausschnitt): `{"title":"Neues Buch","author":"","goal":"50000","text":"# Kapitel 1\n\nDer alte Leuchtturmw\u00E4rter, Herr M\u00FCller, hatte seit 30 Jahren die Nachtwache. Seine Augen, m\u00FCde von Jahrzehnten des Dienstes, schimmerten noch immer im Licht der Laterne. Obwohl die moderne Technik ihn ersetzt hatte, blieb er bis zum Ende seiner Tage am Ort seiner Pflicht."}`

## Tagesplaner (tagesplaner-programm)

- Auftrag: Trag in meine Aufgaben „Wocheneinkauf“ mit 60 Minuten ein (als neue Aufgabe in der Liste tasks, Felder wie im Dokument: id, title, min, imp, due, done).
- Plan (   verstanden über: KI (Ollama, qwen3:14b), 27 s): Inhalt im Dokument ändern – Stelle: tasks, Neuer Inhalt: [{"id":"task-1","title":"Wocheneinkauf","min":60,"imp":1,"due":"2026-11-16","done":false}]
- Datei nach „Ja, behalten“: tagesplaner-programm-nach-ki.tdoc · SHA-256 e92bfd5829999a6056c4e4e39cf52a6917ab5ac19524e6319ca4abb93512857c
- Rückgängig: Dokument wieder wie vorher = True
- Daten danach (Ausschnitt): `{"day":"2026-11-16","start":"08:00","end":"17:00","work":"90","brk":"10","tasks":[{"id":"task-1","title":"Wocheneinkauf","min":60,"imp":1,"due":"2026-11-16","done":false}],"appts":[],"plan":[],"later":[]}`

## Messdaten & Auswertung (wissenschaft)

- Auftrag: Benenne die Messreihe (sheet/name) in „Kellertemperatur Januar“ um.
- Plan (   verstanden über: KI (Ollama, qwen3:14b), 11 s): Inhalt im Dokument ändern – Stelle: sheet/name, Neuer Inhalt: Kellertemperatur Januar
- Datei nach „Ja, behalten“: wissenschaft-nach-ki.tdoc · SHA-256 dcf1f1c2e1c8f54b80cbcba48df7d45017380a0a59dfa4e6d73c770c3ea8e750
- Rückgängig: Dokument wieder wie vorher = True
- Daten danach (Ausschnitt): `{"sheet":{"version":1,"name":"Kellertemperatur Januar","columns":[{"name":"x","unit":"","values":[null]},{"name":"y","unit":"","values":[null]}],"chart":{"kind":"streu","x":0,"ys":[1],"fit":"linear"},"rows":1}}`

Ergebnis: alle drei Programme bestanden.
