# Beleg Trendo Musik V3–V6 – Stimme lernen, Lieder in dieser Stimme, neuer Text, Stimmen-Bibliothek (07.11.2026)

- Freigabe: „Alle ja“ zu V1–V8 (07.11.2026). Feste Regeln: nur eigene Stimme oder Einwilligung der Person, keine Stimmen bekannter Künstler, Liedtexte nur vom Nutzer.
- Lauf: `Trendo.App.Tests.exe` mit MusikVoices_Ui (2 Tests) und allen 23 Musik-Testklassen → Total 52, Failed 0

- V3: Stimme aus dem abgetrennten Gesang eines Lieds lernen. Ohne Bestätigung „keine bekannte Person“ gesperrt; „andere Person“ braucht Name und ausdrückliche Erklärung; der Gesang wird in Abschnitte zu 30 s geschnitten (stille weg), unter 6 Abschnitten wird abgelehnt und nichts gespeichert; Lernen über Applio (RVC) mit der Stimm-Id.
- V4: Beim Erstellen „Gesang in: …“ wählen – nach dem Erzeugen wird der Gesang in die gewählte Stimme umgewandelt und mit der Begleitung gemischt (neues Lied „(Stimme …)“).
- V5: „Neuer Text auf diese Musik“ – eigener Text (mindestens zwei Zeilen) wird per ACE-Step-Cover auf die Begleitung gesungen; das neue Lied hängt an der Begleitung.
- V6: Stimmen-Bibliothek mit Name, Herkunft und Einwilligungsnachweis (Person, Datum, Erklärung); Löschen entfernt Aufnahmen, Modell und Nachweis.
- Hochladen trennt jetzt nur bei „＋ Eigene Aufnahme“ automatisch (nicht bei intern erzeugten Fassungen wie Covers).

## Nicht geprüft
- Echtes Stimm-Lernen und Umwandeln mit Applio auf diesem PC (Laufzeit „Stimme“ nicht eingerichtet; im Test ersetzt). Der Weg ist derselbe wie bei „Meine eigene Stimme“ (M6).
- Echte Erzeugung mit ACE-Step für V5 (im Test ersetzt)
- Klangqualität der gelernten Stimme (Hörtest durch einen Menschen)
