# Verbesserung 42 – Bedienung

Die Leiste sitzt oben in einem Studio, nicht auf dem Startbildschirm. Bei den 3D-Modellen liegt sie links im Rollbereich, damit die Vorschau sichtbar bleibt.

1. Lege zuerst ein Projekt an. Ohne Projekt steht dort „Rückfrage“ und „Lege zuerst ein Projekt an.“
2. Schreib in das Feld „Was soll dieses Studio tun?“, was passieren soll. Angaben stehen als `Feld: Wert`, zum Beispiel `Titel: Hofseite`.
3. „Auftrag prüfen“ macht nur den Plan. Der Stand heißt dann „Plan steht“. Es liegt noch keine Datei da. Die Vorschau sagt das.
4. „Los“ schreibt die Datei und prüft sie. Erst danach steht „QA bestanden“, und nur wenn die Datei wirklich im Projekt liegt und die Prüfung sie gelesen hat.
5. Fehlt etwas, steht „Rückfrage“. Die Antwort kommt in dasselbe Feld, dann „Antwort übernehmen“ oder noch einmal „Auftrag prüfen“.
6. Der Satz „Freigabe: erteilt“ im Auftragstext ersetzt den Knopf „Einmal freigeben“ nicht. Nur vier Vorgänge laufen nach diesem Knopf: eine Datei laden, einen Ordner im Projekt teilen, ein Programm aus dem Katalog installieren, ein Programm aus der Liste mit einer Projektdatei starten. Installieren braucht zusätzlich die vorhandene Lizenz-Zustimmung. Senden, Zahlen, Veröffentlichen, Klonen, Kamera, Mikrofon, Handy-Verbindung, Kopplung, Reparatur und der Explorer bleiben aus. Dann steht „Nicht unterstützt“.
7. Eine neue Eingabe nach einem fertigen Auftrag ändert genau diesen Auftrag. „Rückgängig“ holt die vorige Laufdatei zurück. Die ältere Fassung der Ergebnisdatei bleibt im Projektordner liegen.
8. „Profi“ zeigt Fassung, Schritte, Dateipfade und Prüfung. „Einfach“ blendet das wieder aus. Der Schalter ändert keine Rechte.
9. „Abbrechen“ stoppt einen offenen Auftrag, bevor etwas nach außen geht. Ohne Auftrag steht „Nichts zum Abbrechen.“

Die Zeile darunter nennt den Stand des Studios. „teilweise“ heißt: eine lokale Datei kann entstehen, eine Wirkung nach außen nicht. „blockiert“ heißt: auch „Los“ schreibt keine Datei. „bereit“ steht bei keinem Studio. „nicht eingebaut“ steht bei keinem Katalogstudio mehr. „Cloud-KI ist aus.“ Eine kostenpflichtige KI ist keine Voraussetzung.

Beispiele mit echter Datei: Brief als DOCX, Tabelle mit SUMME, E-Mail-Entwurf, Rechnung, Software-Vorlage, 3D-Datei, Browser-Spiel, Bildumwandlung, Fläche, Karteikarte, HTML-Seite mit Formulardatei, Nachbau-Beschreibung, Speicherabbild, Lizenztext, Startmodus, Ablaufplan. Dazu lokale Auskünfte: Kameranamen ohne Aufnahme, Übersetzung nur Deutsch oder Englisch, gelesene Seite, KI-Antwort, Geräte- und Handy-Liste, Prüfprotokoll. Nach „Los“ baut die offene Studiofläche sich neu auf. Der Baukasten liest `baukasten/seite.json` aus dem neuesten Projekt und setzt den Titel ins Formular. Maßgeblich bleiben die Leiste und die Datei im Projekt.

PDF braucht LibreOffice. Blender, der Unreal-Editor, Versand, Zahlung, Upload, Klonen und Maschinen bleiben aus. Die genaue Zeile je Studio steht in `studio-capability-matrix.md`.

Mögliche Stände: Noch nicht gestartet, Plan steht, Rückfrage, Freigabe nötig, Wird wirklich ausgeführt, Datei erstellt, QA bestanden, Fehlgeschlagen, Abgebrochen, Nicht unterstützt.

Verbesserung 42 ist nicht vollständig implementiert.
