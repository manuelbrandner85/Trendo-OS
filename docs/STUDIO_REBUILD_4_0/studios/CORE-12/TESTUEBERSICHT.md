# Testübersicht: KI-Anweisungen schreiben

Studio CORE-12 · KI-Anweisungen schreiben · Bereich Kreativ & Medien

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

Gezählte Tests des Studios: 82. Gefordert für die Abnahme 4.0: mindestens 20 eigene positive und negative Testfälle.

## Dem Studio zugeordnete Tests

Die Zuordnung kommt aus dem Programmkatalog und ist ungenau: Sie kann Tests anderer Studios enthalten (siehe REA-Audit).

- Anweisungs-Test: falsche Regeln werden gemeldet; Ergebnis zählt Durchläufe beider Fassungen; Regeln bleiben gespeichert
- CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet
- CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt
- CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen
- CORE-03 UI: Rolle wählen, starten, Team-Ablauf, Speichern über Freigabe
- CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch
- CORE-12 UI: Vorlage ausfüllen und senden, neue Vorlage speichern, Masterprompt bauen
- CORE-12: Platzhalter mit Standardwert ausfüllen, fehlende werden genannt
- CORE-12: Qualitätsprüfung erkennt kurze Aufträge, Geheimnisse, offene Platzhalter; Masterprompt hat Regeln
- CORE-12: Versionen speichern und zurückholen, Mitgelieferte nicht löschbar
- Recherche: zeigt zuerst nur den Artikel, Knopf wechselt ohne neuen Abruf zur ganzen Seite und zurück; ohne Artikel ein Hinweis
- SYS-05 UI: Vorlage anlegen, Probelauf, Freigeben, Ausführen, eigener Ablauf
- Skills-Seite: eigener Skill zeigt Tipps der Skill-Prüfung, „Als Datei“ sichert ihn, „Aus Datei laden“ übernimmt ihn als geladenen Skill
- T-0317: Skills-Seite – ansehen, eigenen Skill anlegen und ändern (Version 2), passende Fachregeln gehen an die KI
- T-0317: Vertraute Quelle – Liste nur der SKILL.md-Dateien, Übernehmen speichert Text mit Lizenz und Herkunft (nichts wird ausgeführt)
- T-0317: „In Claude Code öffnen“ legt die passenden Skills als .claude/skills/trendo-…/SKILL.md in den Ordner und nennt die Checklisten-Prüfung
- X-05 UI: Suche findet laufende Quelle, Fragen nutzt die Rückfallkette

## Pflichtprüfungen der Abnahme 4.0

Alle Punkte offen, bis ein Testartefakt vorliegt: alte Funktionen, neue Funktionen, Bedienbarkeit, Erreichbarkeit, Dateikompatibilität, Fehlerbehandlung, Rechte, Leistung, Speicher, Stabilität, Tastatur und Maus, Ziehen und Ablegen, Rückgängig, Wiederherstellung, Exporte, Zusammenspiel mit anderen Studios.
