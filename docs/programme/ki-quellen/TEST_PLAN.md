# KI-Quellen – Testplan

Studio: Kostenlose KI finden (X-05) · Bereich: KI & Automatik · Seite: `ki-quellen` · Stand: 0.9.30 (Stand 234e066)

28 Tests sprechen dieses Programm an.

- Beschreibung: README wird beim Aufklappen geladen; geholte Projekte lassen sich mit Rückfrage entfernen
- CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet
- CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt
- CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen
- CORE-03 UI: Rolle wählen, starten, Team-Ablauf, Speichern über Freigabe
- CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch
- CORE-12 UI: Vorlage ausfüllen und senden, neue Vorlage speichern, Masterprompt bauen
- Claude im Studio: Knopf in jedem Studio, Fläche rechts; nicht installiert → einrichten, nicht angemeldet → anmelden, Testmodus startet nichts
- Claude im Studio: echtes Terminal im Projektordner, Auftrag als Eingabe, Fläche bleibt beim Neuzeichnen dieselbe, Fenster schließen beendet sie
- Einrichtung: Ergebnis bleibt stehen (mit fehlgeschlagenen Paketen), schließt erst nach „Los geht's“
- Einrichtung: „Abbrechen“ beendet die Installation und schließt den Bildschirm; danach kein Ergebnisbildschirm
- Installer-Programm: Installieren & starten führt den Installer still aus, erkennt das Programm, startet es; Entfernen ruft den Deinstaller
- KI-Fortschritt: laufende KI-Aufgabe erscheint oben mit Bezeichnung, Phase und Abbrechen; danach verschwindet sie
- KI-Programme entfernen: über KI-Quellen eingerichtetes KoboldCpp und eine KI-Laufzeit lassen sich wieder entfernen
- KI-Quellen und KI-Bibliothek: ohne laufendes Ollama ein Hinweis mit nächstem Schritt, gesperrte Knöpfe sagen warum
- KoboldCpp: Bestätigen → von GitHub laden → einrichten → Starten mit größtem Sprachmodell
- LM Studio: Installer von der Herstellerseite laden und starten, danach erkennen und Quelle einschalten
- SYS-05 UI: Vorlage anlegen, Probelauf, Freigeben, Ausführen, eigener Ablauf
- T-0316: Abo-Token – API-Schlüssel wird abgelehnt (nur Abo), Abo-Token landet im sicheren Speicher und lässt sich entfernen
- T-0316: Claude als Haupt-KI – Frage läuft eingegrenzt über Claude (kein Datei-Ändern), Antwort mit Quelle „Claude (dein Abo)“; Haupt-KI aus → Claude wird nicht gefragt
- T-0316: Claude-Karte – nicht installiert → „Claude Code einrichten“, nicht angemeldet → „Mit Claude verbinden“, verbunden → „✓ Mit deinem Claude-Abo verbunden (Max)“
- T-0316: Ohne Anthropic-Freigabe – kein Abo im Hintergrund (keine Haupt-KI, Läufe gesperrt), Hinweis sichtbar; „Claude Code öffnen“ startet das echte Claude Code im Projektordner
- T-0316: Studio – „Claude Code“ immer in der Auftragsleiste; „Im eigenen Fenster“ öffnet Claude Code mit der Rolle des Studios
- T-0316: Testmodus – Knöpfe „Claude Code einrichten“, „Mit Claude verbinden“, „Token erzeugen“, „Claude Code öffnen“ starten nie ein echtes Programm
- X-05 UI: Suche findet laufende Quelle, Fragen nutzt die Rückfallkette
- X-05: Nur freie Quellen auf diesem Gerät, Modelle passend zur Hardware
- X-05: Rückfallkette lässt sich sortieren, abschalten und bleibt gespeichert
- Zusatzprogramme: Vollpaket-Karte mit Ordnerwahl; falscher Ordner wird erklärt
