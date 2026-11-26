# Trendo Text – Testergebnisse

Studio: Texte & Tabellen (SYS-10) · Bereich: Arbeiten & Büro · Seite: `text` · Stand: 0.9.112 (Stand d6f9fcd)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 102 · Fehlgeschlagen 0 · Übersprungen 2 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| 108-49: KI-Vorschlag mit Änderungen – erst „Übernehmen“ ersetzt, „Verwerfen“ lässt alles, geänderter Text wird nicht überschrieben | ✓ bestanden |
| 3D: Text wird zu Flächen, OBJ/STL/GLB kommen wieder herein | ✓ bestanden |
| Annehmen/Verwerfen im Programm: Claude ändert das Studio-Dokument – Vorher/Nachher vergleichen, verwerfen stellt alles her, behalten übernimmt | ✓ bestanden |
| Befehlsleiste Strg+K und Projekt-Fortsetzung überall: Claude fragen, weitermachen – auch aus einem anderen Studio | ✓ bestanden |
| C1/C2/C4: Ist Claude verbunden, geht „Los“ der Studio-Zeile an Claude (Studio bleibt sichtbar, volle Rechte mit Rückfrage bei Wirkung nach außen); der zweite Auftrag läuft in derselben Sitzung; ohne Claude plant Trendo selbst | ✓ bestanden |
| CORE-05: Claude baut ein 3D-Modell – sieht Szene, Formen und Materialien, Plan zeigt das Modell mit Gesamtmaß, erst „Ausführen“ baut Teile, Licht und Kamera; ungültiges Material fällt weg; Rückgängig stellt zurück | ✓ bestanden |
| CORE-06: Claude baut Level – sieht alle Level voll, Plan zeigt das neue Level als Vorschau, erst „Ausführen“ legt Level 2 und den Hintergrund an, falsches Zeichen fällt weg, „Verwerfen“ stellt alles zurück | ✓ bestanden |
| Claude auch in der Studio-Übersicht, in der Suche und im Fokusmodus | ✓ bestanden |
| Claude im Menüband jedes Fachprogramms und Rechtsklick „Claude fragen“ auf Elemente der Seite | ✓ bestanden |
| Claude im Programm: Studio ohne eigenes Programm (Webseiten) bleibt auf seiner Seite, kein Dokument | ✓ bestanden |
| Claude im Programm: neues Projekt im Spiele-Studio legt ein Dokument in „Spiele bauen“ an, zeigt das Programm, Claude arbeitet darin; Fortsetzen öffnet es wieder | ✓ bestanden |
| Claude im Studio: Auswahl in Trendo Tabelle geht als Zustand mit; Internet und Befehle bei Bedarf immer an, je Projekt auf „sicher“ stellbar; Vorgabe abschaltbar | ✓ bestanden |
| Claude im Studio: Ergebnis erscheint links im passenden Programm – Webseite im Webseiten-Studio, Spiel im Spiele-Studio; Studio wird daneben eingeblendet | ✓ bestanden |
| Claude im Studio: Startideen, Rückfrage als Knöpfe, Anhang landet im Projekt, Abo-Verbrauch je Antwort | ✓ bestanden |
| Claude im Studio: Trendo prüft das Ergebnis und gibt Fehler automatisch an Claude zurück, bis es läuft – höchstens drei Durchgänge | ✓ bestanden |
| Claude kennt das Programm: offene Seite mit Feldern und Knöpfen geht mit; Claude bedient es erst nach „Ausführen“ – unbekannte Ziele fallen weg | ✓ bestanden |
| Claude live: Webseite erscheint in der Vorschau, beim Ändern vorher/nachher – Behalten oder Zurücknehmen; Lieferformat index.html | ✓ bestanden |
| Claude live: neuer Text öffnet gleich in Trendo Text – aber nie über einen ungespeicherten Text | ✓ bestanden |
| Claude live: offenes Studio-Dokument (.tdoc) – Claude bearbeitet es, das Studio übernimmt die Änderung sofort, Rückgängig im Studio | ✓ bestanden |
| Claude schlägt vor: Vorschläge als Knöpfe, Klick schickt sie ab, auch in der Befehlsleiste; abschaltbar | ✓ bestanden |
| Claude wie im Original: Skills, Plugins, Connectoren und Agenten aus dem eigenen Claude sichtbar; genutzter Skill/Connector unter der Antwort | ✓ bestanden |
| Claude zeigt, wo es arbeitet; Felder in Aufklapp-Menüs kann Claude lesen und ausfüllen | ✓ bestanden |
| Claude-Abo aufgebraucht: Ersatz-KI übernimmt dieselbe Nachricht (sichtbar „Aktive KI“), bis zur Freigabe ohne Claude, danach automatisch wieder Claude | ✓ bestanden |
| Claude-Chat: ohne Claude Code „einrichten“, ohne Anmeldung „Mit Claude anmelden“ – im Testmodus startet nie ein echtes Claude | ✓ bestanden |
| Claude-Chat: wie in Claude – erste Nachricht legt das Projekt an, Antwort mit Arbeitsschritten, Fortsetzen über die Sitzung, Zurücknehmen, Löschen in den Papierkorb | ✓ bestanden |
| Claude: während der Arbeit weiterschreiben – Nachricht wartet sichtbar und kommt gleich danach dran; „Stopp & jetzt“ nimmt sie sofort | ✓ bestanden |
| Echt testen bis es läuft: Skriptfehler gehen zurück an Claude; ohne Fehler sieht Claude das Bildschirmfoto an; abschaltbar | ✓ bestanden |
| Etappen: Abo mitten im Auftrag leer – Ersatz-KI übernimmt, Trendo merkt sich das Projekt; ist Claude wieder frei, macht Claude dort weiter | ✓ bestanden |
| Freie Bilder & Klänge Profi: ähnliche Bilder, Merken in neue Liste, Merklisten-Reiter mit Namensnennungen, Schriftvorschau mit eigenem Text; Claude sucht, prüft Lizenzen, merkt und schreibt die Namensnennung | ✓ bestanden |
| Freihändig sprechen: zuhören, abschicken, Antwort vorlesen, bis man es ausschaltet | ✓ bestanden |
| Genauere Leser (Kalender, Programm-Dokument), Design „Wie Windows“, Hilfe „?“ auf Seiten ohne eigenen Hilfetext, Hinweis statt stummer Knöpfe | ✓ bestanden |
| Gutachter: zweite Meinung mit Note, ändert nichts; „Verbessern lassen“ gibt die Punkte an Claude | ✓ bestanden |
| JSON: alle Eigenschaften überstehen Speichern und Laden; kaputte Werte fallen auf sichere Standards | ✓ bestanden |
| Kostenlose KI-Werkzeuge: Claude bestellt ein Bild – erst als Entwurf zum Ansehen, ins Projekt erst nach „Übernehmen“; neu erzeugen, verwerfen; Knöpfe; Diktieren | ✓ bestanden |
| Live ohne Schreibwerkzeug (z. B. per Befehl): Trendo sieht die Änderung im Projektordner und zeigt sie noch während Claude arbeitet – auch in Trendo Text | ✓ bestanden |
| Markieren & Claude fragen: Zellbereich in Trendo Tabelle geht mit ins Eingabefeld; Knopf auf der Seite; Strg+Umschalt+A | ✓ bestanden |
| Not-Stopp: Bricht laufende Claude-Arbeit ab und lässt keinen neuen Durchgang starten | ✓ bestanden |
| Pilot Text Leistung: 300 formatierte Absätze (ca. 15 Seiten) – Seite aufbauen unter 3 s, Formatwechsel unter 1,5 s | ✓ bestanden |
| Pilot Text UI: Rechtsklick im Text zeigt Ausschneiden/Kopieren/Einfügen, Formate, Farbe, Absatzart, Link, Kommentar und „Claude fragen“ | ✓ bestanden |
| Pilot Text UI: Schriftart, Größe, Farbe und Hervorhebung über das Menüband, Zeilenabstand und Erstzeileneinzug, alles rückgängig machbar | ✓ bestanden |
| Projekt-Gedächtnis: Claude merkt sich „Merken:“-Zeilen, sie gehen bei jeder Nachricht mit; ansehen, ergänzen, vergessen | ✓ bestanden |
| SPEC-15: Claude als Regie – Briefing mit Szenen, Ton und Ton-Wache, Plan zeigt neue Szenen, „Ausführen“ legt Szenen mit Bauchbinde an und setzt den Sendeplan; ungültige Quelle fällt weg | ✓ bestanden |
| STUDIO-MUSIK-01: Claude als Produzent – Briefing mit Silben und Reimen, Plan mit Rhythmus vorher/nachher, Ausführen füllt den Entwurf, Erstellen hängt die Fassung ans Original, Mastering-Ziel „laut“ | ✓ bestanden |
| SYS-02/SYS-10: Dateiregal filtert nach Art, neueste zuerst, ohne Office-Sperrdateien | ✓ bestanden |
| SYS-10 (T-0244 getrennt): Metadaten aus einem echten LibreOffice-PDF entfernt | übersprungen |
| SYS-10 (UI): Tabs; Aufgabe anlegen/erledigen; Tabelle anzeigen; Text zählen; Drucken nur nach Bestätigung | ✓ bestanden |
| SYS-10 F1/U1/U2: Start mit Programmkacheln und Vorlagen; Dokumente als Kacheln mit Filter; Öffnen im passenden Programm | ✓ bestanden |
| SYS-10 wie Microsoft 365: Dokumente suchen (alle Wörter im Namen), Anheften mit zeigt sie oben unter „Angeheftet“, Lösen nimmt sie weg; gelöschte Datei verschwindet | ✓ bestanden |
| SYS-10-P01 (T-0244 getrennt): LibreOffice öffnet ein von Trendo erzeugtes DOCX | ✓ bestanden |
| SYS-10-P02/P11: Neues DOCX/XLSX; Tabelle ohne Office lesen (XLSX + CSV mit ; und Anführungszeichen) | ✓ bestanden |
| SYS-10-P04: Zuletzt verwendet – neueste zuerst | ✓ bestanden |
| SYS-10-P05/P06: Seitenzahl und A4 eines echten LibreOffice-PDFs | ✓ bestanden |
| SYS-10-P07: Text aus einem echten PDF | ✓ bestanden |
| SYS-10-P08 (T-0244 getrennt): Scan → durchsuchbares PDF mit Tesseract | übersprungen |
| SYS-10-P10: Serienbrief – N Zeilen = N Dateien, Platzhalter auch über Formatgrenzen | ✓ bestanden |
| SYS-10-P12/P21: Protokoll als DOCX + Aufgaben als .ics (VTODO mit Fälligkeit und Erinnerung) | ✓ bestanden |
| SYS-10-P13: Rechtschreibung mit Hunspell-Wörterbuch (Suffixe, Großschreibung, einfache Komposita) | ✓ bestanden |
| SYS-10-P14/P15: Wörter, Sätze, Lesbarkeit; Vergleich markiert Änderungen | ✓ bestanden |
| SYS-10-P16: Metadaten aus DOCX und PDF entfernen (PDF bleibt gültig) | ✓ bestanden |
| SYS-10-P17 (T-0244 getrennt): PDF zusammenfügen = Summe der Seiten, Teilen stimmt | ✓ bestanden |
| SYS-10-P19/P24/P09: Versionen vor dem Überschreiben, Export-Paket mit stimmender SHA-Liste, Vorlagen mit Version | ✓ bestanden |
| SYS-10-P20: Suche in Dokumenten mit Fundstelle (DOCX, TXT, PDF) | ✓ bestanden |
| SYS-10-P22: Unterschrift als Bild in DOCX (Kopie, mit Hinweis „nicht rechtsverbindlich“) | ✓ bestanden |
| SYS-10-P23: PDF-Formular lesen und ausfüllen (Kopie, inkrementell, Original unverändert) | ✓ bestanden |
| SYS-10-P25: Barrierefreiheits-Bericht listet Mängel | ✓ bestanden |
| Schreibhilfe UI: Ja korrigiert (Strg+Z-fähig), Nein bleibt gemerkt, langer Satz wird markiert | ✓ bestanden |
| Schöne Antworten: Überschrift, Tabelle als Raster, Code mit Farben und „Kopieren“; Glas-Design im Chat | ✓ bestanden |
| Selbstlernend in allen Studios: Claudes „Lernregel“ wird Vorschlag; nach Ja gilt sie für alle Projekte des Studios; Nein und Zurücknehmen; Gutachten lehrt mit | ✓ bestanden |
| Spiele-Studio: eingebaute Spielregeln; mit GAMEFORGE dessen Regeln, Skills und Agenten (im sicheren Modus als Projekt-Skills); Regeln-Feld zeigt den Stand | ✓ bestanden |
| T-0223 T1–T4 UI: tippen, Enter teilt, Rücktaste verbindet, Fett per Knopf und Strg+B, Absatzart, Liste, Ausrichtung, Rückgängig | ✓ bestanden |
| T-0223 T5/T6/T7/T9/T10 UI: Tabelle und Seitenumbruch einfügen, Suchen & Ersetzen, Speichern und Öffnen, Als PDF, Vorlage | ✓ bestanden |
| T-0223 T8 UI: Rechtschreibung ohne Wörterbuch sagt, woher es kommt; Seite gehört zu „Texte & Tabellen“ | ✓ bestanden |
| T-0230 W1/W2/W5: Formatvorlagen-Galerie, Format übertragen, Link/Kommentar/Fußnote über das Eingabefeld, Verzeichnis, Navigation | ✓ bestanden |
| T-0230 W3/W4: Design, Wasserzeichen, Ränder, Querformat, Spalten, Einzug, Seitenzahlen – im Blatt sichtbar und im Dokument gespeichert | ✓ bestanden |
| T-0230 W6/W7: Serienbrief aus Tabelle mit Feldern und Etiketten; Vergleichen, annehmen; Barrierefreiheit und Kommentare in der Seitenleiste | ✓ bestanden |
| T-0254: Text und Tabelle – ungespeichert gesichert, nach Neustart „Wiederherstellen“; „Verwerfen“ löscht; Speichern räumt die Sicherung weg | ✓ bestanden |
| T-0682 Text: fremde DOCX – Hinweisleiste nennt, was fehlt; Strg+S und „Speichern“ lassen das Original bytegleich; „Als Kopie speichern“ schreibt „Name (Trendo).docx“ | ✓ bestanden |
| T-0682 Text: „Trotzdem überschreiben“ fragt erst nach; nach „Ja“ liegt das Original bytegleich als „Name.original.docx“ daneben – einmalig | ✓ bestanden |
| T-0682: ODT wie DOCX geschützt; ODS und ODP kann Trendo nicht zurückschreiben – nur „Als Kopie speichern“ (XLSX/PPTX), Strg+S lässt das Original; ältere Sicherung wird nicht überschrieben | ✓ bestanden |
| T-0692 UI: Maus – Klick setzt den Cursor, Umschalt+Klick markiert über Absatzgrenzen, Doppelklick markiert ein Wort, Dreifachklick den Absatz, Ziehen markiert | ✓ bestanden |
| T-0692 UI: Tabelle im Text – in die Zelle klicken und tippen, Tab springt zur nächsten Zelle, fett in der Zelle, „Verbinden →“ verbindet, Speichern als DOCX und ODT behält Format und Verbindung | ✓ bestanden |
| T-0692 UI: echte Seiten – 3 Seiten am Bildschirm wie im PDF, Status „Seite x von y“ folgt dem Cursor, Strg+Ende springt auf die letzte Seite, Strg+Enter macht einen Seitenumbruch | ✓ bestanden |
| T-0692 UI: leere Seite zeigt „Hier schreiben …“, Tippen über die Tastatur, Enter teilt, Umschalt+Pfeil markiert über drei Absätze, Strg+B setzt alle fett, Weitertippen bleibt fett, Strg+Z | ✓ bestanden |
| T-0692 V-20: „Einfach“ zeigt eine Werkzeugzeile mit den wichtigsten Formaten und genau einer goldenen Hauptaktion; alles Weitere liegt unter „Mehr …“ und geht nicht verloren | ✓ bestanden |
| T-0707: Programme außerhalb der Hülle (Text, Tabelle, Geld …) – einheitlich „Noch nicht im Dokument möglich“, Liste aus dem Katalog | ✓ bestanden |
| T-0710: Programme außerhalb der Hülle haben die Leiste – Trendo Text: Alt+D, Pfeil runter bis „Speichern“, Enter schreibt die DOCX-Datei | ✓ bestanden |
| T-0714 Text und Präsentation: 100 Rückgängig-Schritte wie die Hülle (vorher 80) | ✓ bestanden |
| T-0714 Text: über die Tastatur getippt, Fenster ohne Speichern geschlossen → Karte im neuen Fenster → Wiederherstellen bringt den Text; Speichern räumt weg | ✓ bestanden |
| T-0714 Umzug: Sicherungen der früheren Notsicherung (wiederherstellen/text.docx, cad.tcad, tabelle.xlsx) werden beim ersten Start angeboten, mit Zeitpunkt; Zurückholen und Verwerfen gehen | ✓ bestanden |
| T-0730 Text: Sicherung mit kaputtem Base64 – Zurückholen scheitert, Sicherung bleibt erhalten und angeboten | ✓ bestanden |
| T-0730 Text: beschädigte DOCX in der Sicherung – Sicherung bleibt erhalten und angeboten | ✓ bestanden |
| T-0730 Text: lesbare Sicherung wird nach der Prüfung wie bisher übernommen | ✓ bestanden |
| T-0730 Umzug: liegengebliebene „text.docx.uebernahme-<PID>“ eines beendeten Prozesses wird wieder angeboten | ✓ bestanden |
| T-0747-fix 6: Windows-1252 (€), Sonderzeichen nur in fremden DXF, kaputter SVG-Pfad mit verständlicher Meldung | ✓ bestanden |
| T-0776 echt: Texte (SYS-10) – Trendo schreibt DOCX und ODT, LibreOffice 26 öffnet beide und liefert Überschrift, Absätze, Liste und Tabelle als Text zurück | ✓ bestanden |
| Text F3: 1.-Liste, Enter und Tab rückt ein (a), Tab noch einmal (i.), Umschalt+Tab zurück; ⇤/⇥-Knöpfe; Rückgängig; Tab ohne Liste ändert nichts | ✓ bestanden |
| Text F3: „Formatierung löschen“ – erst nur das Markierte, ohne Markierung der ganze Absatz (Absatzart, Ausrichtung zurück) | ✓ bestanden |
| Unterlagen: nur Freigegebenes geht an Claude (als Datei im Projekt); passende Unterlagen werden vorgeschlagen – erst nach Ja | ✓ bestanden |
| Verbindungen: GitHub-Anmeldung im eigenen gh-Ordner der Person (sichtbar, im Browser), Netlify-Schlüssel nur im Tresor, Claude-Plugins nur nach Ja geändert | ✓ bestanden |
| Veröffentlichen: erst nach Ja; Projekt als ZIP im Ordner „Veröffentlicht“, ohne Hilfsdateien – hochgeladen wird nichts | ✓ bestanden |
| Wiederholungen automatisieren: Auftrag als Ablauf merken (Prompt-Bibliothek), mit einem Klick wiederholen; Wiederholung wird erkannt | ✓ bestanden |
| Zeitleiste mit Dauer und Tönen: fertig, Rückfrage, abschaltbar | ✓ bestanden |
| Zeitreise: jede Antwort mit Änderungen wird ein Versionspunkt; zurück auf einen älteren Stand und wieder vor | ✓ bestanden |
| Ziehen in Claude: Text kommt als Zitat ins Eingabefeld, Dateien werden angehängt | ✓ bestanden |
