# Programmieren lernen – Testergebnisse

Studio: Programmieren lernen (STUDIO-CODELEARN-01) · Bereich: Wissen & Lernen · Seite: `programmieren-lernen-programm` · Stand: 0.9.113 (Stand 1978f1a)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 14 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| App-Profil: Deinstallation entfernt das Profil (hier ein eigenes Test-Profil); eine Prüf-Instanz lässt es stehen | ✓ bestanden |
| H1: Kinderprofil – „Programmieren lernen“ geht auch mit „Claude ohne Klicks“ nie an Claude (Route Dokument, Studio-Leiste lehnt ab) | ✓ bestanden |
| H1: „Claude ohne Klicks“ und Claude angemeldet – Nachhilfe bleibt lokal (Auftragszeile, Menüband-Weg, Studio-Leiste): 0 Claude-Aufrufe; Gegenprobe: ein anderes Programm geht an Claude | ✓ bestanden |
| M2: Programmausgabe und Testmeldungen gehen weder über die KI-Kette noch über „Hinweis von der KI“ an eine nicht lokale KI; Hinweis in der App nennt Grenzen und App-Profil | ✓ bestanden |
| M6: die KI-Kette der Nachhilfe sieht die Frage, aber keine Antwort, keinen Lösungsweg und keinen Hinweis | ✓ bestanden |
| N4: eine „lokale“ Quelle mit entfernter Adresse wird in der Nachhilfe nie gefragt | ✓ bestanden |
| T-0782 ECHT: lokale KI (Ollama) – Nachhilfe „Übung erzeugen“ und Programmieren „Hinweis geben“ im offenen Dokument, Rückgängig; Code-Tests echt (nur mit TRENDO_T0782_ECHT) | übersprungen |
| T-0782 Nachhilfe KI-Fachaktion: Übung mit Link oder ohne Antwort wird in der Vorschau abgelehnt; Fortschritt ist für die KI gesperrt | ✓ bestanden |
| T-0782 Nachhilfe KI-Fachaktion: „Gib mir eine Übung …“ → Plan uebung.erzeugen → Vorschau ändert nichts → Ja → Übung im Dokument, EIN Rückgängig-Schritt; nur die KI auf diesem PC (Claude nie gefragt) | ✓ bestanden |
| T-0782 Nachhilfe Klick bis Datei: leere Vorlage → erster Schritt → falsch → Hinweis → Lösungsweg erst nach Versuch → richtig → Fortschritt; Arbeitsblatt als ODT und DOCX | ✓ bestanden |
| T-0782 Nachhilfe „KI fragen“: verrät die lokale KI die Lösung, wird die Antwort zurückgehalten; Links werden entfernt; Frage ohne Lösung | ✓ bestanden |
| T-0782 Nachhilfe: CSV aus dem Explorer ablegen → Übungen im Dokument (ein Schritt); Rechtsklick „Dieses Thema üben“; Kinderprofil kann sokratisch nicht ausschalten | ✓ bestanden |
| T-0782 Programmieren lernen ECHT: Endlosschleife → Zeitgrenze, Syntaxfehler → verständliche Meldung; .py ablegen ersetzt den Code (ein Schritt) | ✓ bestanden |
| T-0782 Programmieren lernen ECHT: Klick → Startcode fällt durch → Code getippt → alle Tests grün (AppContainer) → nächste Aufgabe frei; Export .py läuft in fremdem Python | ✓ bestanden |
| T-0782 Programmieren lernen KI-Fachaktion: hinweis.geben → Vorschau → Ja → Hinweis bei der Aufgabe, EIN Rückgängig-Schritt; fertiger Code als „Hinweis“ wird abgelehnt; die KI bewertet nicht | ✓ bestanden |
