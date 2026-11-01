# KI-Quellen – Testplan

Studio: Kostenlose KI finden (X-05) · Bereich: KI & Automatik · Seite: `ki-quellen` · Stand: 0.9.33 (Stand 9436f3b)

28 Tests sprechen dieses Programm an.

- Beschreibung: README wird beim Aufklappen geladen; geholte Projekte lassen sich mit Rückfrage entfernen
- CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet
- CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt
- CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen
- CORE-03 UI: Rolle wählen, starten, Team-Ablauf, Speichern über Freigabe
- CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch
- CORE-12 UI: Vorlage ausfüllen und senden, neue Vorlage speichern, Masterprompt bauen
- Claude im Studio: Fenster schließen schließt Claude
- Claude im Studio: wie Claude im Web – claude.ai im ganzen Fenster, kein Terminal; anmelden, Projekte des Studios anlegen, fortsetzen, entfernen
- Claude-Regeln 01.11.2026: Claude Code aus Trendo erlaubt (eigene Anmeldung je Nutzer) – kein Sperr-Hinweis; „Claude Code öffnen“ startet das echte Claude Code im Projektordner
- Claude-Regeln 01.11.2026: Trendo speichert keine Claude-Tokens – ein alter wird beim Start gelöscht, kein Token-Feld mehr
- Einrichtung: Ergebnis bleibt stehen (mit fehlgeschlagenen Paketen), schließt erst nach „Los geht's“
- Einrichtung: „Abbrechen“ beendet die Installation und schließt den Bildschirm; danach kein Ergebnisbildschirm
- Installer-Programm: Installieren & starten führt den Installer still aus, erkennt das Programm, startet es; Entfernen ruft den Deinstaller
- KI-Fortschritt: laufende KI-Aufgabe erscheint oben mit Bezeichnung, Phase und Abbrechen; danach verschwindet sie
- KI-Programme entfernen: über KI-Quellen eingerichtetes KoboldCpp und eine KI-Laufzeit lassen sich wieder entfernen
- KI-Quellen und KI-Bibliothek: ohne laufendes Ollama ein Hinweis mit nächstem Schritt, gesperrte Knöpfe sagen warum
- KoboldCpp: Bestätigen → von GitHub laden → einrichten → Starten mit größtem Sprachmodell
- LM Studio: Installer von der Herstellerseite laden und starten, danach erkennen und Quelle einschalten
- SYS-05 UI: Vorlage anlegen, Probelauf, Freigeben, Ausführen, eigener Ablauf
- T-0316: Claude als Haupt-KI – Frage läuft eingegrenzt über Claude (kein Datei-Ändern), Antwort mit Quelle „Claude (dein Abo)“; Haupt-KI aus → Claude wird nicht gefragt
- T-0316: Claude-Karte – nicht installiert → „Claude Code einrichten“, nicht angemeldet → „Mit Claude verbinden“, verbunden → „✓ Mit deinem Claude-Abo verbunden (Max)“
- T-0316: Studio – „✳ Claude“ immer in der Auftragsleiste; das Claude-Projekt des Studios trägt seine Rolle, kein Terminal
- T-0316: Testmodus – Knöpfe „Claude Code einrichten“, „Mit Claude verbinden“, „Claude Code öffnen“ starten nie ein echtes Programm
- X-05 UI: Suche findet laufende Quelle, Fragen nutzt die Rückfallkette
- X-05: Nur freie Quellen auf diesem Gerät, Modelle passend zur Hardware
- X-05: Rückfallkette lässt sich sortieren, abschalten und bleibt gespeichert
- Zusatzprogramme: Vollpaket-Karte mit Ordnerwahl; falscher Ordner wird erklärt
