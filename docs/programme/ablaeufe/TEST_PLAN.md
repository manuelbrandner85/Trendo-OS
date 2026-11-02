# Abläufe (Automatisch erledigen) – Testplan

Studio: Aufgaben automatisieren (SYS-05) · Bereich: KI & Automatik · Seite: `ablaeufe` · Stand: 0.9.34 (Stand 695d2ea)

18 Tests sprechen dieses Programm an.

- Ablauf-Editor: Probelauf mit Anzeige – jeder Baustein leuchtet vorher auf, danach ✓ mit Anzahl bzw. ✕ bei Fehler, Ergebnis am Ende; nichts wird geändert
- Ablaufplan: Bausteine anhängen, Schritt per Maus nach oben ziehen, einstellen, Probelauf, Rückgängig; Änderung an freigegebenem Ablauf verlangt neue Freigabe
- CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet
- CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt
- CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen
- CORE-03 UI: Rolle wählen, starten, Team-Ablauf, Speichern über Freigabe
- CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch
- CORE-12 UI: Vorlage ausfüllen und senden, neue Vorlage speichern, Masterprompt bauen
- SYS-05 UI: Vorlage anlegen, Probelauf, Freigeben, Ausführen, eigener Ablauf
- SYS-05: Auslöser täglich, Intervall und neue Datei; KI-Zusammenfassung
- SYS-05: Nur erlaubte Ordner, Textdatei mit {datum}, ZIP, Datum voranstellen
- SYS-05: Probelauf ändert nichts, Verschieben braucht Freigabe, Änderung verlangt neue Freigabe
- T-0201: Datei aufs Feld oder aufs Studio ziehen füllt das passende Feld
- T-0201: Mausklick auf „Datei wählen …“ füllt das Feld, die Datei steht danach unter „Zuletzt“; Eintippen geht weiter
- T-0201: Ordnerfeld öffnet den Ordner-Dialog
- T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“
- Vorschläge UI: Kalender plant fällige Aufgabe ein (Ja, Rückgängig), Aufgaben aus E-Mails (Nein bleibt gemerkt), Abläufe, Videoschnitt-Stille
- X-05 UI: Suche findet laufende Quelle, Fragen nutzt die Rückfallkette
