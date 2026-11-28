# T-0788 ECHT: KI schreibt Personen in den offenen Stammbaum

Datum: 2026-11-28 13:04 · KI: Ollama 127.0.0.1:11434 · Modell: qwen3:14b (Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.)
Wartezeit der Kette auf die erste KI-Antwort für diesen Lauf: 8 min (Standard 90 s) – der Grafikspeicher ist mit anderen Programmen geteilt.
Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt. Namen frei erfunden (neutral).

- Auftrag: Füge die Personen aus diesem Text ein: Erika Muster wurde am 15.08.1952 in Neustadt geboren. Ihre Eltern waren Hans Muster, geboren 1921 in Musterstadt und gestorben 1999 in Neustadt, und Marie Sommer, geboren um 1925 in Talheim. Hans Muster und Marie Sommer heirateten 1950 in Neustadt.
- Phase nach 21 s: vorschau – So sieht es aus. Behalten? Erst bei „Ja, behalten“ kommt es in den Stammbaum – ein Rückgängig-Schritt nimmt es wieder heraus.
- Plan: Personen aus Text einfügen – Eine Zeile je Person: „Name | Geschlecht (m/w) | Geburt | Geburtsort | Tod | Sterbeort | Vater | Mutter“, Ehen als „Ehe: Name + Name | Datum | Ort“: Erika Muster |  | 15.08.1952 | Neustadt |  |  | Hans Muster | Marie Sommer
Hans Muster |  | 1921 | Musterstadt | 1999 | Neustadt |  |
Marie Sommer |  | um 1925 | Talheim |  |  |  |
- KI-Antwort (roh): `{"steps":[{"action":"stammbaum.personen","args":{"personen":["Erika Muster |  | 15.08.1952 | Neustadt |  |  | Hans Muster | Marie Sommer","Hans Muster |  | 1921 | Musterstadt | 1999 | Neustadt |  | ","Marie Sommer |  | um 1925 | Talheim |  |  |  | "]}}],"frage":""}`
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=287Z antwort=265Z wissen=0
  - Protokoll: assist.plan stammbaum-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: fertig ✓ Behalten (Notsicherung ist geschrieben – Strg+S legt die Datei an). Rückgängig geht hier oder mit Strg+Z.
- Personen im Dokument: Erika Muster (U) * 15. August 1952 Neustadt † - | Hans Muster (M) * 1921 Musterstadt † 1999 | Marie Sommer (F) * um 1925 Talheim † -
- Familien: Hans Muster + Marie Sommer, Kinder: Erika Muster, Heirat  
- Prüfung: 0 Fehler, 0 Hinweise 
- Rückgängig-Liste: 0 → 1, oben „KI: Füge die Personen aus diesem Text ein: Erika Mus…“
- Datei stammbaum-mit-ki.ged: 589 Bytes, SHA-256 607b3f932c849db1fb4dd69b1e9b463c3415b1f9e7089d0f8ab7607cac706676
- Rückgängig: Dokument wie vorher = True

Ergebnis: bestanden.
