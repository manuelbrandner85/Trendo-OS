# T-0787 ECHT: Rezepttext → Rezept im offenen Rezeptbuch (Rezepte & Ernährung)

Datum: 2026-11-28 12:43 · KI: Ollama 127.0.0.1:11434 · Modell: qwen3:14b (Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.)
Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt. Zutaten gelesen und Nährwerte gerechnet hat der eigene Kern.

- Auftrag: Mach aus diesem Text ein Rezept: Kartoffelsuppe für 4 Personen. Man braucht 800 g Kartoffeln, 2 Karotten, 1 Zwiebel, 1 Liter Gemüsebrühe, 100 ml Sahne und 1 EL Butter. Zwiebel in Butter andünsten, Kartoffeln und Karotten würfeln und dazugeben, mit Brühe aufgießen und 20 Minuten kochen, dann pürieren und die Sahne unterrühren.
- Phase: vorschau – So sieht es aus. Behalten? Erst bei „Ja, behalten“ kommt das Rezept ins Rezeptbuch – ein Rückgängig-Schritt nimmt es wieder heraus. Die Nährwerte rechnet Trendo selbst.
- Plan (verstanden über: KI (Ollama, qwen3:14b), 21 s): Rezept ins Rezeptbuch schreiben (zutaten = eine Zutat je Zeile mit Menge und Einheit, schritte = ein Arbeitsschritt je Zeile) – Name des Gerichts: Kartoffelsuppe, Portionen: 4, Zutaten, eine je Zeile („200 g Mehl“): 800 g Kartoffeln
2 Karotten
1 Zwiebel
1 Liter Gemüsebrühe
100 ml Sahne
1 EL Butter, Arbeitsschritte, einer je Zeile: Zwiebel in Butter andünsten.
Kartoffeln und Karotten würfeln und dazugeben.
Mit Gemüsebrühe aufgießen und 20 Minuten kochen.
Suppe pürieren und Sahne unterrühren., Ein Satz zum Gericht: Eine cremige Kartoffelsuppe mit Sahne und Gemüsebrühe.
- KI-Antwort (roh): `{"steps":[{"action":"rezept.anlegen","args":{"name":"Kartoffelsuppe","portionen":4,"zutaten":"800 g Kartoffeln\n2 Karotten\n1 Zwiebel\n1 Liter Gemüsebrühe\n100 ml Sahne\n1 EL Butter","schritte":"Zwiebel in Butter andünsten.\nKartoffeln und Karotten würfeln und dazugeben.\nMit Gemüsebrühe aufgießen und 20 Minuten kochen.\nSuppe pürieren und Sahne unterrühren.","beschreibung":"Eine cremige Kartoffelsuppe mit Sahne und Gemüsebrühe."}}],"frage":""}`
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=327Z antwort=448Z wissen=0
  - Protokoll: assist.plan rezepte-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: „Kartoffelsuppe“, 4 Portionen
  - 800 g Kartoffeln → Kartoffeln, 800 g
  - 2 Karotten → Karotte (Möhre), 122 g
  - 1 Zwiebel → Zwiebel, 110 g
  - 1 l Gemüsebrühe → Hühnerbrühe, 1050 g
  - 100 ml Sahne → Schlagsahne (30 %), 101 g
  - 1 EL Butter → Butter, 14 g
  1. Zwiebel in Butter andünsten.
  2. Kartoffeln und Karotten würfeln und dazugeben.
  3. Mit Gemüsebrühe aufgießen und 20 Minuten kochen.
  4. Suppe pürieren und Sahne unterrühren.
- Nährwerte je Portion (Kern, Richtwert): 305 kcal, Eiweiß 6,9 g, Fett 12,8 g, Kohlenhydrate 42,2 g; nicht erfasst: –
- Rückgängig-Liste: 0 → 1, oben „KI: Mach aus diesem Text ein Rezept: Kartoffelsuppe …“
- Rückgängig: Rezeptbuch wie vorher = True

Ergebnis: bestanden.
