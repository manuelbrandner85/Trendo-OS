# Vorschläge: kostenlose Schnittstellen (APIs) für Trendo OS

**Auftrag (Manuel, 2026-10-25):** „Finde auch in das Betriebssystem coole Möglichkeiten über kostenlose API zu integrieren für Funktionen usw., mache Vorschläge, ich sage ja oder nein.“
**Stand:** nur Vorschläge, nichts gebaut. Jede Antwort „ja“ wird ein eigener Auftrag.

**Regeln für alle Vorschläge** (aus CLAUDE.md):
- Trendo funktioniert auch ohne sie. Offline zeigt Trendo „nicht verfügbar“ und benutzt nur, was es selbst kann.
- Nichts wird heimlich gesendet. Jede Abfrage steht im Audit-Log, persönliche Daten verlassen den PC nur, wenn der Nutzer das bei der Funktion ausdrücklich einschaltet.
- Nur kostenlose Dienste. Wenn ein Dienst einen kostenlosen Schlüssel braucht, steht das dabei; der Schlüssel liegt im Secret-Store.
- Lizenz- und Quellenhinweise werden angezeigt, wo der Dienst das verlangt (z. B. CC BY).

| Nr. | Funktion in Trendo | Dienst | Schlüssel? | Grenze / Hinweis |
|---|---|---|---|---|
| API-01 | Wetter auf der Startseite und im Kalender, 7-Tage-Vorhersage | Open-Meteo | nein | frei für nicht-kommerzielle Nutzung, Quelle nennen |
| API-02 | Unwetter- und Katastrophenwarnungen als Benachrichtigung | NINA-Warn-API des Bundes (bund.dev) | nein | nur Deutschland |
| API-03 | Feiertage und Schulferien im Kalender und in der Business-Planung | Nager.Date | nein | – |
| API-04 | Adresse suchen, Karte, Route (Maps-Studio, Business-Standort) | OpenStreetMap: Nominatim und OSRM | nein | höchstens 1 Anfrage/s, Quelle nennen; Kartenkacheln später auch offline |
| API-05 | Wechselkurse im Finanz- und Business-Studio | Frankfurter (Kurse der EZB) | nein | Tageskurse |
| API-06 | USt-IdNr. eines Kunden prüfen (Rechnungen, Business) | VIES der EU-Kommission | nein | sendet nur die Nummer |
| API-07 | Wissen nachschlagen: Kurzfassung und Fakten für Recherche und KI | Wikipedia- und Wikidata-REST | nein | CC BY-SA, Quelle nennen |
| API-08 | Synonyme im Office- und Brief-Studio | OpenThesaurus | nein | deutsch |
| API-09 | Rechtschreib- und Grammatikprüfung | LanguageTool (öffentliche API) | nein | 20 Anfragen/min, Text verlässt den PC; später als Offline-Fassung möglich |
| API-10 | Buch per ISBN finden, Titel und Cover (Book-Studio) | Open Library | nein | – |
| API-11 | Fachartikel und Studien suchen, Zitate (Science-Studio) | OpenAlex, arXiv, Crossref | nein | – |
| API-12 | Freie PBR-Materialien und HDRIs fürs 3D-Studio (zusätzlich zu Poly Haven) | ambientCG | nein | CC0 |
| API-13 | Frei lizenzierte Bilder und Töne suchen (Medien-Studio) | Wikimedia Commons, Openverse | nein | Lizenz je Datei anzeigen |
| API-14 | „Bild des Tages“ als Hintergrund | Wikimedia Commons (Bild des Tages) | nein | Lizenz je Bild |
| API-15 | Nachrichten-Überblick auf der Startseite | tagesschau-API | nein | nur Schlagzeilen und Links |
| API-16 | Passwort-Check „schon geleakt?“ | Have I Been Pwned (Passwort-Bereichsabfrage) | nein | nur die ersten 5 Zeichen des Hashs verlassen den PC |
| API-17 | Zweite KI im KI-Router, wenn die lokale KI nicht reicht | Hugging Face Inference (kostenlose Stufe) | ja, kostenlos | Text verlässt den PC; nur mit Schalter und Hinweis |
| API-18 | Tankpreise in der Nähe | Tankerkönig (Daten der MTS-K) | ja, kostenlos | nur Deutschland |
| API-19 | Bevölkerung, Löhne, Branchenzahlen für Marktanalysen (Business) | Destatis GENESIS-Online | ja, kostenlos (Registrierung) | – |
| API-20 | Veröffentlichte Projekte und Releases anzeigen (GitHub-Kachel) | GitHub REST (schon teilweise drin) | nein (60/h), mit Token mehr | Konto des Nutzers, nie automatisch |

Nicht vorgeschlagen: Übersetzungsdienste (SPEC-07 abgelehnt) und kostenpflichtige KI-Dienste.
