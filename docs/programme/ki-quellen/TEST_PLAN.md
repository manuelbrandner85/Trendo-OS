# KI-Quellen – Testplan

Studio: Kostenlose KI finden (X-05) · Bereich: KI & Automatik · Seite: `ki-quellen` · Stand: 0.9.28 (Stand ab682fb)

20 Tests sprechen dieses Programm an.

- Beschreibung: README wird beim Aufklappen geladen; geholte Projekte lassen sich mit Rückfrage entfernen
- CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet
- CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt
- CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen
- CORE-03 UI: Rolle wählen, starten, Team-Ablauf, Speichern über Freigabe
- CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch
- CORE-12 UI: Vorlage ausfüllen und senden, neue Vorlage speichern, Masterprompt bauen
- Einrichtung: Ergebnis bleibt stehen (mit fehlgeschlagenen Paketen), schließt erst nach „Los geht's“
- Einrichtung: „Abbrechen“ beendet die Installation und schließt den Bildschirm; danach kein Ergebnisbildschirm
- Installer-Programm: Installieren & starten führt den Installer still aus, erkennt das Programm, startet es; Entfernen ruft den Deinstaller
- KI-Fortschritt: laufende KI-Aufgabe erscheint oben mit Bezeichnung, Phase und Abbrechen; danach verschwindet sie
- KI-Programme entfernen: über KI-Quellen eingerichtetes KoboldCpp und eine KI-Laufzeit lassen sich wieder entfernen
- KI-Quellen und KI-Bibliothek: ohne laufendes Ollama ein Hinweis mit nächstem Schritt, gesperrte Knöpfe sagen warum
- KoboldCpp: Bestätigen → von GitHub laden → einrichten → Starten mit größtem Sprachmodell
- LM Studio: Installer von der Herstellerseite laden und starten, danach erkennen und Quelle einschalten
- SYS-05 UI: Vorlage anlegen, Probelauf, Freigeben, Ausführen, eigener Ablauf
- X-05 UI: Suche findet laufende Quelle, Fragen nutzt die Rückfallkette
- X-05: Nur freie Quellen auf diesem Gerät, Modelle passend zur Hardware
- X-05: Rückfallkette lässt sich sortieren, abschalten und bleibt gespeichert
- Zusatzprogramme: Vollpaket-Karte mit Ordnerwahl; falscher Ordner wird erklärt
