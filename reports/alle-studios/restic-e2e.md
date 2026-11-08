# Verschlüsselte Sicherung mit Versionen (restic) – 08.11.2026

Alle Studios, Runde 8, CORE-11 „Daten schützen“.

**restic 0.19.1** (BSD-2-Clause, Fassung vom 05.07.2026) ist ein Einzelprogramm.
- Archiv `restic_0.19.1_windows_amd64.zip`, SHA-256 `da948ad7…e67d` laut Feld „digest“ des GitHub-Release.
- Im Archiv liegt genau `restic_0.19.1_windows_amd64.exe`. Das steht im Release-Werkzeug des Projekts (`helpers/build-release-binaries/main.go`); Trendo übernimmt nur diese Datei als `restic.exe`.

## Was die Nutzerin oder der Nutzer sieht
In „Daten schützen“ gibt es die Karte **„Verschlüsselte Sicherung mit Versionen (restic)“**:
- **Eingaben:** Ordner (mehrere mit `;`), Sicherungsort (z. B. USB-Platte) und Passwort.
- **„Jetzt sichern“:** Beim ersten Mal wird die Ablage angelegt (`init`). Jede Sicherung legt nur Neues und Geändertes ab, verschlüsselt, mit Kennzeichen `trendo`. Danach steht dort zum Beispiel „120 neue, 3 geänderte, 2000 unveränderte Dateien – 50,0 MB neu dazu (von 2,0 GB)“.
- **„Stände anzeigen“:** alle früheren Stände, die neuesten zuerst.
- **„In neuen Ordner zurückholen“:** Ein Stand landet in `Wiederhergestellt/<Datum> (<Kurz-ID>)`. **Es wird nie etwas überschrieben**; ein nicht leerer Zielordner wird abgelehnt.

## Sicherheit
- **Passwort:** Es geht nur als Umgebungsvariable `RESTIC_PASSWORD` an den restic-Prozess. Es steht **nie** in den Argumenten, in Dateien oder im Audit-Protokoll; das prüft der Test. Trendo speichert es nicht und weist darauf hin, es gut aufzubewahren.
- **Sicherungsort:** Er darf nicht im gesicherten Ordner liegen, sonst würde sich die Sicherung selbst sichern.
- **Stand-Kennungen:** Nur Hex-Zeichen sind erlaubt, also keine Befehlstricks über den Stand.
- **Falsches Passwort:** Es führt zu einer verständlichen Meldung.

## Umsetzung
Die JSON-Felder sind **aus dem Quellcode von restic 0.19.1** übernommen:
- `backup --json`, Zeile `message_type: "summary"`: `files_new`, `files_changed`, `files_unmodified`, `data_added`, `total_bytes_processed`, `total_duration`, `snapshot_id` (internal/ui/backup/json.go).
- `snapshots --json`: Liste mit `time`, `paths` und `id` (internal/data/snapshot.go, cmd_snapshots.go).

## Tests
- **`ResticBackupTests`** (6 Tests, grün):
  - Zusammenfassung und Stände lesen
  - erste Sicherung mit `init`, danach ohne
  - Passwort nur in der Umgebung
  - Wiederherstellen nur in einen neuen oder leeren Ordner
  - Eingabeprüfungen
  - falsches Passwort, fehlende Einrichtung, Entpacken nur der Programmdatei
- **`SchutzRestic_Ui`** (1 Oberflächentest, grün):
  - Einrichten-Knopf, sichern, Stände, zurückholen
  - kein Passwort in Argumenten oder Audit-Protokoll
  - Sicherungsort im gesicherten Ordner wird abgelehnt
- **Gefunden und behoben:** Größenangaben hingen von der Systemsprache ab. Jetzt stehen sie immer deutsch da („50,0 MB“).

## Nicht geprüft (NOT RUN)
- ~~**Echter Lauf von restic** (sichern, Stände, zurückholen, Prüfsumme des Archivs).~~ → erledigt am 08.11.2026 nach Freigabe, siehe „Echter Lauf“ unten. Das wäre ein heruntergeladenes Programm und braucht die Freigabe des Nutzers.
- Zeitgesteuertes Sichern. Bisher wird auf Knopfdruck gesichert.

## Echter Lauf (08.11.2026, Freigabe des Nutzers „Ja, alle testen“)
- **Download:** Das Archiv kam beim zweiten Versuch (GitHub war gestört). Die SHA-256 stimmt exakt; im Archiv liegt genau `restic_0.19.1_windows_amd64.exe`.
- **Echter Ablauf** mit 2 Dateien, darunter ein 3-MB-Foto:
  - Die erste Sicherung legt die Ablage an.
  - **Die Ablage enthält keinen Klartext** (Markierung im Brief nicht auffindbar).
  - Die zweite Sicherung nach einer Änderung meldet 0 neu, 1 geändert, 1 unverändert, unter 100 KB dazu – das Foto wird nicht noch einmal abgelegt.
  - 2 Stände.
  - Der ältere Stand kommt zurück: Foto Byte für Byte gleich, Brief ohne den späteren Nachtrag.
  - Ein falsches Passwort ergibt „Das Passwort passt nicht zu dieser Sicherung.“
- **Gefunden und behoben:** Beim Zurückholen baute restic den ganzen Originalpfad nach (`ziel\C\Users\…`) und brach an den Zeitstempeln übergeordneter Ordner ab („Access is denied“). Trendo holt jetzt gezielt jeden gesicherten Ordner zurück (`<stand>:/C/…`): bei einem Ordner direkt ins Ziel, bei mehreren je einen Unterordner. Das ist für Nutzer auch übersichtlicher.
- **Test:** `ResticRealTests.Real_backup` ist grün.
