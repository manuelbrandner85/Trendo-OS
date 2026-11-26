# T-0780 ECHT: KI schreibt ins offene Dokument (Referate & Hausarbeiten, Zeitleisten)

Datum: 2026-11-26 16:56 · KI: Ollama 127.0.0.1:11434 · Modell: qwen3:14b (Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.)
Wartezeit der Kette auf die erste KI-Antwort für diesen Lauf: 8 min (Standard 90 s) – der Grafikspeicher ist mit anderen Programmen geteilt, das Modell rechnet teilweise auf dem Prozessor.
Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt.

## Referate – Gliederung

- Auftrag: Erstelle für meine Hausarbeit zum Thema „Bienen und ihre Bedeutung für die Landwirtschaft“ eine Gliederung mit Einleitung, drei Kapiteln im Hauptteil und Fazit.
- Phase nach 201 s: vorschau – So sieht es aus. Behalten? Erst bei „Ja, behalten“ kommt es ins Dokument – ein Rückgängig-Schritt nimmt es wieder heraus.
- Plan: Gliederung erzeugen – Gliederung (eine Zeile je Punkt, z. B. „1 Einleitung“, „1.1 Fragestellung“): 1 Einleitung
1.1 Problemstellung
1.2 Zielsetzung der Arbeit
1.3 Aufbau der Arbeit

2 Die Rolle der Bienen in der Bestäubung
2.1 Die Bedeutung der Bestäubung für Pflanzenwachstum
2.2 Der Einfluss von Bienen auf die Fruchtentwicklung
2.3 Vergleich zwischen natürlicher und künstlicher Bestäubung

3 Wirtschaftliche Bedeutung der Bienen für die Landwirtschaft
3.1 Beitrag der Bienen zur Nahrungsmittelproduktion
3.2 Schätzungen des wirtschaftlichen Wertes von Bienenbestäubung
3.3 Regionale Unterschiede in der Abhängigkeit von Bienen

4 Bedrohungen für die Bienengesundheit und Schutzmaßnahmen
4.1 Ursachen der Bienentod- und Bienengesundheitsprobleme
4.2 Auswirkungen auf die Landwirtschaft
4.3 Maßnahmen zur Erhaltung und Förderung von Bienen

5 Fazit
5.1 Zusammenfassung der Ergebnisse
5.2 Empfehlungen für die Praxis
5.3 Ausblick auf zukünftige Forschung
- KI-Antwort (roh): `{"steps":[{"action":"referat.gliederung","args":{"gliederung":"1 Einleitung\n1.1 Problemstellung\n1.2 Zielsetzung der Arbeit\n1.3 Aufbau der Arbeit\n\n2 Die Rolle der Bienen in der Bestäubung\n2.1 Die Bedeutung der Bestäubung für Pflanzenwachstum\n2.2 Der Einfluss von Bienen auf die Fruchtentwicklung\n2.3 Vergleich zwischen natürlicher und künstlicher Bestäubung\n\n3 Wirtschaftliche Bedeutung der Bienen für die Landwirtschaft\n3.1 Beitrag der Bienen zur Nahrungsmittelproduktion\n3.2 Schätzungen des wirtschaftlichen Wertes von Bienenbestäubung\n3.3 Regionale Unterschiede in der Abhängigkeit von Bienen\n\n4 Bedrohungen für die Bienengesundheit und Schutzmaßnahmen\n4.1 Ursachen der Bienentod- und Bienengesundheitsprobleme\n4.2 Auswirkungen auf die Landwirtschaft\n4.3 Maßnahmen zur Erhaltung und Förderung von Bienen\n\n5 Fazit\n5.1 Zusammenfassung der Ergebnisse\n5.2 Empfehlungen für die Praxis\n5.3 Ausblick auf zukünftige Forschung"}],"frage":"Gibt es weitere Anforderungen an die Gliederung oder die Inhalte der Kapitel?"}`
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=160Z antwort=1034Z wissen=0
  - Protokoll: assist.plan referate-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: fertig ✓ Behalten (Notsicherung ist geschrieben – Strg+S legt die Datei an). Rückgängig geht hier oder mit Strg+Z.
- Gliederung im Dokument: 1 Einleitung | 1.1 Problemstellung | 1.2 Zielsetzung der Arbeit | 1.3 Aufbau der Arbeit | 2 Die Rolle der Bienen in der Bestäubung | 2.1 Die Bedeutung der Bestäubung für Pflanzenwachstum | 2.2 Der Einfluss von Bienen auf die Fruchtentwicklung | 2.3 Vergleich zwischen natürlicher und künstlicher Bestäubung | 3 Wirtschaftliche Bedeutung der Bienen für die Landwirtschaft | 3.1 Beitrag der Bienen zur Nahrungsmittelproduktion | 3.2 Schätzungen des wirtschaftlichen Wertes von Bienenbestäubung | 3.3 Regionale Unterschiede in der Abhängigkeit von Bienen | 4 Bedrohungen für die Bienengesundheit und Schutzmaßnahmen | 4.1 Ursachen der Bienentod- und Bienengesundheitsprobleme | 4.2 Auswirkungen auf die Landwirtschaft | 4.3 Maßnahmen zur Erhaltung und Förderung von Bienen | 5 Fazit | 5.1 Zusammenfassung der Ergebnisse | 5.2 Empfehlungen für die Praxis | 5.3 Ausblick auf zukünftige Forschung
- Rückgängig-Liste: 1 → 2, oben „KI: Erstelle für meine Hausarbeit zum Thema „Bienen …“
- Rückgängig: Dokument wie vorher = True

## Referate – Abschnitt schreiben (Beispiel mit zwei eigenen Quellen)

- Auftrag: Schreib den Abschnitt 2.2 „Mulchen“ neu: drei Sätze, wie Mulch beim Wassersparen hilft. Zitiere nur meine Quelle q-buch, wenn es passt.
- Phase nach 463 s: vorschau – So sieht es aus. Behalten? Erst bei „Ja, behalten“ kommt es ins Dokument – ein Rückgängig-Schritt nimmt es wieder heraus.
- Plan: Abschnitt schreiben – Abschnitt (Nummer wie „2.1“ oder Überschrift): s4, Fertiger Text des Abschnitts: Mulch aus organischen Materialien wie Kompost oder Holzsplitt reduziert den Wasserverlust durch Verdunstung, indem er den Boden abdeckt [@q-buch]. Zudem verbessert er die Bodenstruktur und fördert das Wasserspeicherpotential des Bodens. Langfristig kann dies den Wasserbedarf der Pflanzen um bis zu 50 % senken [@q-buch].
- KI-Antwort (roh): `{"steps":[{"action":"referat.abschnitt","args":{"abschnitt":"s4","text":"Mulch aus organischen Materialien wie Kompost oder Holzsplitt reduziert den Wasserverlust durch Verdunstung, indem er den Boden abdeckt [@q-buch]. Zudem verbessert er die Bodenstruktur und fördert das Wasserspeicherpotential des Bodens. Langfristig kann dies den Wasserbedarf der Pflanzen um bis zu 50 % senken [@q-buch]."}],"frage":""}`
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=135Z antwort=409Z wissen=0
  - Protokoll: assist.plan referate-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: fertig ✓ Behalten (Notsicherung ist geschrieben – Strg+S legt die Datei an). Rückgängig geht hier oder mit Strg+Z.
- Abschnitt „Mulchen“ im Dokument: „Mulch aus organischen Materialien wie Kompost oder Holzsplitt reduziert den Wasserverlust durch Verdunstung, indem er den Boden abdeckt [@q-buch]. Zudem verbessert er die Bodenstruktur und fördert das Wasserspeicherpotential des Bodens. Langfristig kann dies den Wasserbedarf der Pflanzen um bis zu 50 % senken [@q-buch].“
- Verweise: q-buch, q-buch · unbekannt: 0 · Quellen in der Liste: 2
- Datei referat-mit-ki-abschnitt.docx: 5265 Bytes, SHA-256 72a9051d1e3853aee55126235e33e15edfd44d46f6468c3c7c85d242523fa1d4

## Zeitleisten – Ereignisse aus Text

- Auftrag: Füge die Ereignisse aus diesem Text ein: Im Jahr 44 v. Chr. wurde Caesar ermordet. Karl der Große wurde im Jahr 800 zum Kaiser gekrönt. Der Dreißigjährige Krieg dauerte von 1618 bis 1648. Um 1450 erfand Gutenberg den Buchdruck mit beweglichen Lettern.
- Phase nach 196 s: vorschau – So sieht es aus. Behalten? Erst bei „Ja, behalten“ kommt es ins Dokument – ein Rückgängig-Schritt nimmt es wieder heraus.
- Plan: Ereignisse aus Text einfügen – Ereignisse, eine Zeile je Eintrag: „Datum | Titel | Spur | Beschreibung“ oder „Beginn – Ende | Titel …“: 44 v. Chr. | Caesar wird ermordet | t-allg
800 | Karl der Große wird zum Kaiser gekrönt | t-allg
1618 – 1648 | Der Dreißigjährige Krieg | t-allg
ca. 1450 | Gutenberg erfand den Buchdruck mit beweglichen Lettern | t-allg
- KI-Antwort (roh): `{"steps":[{"action":"zeitleiste.ereignisse","args":{"ereignisse":["44 v. Chr. | Caesar wird ermordet | t-allg","800 | Karl der Große wird zum Kaiser gekrönt | t-allg","1618 – 1648 | Der Dreißigjährige Krieg | t-allg","ca. 1450 | Gutenberg erfand den Buchdruck mit beweglichen Lettern | t-allg"]}],"frage":""}`
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=251Z antwort=308Z wissen=0
  - Protokoll: assist.plan zeitleisten-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: fertig ✓ Behalten (Notsicherung ist geschrieben – Strg+S legt die Datei an). Rückgängig geht hier oder mit Strg+Z.
- Einträge im Dokument: 44 v. Chr.: Caesar wird ermordet | 800: Karl der Große wird zum Kaiser gekrönt | 1618 – 1648: Der Dreißigjährige Krieg | ca. 1450: Gutenberg erfand den Buchdruck mit beweglichen Lettern
- Rückgängig-Liste: 0 → 1, oben „KI: Füge die Ereignisse aus diesem Text ein: Im Jahr…“
- Datei zeitleiste-mit-ki.svg: 3720 Bytes, SHA-256 82cf49edd4e9decdccf9de901a26f12c3421a4a922b004e51a6e002287d63d04
- Rückgängig: Dokument wie vorher = True

Ergebnis: bestanden.
