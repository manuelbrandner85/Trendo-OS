# Barrierefreiheit genau prüfen mit axe-core – 08.11.2026

Alle Studios, Runde 7, CORE-07 „Webseite bauen“.

| Baustein | Fassung | Lizenz | Herkunft |
|---|---|---|---|
| axe-core (Deque) | 4.14.0, seit 05.10.2026 | MPL-2.0, unverändert genutzt | offizielle npm-Quelle |
| jsdom | 30.1.3, seit 15.10.2026 | MIT | offizielle npm-Quelle |

Bewusst *nicht* gewählt: jsdom 31.0.0. Diese Fassung war am Prüftag erst drei Tage alt.

Beides wird **erst nach einem Klick** per npm in den Werkzeugordner geladen: feste Fassungen, ohne Installationsskripte, mit Eintrag im Audit-Log. Im Setup ist nichts davon enthalten.

## Was die Nutzerin oder der Nutzer sieht
„Webseite bauen“ → Qualität. Dort gibt es den neuen Knopf **„Barrierefreiheit genau prüfen (axe-core)“**. Fehlt die Einrichtung, steht dort stattdessen „… einrichten“.

Der Knopf prüft alle HTML-Seiten des Projekts. Die Befunde erscheinen auf Deutsch:
- ✗ steht für einen schweren Fehler, • für einen Hinweis.
- Bei jedem Befund stehen Anzahl und Fundstelle.
- 28 häufige Regeln sind übersetzt, etwa Bild ohne Alternativtext, fehlende Sprache, Link oder Knopf ohne Text, Eingabefeld ohne Beschriftung, Überschriftenfolge, Bereiche und gesperrtes Zoomen.
- Alle anderen Regeln behalten den axe-Text und dazu ihre Kennung.

## Sicherheit
- axe-core läuft in **jsdom** in einem eigenen Node-Prozess. Das Startskript `trendo-axe.js` ist Trendos eigener Code.
- `runScripts: 'outside-only'`: Nur axe läuft, **die Skripte der geprüften Seite laufen nicht**.
- Die Seite hat keine Adresse und es gibt keine Ressourcen-Einstellung. Damit lädt jsdom nichts nach, und **nichts geht ins Netz**.
- Grenze: Den **Farbkontrast** kann jsdom ohne Layout nicht prüfen. Diese Regel ist abgeschaltet; den Kontrast prüft Trendos eigene Qualitätsprüfung.

## Tests
- **`AxeCheckTests`** (4 Tests ohne Fremdcode, grün):
  - Antwort in echter Form lesen
  - Übergabe der Seiten (ohne `.vorschau`)
  - Eigenschaften des Startskripts (Sicherheit)
  - verständliche Meldungen bei fehlender Einrichtung, Fehlern und kaputter Antwort
- **`WebAxe_Ui`** (1 Oberflächentest, grün): Ohne axe erscheint der Einrichten-Knopf. Mit (nachgebildetem) axe erscheinen die deutschen Befunde samt Version.
- **Echtlauf `AxeCheckTests.Real_run`:**
  - Prüfseite mit bekannten Fehlern: Bild ohne Alt-Text, keine Sprache, kein Titel, leerer Link, leerer Knopf, Feld ohne Beschriftung, h1 → h3.
  - Auf der Seite liegt zusätzlich ein Skript, das den Alt-Text und die Sprache „reparieren“ würde. Findet axe beides trotzdem, ist bewiesen, dass Seiten-Skripte nicht laufen.
  - Eine saubere Gegenseite darf keine Fehler haben.
  - **NOT RUN:** Das Ausführen des heruntergeladenen Codes braucht die Freigabe des Nutzers. Ohne Freigabe wird der Test automatisch übersprungen. Der heruntergeladene Ordner liegt geparkt im Prüfordner, damit kein Testlauf ihn ungefragt startet.

## Nicht geprüft (NOT RUN)
- Der echte Lauf (siehe oben), bis zur Freigabe.
- Das Einrichten über den Knopf. Der Download selbst ist geprüft: 37 Pakete, Fassungen wie gepinnt.
