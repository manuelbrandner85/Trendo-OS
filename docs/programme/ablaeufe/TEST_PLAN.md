# Abläufe (Automatisch erledigen) – Testplan

Studio: Aufgaben automatisieren (SYS-05) · Bereich: KI & Automatik · Seite: `ablaeufe` · Stand: 0.9.36 (Stand df96c57)

33 Tests sprechen dieses Programm an.

- A2/A4: Texte & Tabellen mit Neu-Karten (Dokument, Tabelle, Präsentation, PDF, Brief, Word-Vorlagen) und Leerzustand; Neues Projekt mit Ziel-Karten
- Ablauf-Editor: Probelauf mit Anzeige – jeder Baustein leuchtet vorher auf, danach ✓ mit Anzahl bzw. ✕ bei Fehler, Ergebnis am Ende; nichts wird geändert
- Ablaufplan: Bausteine anhängen, Schritt per Maus nach oben ziehen, einstellen, Probelauf, Rückgängig; Änderung an freigegebenem Ablauf verlangt neue Freigabe
- Abläufe Profi: n8n einrichten nur nach Ja, starten, Vorlage als Fluss mit Nach-außen-Hinweis, Nein verwirft, Ja speichert .n8n.json; Claude-Datei erscheint; Probe-Ereignis geht an den lokalen Webhook
- Abläufe: Fehler im Zeitplan wird angezeigt und protokolliert, der nächste fehlerfreie Lauf nimmt den Hinweis wieder weg
- C1/C2/C4: Ist Claude verbunden, geht „Los“ der Studio-Zeile an Claude (Studio bleibt sichtbar, volle Rechte mit Rückfrage bei Wirkung nach außen); der zweite Auftrag läuft in derselben Sitzung; ohne Claude plant Trendo selbst
- CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet
- CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt
- CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen
- CORE-03 UI: Rolle wählen, starten, Team-Ablauf, Speichern über Freigabe
- CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch
- CORE-12 UI: Vorlage ausfüllen und senden, neue Vorlage speichern, Masterprompt bauen
- G10: KI-Aufträge der Grafikkarten-Warteschlange als Karten – läuft mit Laufzeit, wartet mit Platz; fertig = Karte weg
- G2: Befehlsleiste mit Gruppen-Überschriften und Symbolen; ausgeführter Befehl steht danach oben unter „Zuletzt benutzt“
- G3/G4: Vorschlags-Chips unter der Studio-KI-Zeile setzen den Auftrag ein (starten nichts) und verschwinden beim Tippen; Desktop „Weitermachen“ mit zuletzt benutzten Programmen
- G9: Seitenleisten in Trendo Musik mit Griff; Breite gemerkt und beim nächsten Öffnen angewandt; Doppelklick klappt ein und zur letzten Breite aus
- I2/I4: KI-Rollen als Karten (Auftrag, Schreibrecht), Wahl markiert; Ablauf-Vorlagen als Karten, ohne Ablauf Leerzustand, Vorlage legt Ablauf an
- K1: Musikspur im Videoschnitt zeigt die echte Wellenform der Tondatei (laut/leise erkennbar); ohne lesbare Datei nur Mittellinie statt erfundener Welle
- K3: Abspielleiste von Trendo Musik zeigt die echte Wellenform des gewählten Lieds; ohne Lied keine Welle
- SYS-05 UI: Vorlage anlegen, Probelauf, Freigeben, Ausführen, eigener Ablauf
- SYS-05: Auslöser täglich, Intervall und neue Datei; KI-Zusammenfassung
- SYS-05: Nur erlaubte Ordner, Textdatei mit {datum}, ZIP, Datum voranstellen
- SYS-05: Probelauf ändert nichts, Verschieben braucht Freigabe, Änderung verlangt neue Freigabe
- T-0201: Datei aufs Feld oder aufs Studio ziehen füllt das passende Feld
- T-0201: Mausklick auf „Datei wählen …“ füllt das Feld, die Datei steht danach unter „Zuletzt“; Eintippen geht weiter
- T-0201: Ordnerfeld öffnet den Ordner-Dialog
- T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“
- T4/T7: Geräte überwachen und Auto & Fahrten starten mit Leerzustand; Beispiel-Knopf darin füllt Dashboard bzw. Fahrzeug
- Vorschläge UI: Kalender plant fällige Aufgabe ein (Ja, Rückgängig), Aufgaben aus E-Mails (Nein bleibt gemerkt), Abläufe, Videoschnitt-Stille
- W3: Wissensnetz – Seiten als Punkte, [[Verweise]] als Linien (ungerichtet, ohne Doppelte, fehlende Ziele nicht), Klick öffnet die Seite; leer mit Leerzustand
- X-05 UI: Suche findet laufende Quelle, Fragen nutzt die Rückfallkette
- Y3/Y6: Sicherheitszentrale mit Schutz-Ring und Ampel-Karten je Prüfung; Programme verwalten mit App-Regal – Klick sucht, installiert nichts ohne „Installieren“
- Y4/Y7/Y11/Y14: Gefahren prüfen, Teilen, Qualität und Geräteverbund als Werkzeug-Karten mit Symbol; Geräteverbund ohne Geräte mit Leerzustand
