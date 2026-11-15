# Z6 – Prüflauf nach „seite_anlegen“ (T-0662)

Datum: 15.11.2026. PC: RTX 5070 (12 GB), 32 GB Arbeitsspeicher, Ollama, Kontextfenster 8192 Token.
Je Modell zwei Läufe mit den drei festen Web-Aufgaben, mit genau einer Nachfrage bei abgelehntem Plan (wie im Studio).
Rohprotokoll: `web-modell-pruefung-2026-11-15-seite-anlegen.log`, SHA-256 `d866a0a0bbe75a0fc6c4525166685d6358b0067c24e65af5394aa4a95dc56fbc`.

| Modell | Vorher (ein Lauf) | Jetzt (zwei Läufe) | Neue Seite dauert |
|---|---|---|---|
| qwen2.5-coder:14b | 2 von 3 | **6 von 6** | 146–203 s |
| qwen3:14b | 2 von 3 | 5 von 6 | 340–386 s (denkt vorher nach) |
| qwen2.5-coder:7b | 1 von 3 | 4 von 6 | 7–25 s |

Offene Fehler: qwen3:14b einmal falsches Feld beim Nachbessern („#t1“ als Feldname); qwen2.5-coder:7b einmal eine erfundene E-Mail-Adresse im Formular und einmal kein gültiges JSON.

## Was den Unterschied gemacht hat

1. **Kontextfenster.** Ollama nutzt ohne Angabe nur 4096 Token. Die Web-Anweisungen allein brauchen etwa 2500 Token; eine ganze Seite passte nicht mehr, die Antwort brach mitten im JSON ab. Trendo gibt jetzt 8192 Token mit, bei sehr langen Anfragen 16384 (gilt für alle lokalen Anfragen über Ollama).
2. **`seite_anlegen` mit `bausteine`.** Die ganze Seite in einer Aktion statt vieler einzelner. Vorher bauten die Modelle meist nur einen Baustein oder hängten Bausteine an eine erfundene Seiten-id.
3. **Feste Regeln für häufige Fehler.** Felder neben statt in `props`, Angaben einer Aktion in `props` verpackt, Liste aus Texten statt Zeilen, `true`/`false` bei Ja/Nein, erfundene Bildadresse in einem neuen Baustein (bleibt leer und wird genannt).
4. **Eine Nachfrage.** Ein abgelehnter Plan geht mit der genauen Meldung einmal an die KI zurück.

## Empfehlung

`qwen2.5-coder:14b` (Apache-2.0, etwa 9 GB) steht jetzt in der KI-Bibliothek. Trendo empfiehlt ein Modell erst, wenn es den Prüflauf **auf dem eigenen PC** bestanden hat und zur Hardware passt. Das Modell wird nicht mitgeliefert.
