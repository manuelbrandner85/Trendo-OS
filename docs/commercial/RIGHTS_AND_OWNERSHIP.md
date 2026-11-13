# Rechte und Eigentum

Phase 1, nur lesend. Stand 13.11.2026. Dieses Dokument ist keine Rechtsberatung. Jeder Punkt mit „offen“ muss eine Fachperson (Anwältin, Anwalt, Commercialista) bestätigen. Gleiches Thema wie „OWNERSHIP_AND_RIGHTS“ im Auftrag, es gibt nur diese eine Datei.

**Commercial Release Gate: Kein öffentlicher Verkauf, solange ein Punkt unten auf „offen“ steht.**

| Nr. | Frage | Befund | Status |
|---|---|---|---|
| 1 | Eigentümer des Repositorys | GitHub-Konto `manuelbrandner85`, öffentlich | technisch belegt, rechtliche Inhaberschaft offen |
| 2 | Wer hat Code beigetragen? | Laut Git nur `manuelbrandner85` und ein zweiter Git-Name des Inhabers sowie 61 Commits mit dem Namen „Cursor Agent“, einem KI-Werkzeug, das unter dem Konto des Inhabers lief. Seit 23.10.2026 entsteht der Code mit Claude Code | offen |
| 3 | Urheberrecht an KI-erzeugtem Code | Großteil des Codes ist mit KI-Werkzeugen entstanden. Wie weit daran Urheberrecht besteht und wer es hält, ist je Land unterschiedlich geregelt und nicht geprüft. Die Nutzungsbedingungen der eingesetzten Werkzeuge (Anthropic, Cursor) zum Eigentum am Ergebnis sind nachzulesen | offen |
| 4 | Lizenz des Repositorys | keine Lizenzdatei. Ohne Lizenz gilt grundsätzlich „alle Rechte vorbehalten“, öffentliche Repositorys dürfen nach den GitHub-Bedingungen aber angesehen und auf GitHub geforkt werden | offen, Entscheidung nötig |
| 5 | Weitere Entwickler oder Verträge | keine Hinweise im Repository auf weitere Personen, Arbeitsverträge oder Abtretungen | offen, Bestätigung durch den Inhaber nötig |
| 6 | Fremdcode und Assets | siehe `THIRD_PARTY_LICENSES.md` und `sbom-nuget.md`. Mitgelieferte Bibliotheken sind überwiegend MIT und Apache-2.0 | teilweise geprüft |
| 7 | Produktname „Trendo OS“ | keine Markenrecherche durchgeführt. Ähnliche Namen (z. B. „Trendo“) können in EU- oder nationalen Registern geschützt sein | offen |
| 8 | Logo „Gold-Orbit“ | laut `CLAUDE.md` KI-generierte Vorlage, ausgewählt am 27.10.2026. Ob daran Schutzrechte entstehen und ob das Bildmodell kommerzielle Nutzung erlaubt, ist nicht geprüft | offen |
| 9 | Hintergrundbild und weitere KI-Bilder | laut `THIRD_PARTY.md` KI-generiert | offen, wie Punkt 8 |
| 10 | Weitergabe der Abhängigkeiten | Mitgelieferte Bibliotheken erlauben kommerzielle Weitergabe mit Lizenzhinweis. Programme unter GPL (Blender, Git, mpv) werden nicht mitgeliefert, sondern beim Nutzer über den Hersteller installiert | technisch geprüft, Hinweise im Produkt prüfen |
| 11 | KI-Modell Qwen2.5 3B | in `THIRD_PARTY.md` mit „Qwen Research License, nicht-kommerziell“ geführt. Darf in einer verkauften Fassung nicht angeboten werden, solange keine kommerzielle Lizenz vorliegt | Blocker für dieses Modell |
| 12 | Claude in einer verkauften Software | jeder Kunde nutzt sein eigenes Konto, Trendo speichert keine Tokens. Die Bedingungen von Anthropic sind vor dem Verkauf erneut zu lesen | siehe `AI_PROVIDER_COMPLIANCE.md` |
| 13 | Rechtlicher Verkäufer | kein Unternehmen, keine Partita IVA oder Steuernummer im Repository genannt. Bei einem Merchant of Record verkauft dieser, der Anbieter braucht trotzdem einen eigenen steuerlichen Status | offen |

## Was der Inhaber selbst klären muss

- Bestätigen, dass niemand sonst Rechte am Code, an Bildern oder am Namen hat.
- Markenrecherche für „Trendo OS“ und, falls gewünscht, Anmeldung.
- Unternehmensform und Steuerstatus im Sitzland (Italien vorgesehen) mit einem Commercialista festlegen.
- Lizenzmodell für den Quelltext wählen (siehe Masterplan, Entscheidung E1).
