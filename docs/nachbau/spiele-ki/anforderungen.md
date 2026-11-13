# Nachbau Spiele-Studio mit KI: Anforderungen

Stand: 13.11.2026. Status: Stufe 1 (D1 bis D4, T-0610) in main; Stufe 2 (E2 bis E4, T-0640) in main seit 13.11.2026; Stufe 3 nicht begonnen.
Iststand D1 bis D4: Stand nach T-0610, belegt durch Tests, App nicht von Hand bedient. Iststand aller anderen Zeilen: geprüft gegen `origin/main` (023a6d742) per Stichwortsuche und Lesen einzelner Dateien, nicht durch Bedienen der App.

## Clean-Room-Regeln

Diese Liste ist die einzige Brücke zwischen Beobachten und Bauen. Sie beschreibt Verhalten in eigenen Worten.

- Vorbilder liefern Funktionen und Abläufe, keine Texte, Grafiken, Namen oder Prompts.
- Genex Desktop und Claude Code Game Studios stehen unter MIT. Gebaut wird trotzdem nach dieser Liste. Wird doch Code übernommen, gehört der Lizenzhinweis mit in das Produkt und in den Nachweis.
- Bezahl-Dienste dürfen nie Voraussetzung sein. Mit Claude voller Umfang über das installierte Claude Code, sonst lokale KIs.
- Bestehende Module erweitern (`GameDesign`, `GameDirector`, `GamePro`, `GamePlaytest`, `GameHarness`, `Intel.AskAsync`), kein zweiter KI-Kern.
- Trendo lädt keine Engine selbst. Fehlt eine, nennt Trendo genau einen klaren Schritt.

## Vorbilder

| Bereich | Vorbild | Grundlage | Tiefe der Beobachtung |
|---|---|---|---|
| Design | Ludo.ai | Produktseiten „Game Concept“ und „Project“, 09.11.2026 | Nur Herstellerangaben, App nicht bedient |
| Entwicklung | Genex Desktop (MIT) | README, 09.11.2026 | Nur README, App nicht gestartet, Windows-Build unsigniert |
| Arbeitsablauf | Claude Code Game Studios (MIT) | README und Workflow-Anleitung, 09.11.2026 | Dokumentation gelesen |
| Prüfung am Spiel | AutoGodot, Open-Godot-MCP | Beschreibungen, 09.11.2026 | Nur Kurzbeschreibung, Angaben unbestätigt |

## Anforderungen

Iststand: **fehlt**, **teilweise** oder **vorhanden**. Stufe gibt die vorgeschlagene Reihenfolge an.

### Spieldesign

| ID | Funktion | Erwartetes Verhalten | Prüfung | Iststand | Quelle | Stufe |
|---|---|---|---|---|---|---|
| D1 | Ideen-Generator | Aus Stichworten, Genre, Plattform, Stil und Perspektive entstehen mehrere Spielideen mit Titel und Kurzbeschreibung. Ideen lassen sich bewerten und neu würfeln, passend zu den Bewertungen. Ein Konzeptbild kommt von der lokalen Bild-KI, falls vorhanden, sonst bleibt der Platz leer mit Hinweis. | Test: Eingabe mit Genre und Stil ergibt mindestens 3 Ideen, die diese Angaben enthalten. Test: ohne Bild-KI kein Fehler, Hinweis sichtbar. | vorhanden; Konzeptbild nur auf Knopfdruck je Idee, nicht automatisch für alle | Ludo „Game Concept“ | 1 |
| D2 | Einstieg ohne Idee | Wer keine Idee hat, wird in wenigen Schritten mit Knöpfen zu einer Idee geführt (Stimmung, Spielart, Aufwand). Am Ende steht eine Idee, die in D3 übernommen werden kann. | Klickweg vom leeren Studio bis zur fertigen Idee ohne Texteingabe. | vorhanden | Ludo „Idea Pathfinder“ | 1 |
| D3 | Lebendes Design-Dokument | Die Spielbibel besteht aus Abschnitten, die sich hinzufügen, umsortieren und einzeln bearbeiten lassen. Eingebettete Bilder bleiben im Abschnitt, ungenutzte landen in einer Ablage. Frühere Stände lassen sich wiederherstellen. Export als PDF und Text. | Test: Abschnitt verschieben, speichern, neu laden, Reihenfolge bleibt. Test: Stand wiederherstellen ergibt exakt den alten Inhalt. Test: PDF enthält Text und Bilder. | vorhanden; Text-Export ist Markdown; Figuren, Welt, Missionen und Meilensteine werden in ihren Bereichen bearbeitet; Wiederherstellen nutzt die Dateiversionen und wirkt erst nach dem Speichern | Ludo „Project“ | 1 |
| D4 | Chat je Abschnitt | Zu jedem Abschnitt gibt es Vorschläge und einen Chat, der Abschnitt und Gesamtspiel kennt. Ein Vorschlag ändert erst nach Übernahme, mit Vorschau und Rückgängig. Der Verlauf bleibt erhalten. | Test: Vorschlag verändert das Dokument vor der Übernahme nicht und nur den gewählten Abschnitt danach. Test: Verlauf nach Neustart vorhanden. | vorhanden | Ludo „Project“ | 1 |
| D5 | Ähnliche Spiele | Zu einer Idee werden vergleichbare, real existierende Spiele mit Quelle genannt, als Referenz für Designentscheidungen. Lässt sich nichts belegen, steht dort „keine Referenz gefunden“, nie ein erfundener Titel. Keine fremden Inhalte oder Figuren werden übernommen. | Test: Ohne Quelle wird kein Titel angezeigt. Stichprobe: Jeder gezeigte Titel hat einen funktionierenden Beleg. | fehlt | Ludo „Game Concept“ | 3 |

### Entwicklung

| ID | Funktion | Erwartetes Verhalten | Prüfung | Iststand | Quelle | Stufe |
|---|---|---|---|---|---|---|
| E1 | Engine einrichten | Trendo erkennt Unreal, Unity und Godot, wählt passend zum Spiel, legt das Projekt an und prüft, ob es sich öffnen und bauen lässt. Fehlt etwas, nennt Trendo genau einen Schritt. Gemeldet wird nur, was wirklich geprüft wurde. | Test mit fehlender Engine: genau ein klarer Schritt, kein Download. PC-Lauf je installierter Engine mit Protokoll. | teilweise; Erkennung und Projektanlage vorhanden, Umfang je Engine noch zu prüfen | Genex README | 3 |
| E2 | Team mit Hierarchie | Leitung, Abteilungen und Spezialisten statt einer flachen Reihe. Aufgaben laufen von oben nach unten, Konflikte gehen an die gemeinsame Leitung. Bei Entscheidungen zeigt das Team 2 bis 4 Optionen und wartet auf die Wahl, bevor geschrieben wird. | Test: Rollenbaum hat drei Ebenen. Test: Ohne Wahl wird das Dokument nicht geändert. Test: Widerspruch zweier Rollen landet bei der Leitung. | vorhanden (T-0640); „ohne Wahl keine Änderung“ ist nur im Weg über die lokale KI erzwungen, bei Claude eine Anweisung | Claude Code Game Studios | 2 |
| E3 | Ablauf bis zur Abnahme | Aus dem Dokument entstehen Epics und Stories mit prüfbaren Abnahmekriterien, auf dem vorhandenen Aufgaben-Board. Eine Story wird erst fertig, wenn ihre Kriterien geprüft sind: fertig, fertig mit Hinweisen oder blockiert. Phasentore urteilen bestanden, Bedenken, nicht geprüft oder durchgefallen. Bedenken erfordern eine festgehaltene Annahme des Risikos, „durchgefallen“ rückt nie vor. | Test je Urteil, ob die Phase vorrückt. Test: Story ohne geprüfte Kriterien kann nicht fertig werden. | vorhanden (T-0640) | Claude Code Game Studios | 2 |
| E4 | Wählbare Prüfstufe | Drei Stufen: schlank (Standard, eine Seite Konzept, keine Tore), normal (Tore an Phasenübergängen), streng (alle Pflichtpunkte, Prüfung bei jedem Schritt). Die Stufe lässt sich im Projekt wechseln, ohne Daten zu verlieren. | Test: Je Stufe laufen genau die vorgesehenen Prüfungen. Test: Wechsel erhält alle Inhalte. | vorhanden (T-0640) mit Abweichung: „schlank“ behält die bisherige Abnahme nach den Phasen-Kriterien; „eine Seite Konzept“ und „Prüfung bei jedem Schritt“ in „streng“ sind nicht umgesetzt | Claude Code Game Studios | 2 |
| E5 | Prüfung per Bild | Während des Testlaufs entstehen Bilder, die automatisch auf sichtbare Fehler geprüft werden (schwarzes Bild, fehlende Texturen, Figur fehlt, Flackern). Jeder Befund nennt Bild und Messwert. Eine Bildbewertung durch KI gilt als Hinweis, nicht als Nachweis. | Test mit präparierten Fehlerbildern: Jeder Fehler wird gefunden. Test mit gutem Bild: kein Fehlalarm. | teilweise; der Bot liefert Bilder, automatische Bewertung nicht gefunden | AutoGodot-Beschreibung | 3 |
| E6 | Wiederholbare Testläufe | Ein Testlauf ist mit gleichem Startwert und gleichen Eingaben wiederholbar. Zwei Läufe lassen sich vergleichen, Verschlechterungen werden gemeldet. Gilt für Browser-Spiele und Unreal, Godot und Unity nach Prüfung. | Test: zwei Läufe mit gleichem Startwert ergeben gleiche Messwerte. Test: eingebaute Verschlechterung wird gemeldet. | teilweise; Bot mit Messzeilen für Unreal, Vergleich zwischen Läufen nicht gefunden | Open-Godot-MCP-Beschreibung | 3 |
| E7 | Einheitliche Dienste | Bild, 3D, Ton, Musik und Sprache laufen über eine gemeinsame Auswahl: lokale Werkzeuge und freie Quellen zuerst, Bezahl-Dienste nur, wenn der Nutzer sie selbst eingerichtet hat. Jedes Ergebnis landet mit Herkunft und Lizenz im Projekt. | Test: Ohne Bezahl-Dienst funktioniert die Auswahl vollständig. Test: Jedes eingefügte Asset hat einen Lizenzeintrag. | teilweise; einzelne Wege vorhanden (Poly Haven, Piper), Gesamtauswahl noch zu prüfen | Genex README | 3 |

## Reihenfolge

1. **Stufe 1, Design:** D1, D2, D3, D4. Baut auf der Spielbibel auf und braucht keine Engine.
2. **Stufe 2, Arbeitsablauf:** E2, E3, E4.
3. **Stufe 3, Prüfung und Anbindung:** E1, E5, E6, E7, D5.

## Noch offen

- Ludo.ai und Genex Desktop wurden nicht bedient. Vor dem Bau sollten die Abläufe an den Web-Oberflächen oder Handbüchern vertieft werden, soweit die Nutzungsbedingungen das erlauben.
- Die Einträge „teilweise“ und „fehlt“ beruhen auf Stichwortsuche. Vor Baubeginn je Funktion im Code und in der App nachprüfen.
- `requirements.yaml` und `decisions.yaml` sind unverändert. Eine Übernahme braucht einen freigegebenen Auftrag.
