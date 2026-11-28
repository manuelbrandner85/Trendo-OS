# Claude-Abgleich mit Studio-Dokumenten (.tdoc)

Stand: T-0788 (Stammbaum, 4. Prüfrunde, 28.11.2026). Gilt für alle Programme der Programm-Hülle.

## Wie der Abgleich arbeitet
- Beim Senden im Claude-Bereich schreibt Trendo das offene Dokument als Arbeitsdatei (`<Titel>.tdoc`) in Claudes Projektordner. Ist das Dokument noch nie gespeichert, legt Trendo es dort als Dokumentdatei an (seit T-0680: die Arbeitsdatei ist dann das Dokument selbst). Trendo merkt sich zu jeder selbst geschriebenen Datei **Programm, Dokument-Id und SHA-256** (nur im Speicher dieser Sitzung); ebenso für Versionen, die ein Speichern dort anlegt.
- Ändert Claude die Datei, gleicht Trendo **nur mit dem Dokument ab, aus dem die Datei stammt** – nie mit dem gerade aktiven Reiter. Gehört die Datei zu keinem offenen Dokument mehr, wird nichts übernommen („… gehört zu keinem offenen Dokument mehr – nichts übernommen“).
- Lehnt der Wächter eine Änderung ab (gesperrte Felder, `KiCheck`, Vorab-Ablehnung), setzt Trendo die Datei auf den Stand davor zurück – nur mit der Sicht, die Claude sehen darf (`ProgramSpec.KiView`).

## Was als „Claudes Ordner“ gilt
Alles **unterhalb** von `Projekte\Claude` (Wurzel der Claude-Projekte) und unterhalb der Projektordner aller Claude-Projekte aller Studios (auch eigene Projektordner wie Web- oder Spieleordner), dazu jede als Arbeitsdatei geführte Datei. Pfade werden normalisiert verglichen (voller Pfad, Groß-/Kleinschreibung egal, mit abschließendem Trenner). Ein mit einem Projekt nur verknüpftes Dokument im Studio-Ordner zählt nicht.

## „Nur lokale KI“ – Programm oder einzelnes Dokument
- `ProgramSpec.KiLocalOnly` (Programm, z. B. Nachhilfe, Kinderprofil) und `ProgramSpec.KiLocalOnlyDoc` (genau dieses Dokument, z. B. Stammbaum mit „Lebende ausblenden“ und lebenden Personen) sperren Claude auf allen Wegen: Bereich, Chat samt .tdoc, Plan-Zeilen, Brücke. Den Satz liefert `ProgramSpec.KiLocalOnlyWhy`.
- Kommt trotzdem eine geänderte Arbeitsdatei zurück (Schutz während des Laufs eingeschaltet), schreibt Trendo **nichts** zurück.
- Ein solches Dokument wird **nie** in Claudes Ordner gespeichert – Strg+S, „Speichern unter“ (auch in Unterordner) und automatisches Speichern landen im Ordner des Studios (`ProgramNewTarget`; läge der selbst unter Claudes Ordner, wird abgelehnt). Meldung: „Lebende Personen sind geschützt – das Dokument liegt jetzt in <Ort> …“.
- Liegt das Dokument beim Einschalten des Schutzes oder beim Speichern in Claudes Ordner, **verlegt** Trendo es: neue Datei schreiben und lesend prüfen, dann die Begleitdateien nachziehen, zuletzt die alte Datei entfernen:
  - **Versionen** (`.versionen\<Name>\*.tdoc`): einzeln kopieren, Kopie per SHA-256 prüfen, das Original nur nach der Löschregel entfernen; leere Versionsordner werden entfernt.
  - **Notsicherung** (`<Datei>.autosave`) und **Wiederherstellung** (`<Datei>.wiederherstellen`): Namen vergibt nur Trendo – kopieren, prüfen, entfernen (die Notsicherung entfernt schon das Speichern am neuen Ort).
  - **Zwischendateien** der atomaren Schreibweise (`<Datei>.xxxxxxxx.tmp`): entfernt.
  - Vorschaubilder oder `.bak`-Dateien legt die Programm-Hülle für .tdoc nicht an (geprüft im Code: `ProgramDocument`, `AtomicFile`, `ProgramShell`).
  - Rückgängig, Versionen und Wiederherstellung funktionieren am neuen Ort weiter.
- Schlägt das Verlegen fehl (z. B. voller Datenträger), gilt der Schutz trotzdem (Claude bekommt nichts), das Dokument bleibt offen und vollständig, und die Meldung sagt: „… Die Kopie liegt noch in Claudes Ordner: <Pfad> – bitte Platz schaffen und erneut speichern oder die Datei selbst löschen.“

## Wann Trendo eine Datei in Claudes Ordner löscht
Nur wenn **alle** Bedingungen gelten:
1. Trendo hat die Datei selbst dort geschrieben (eigene Liste dieser Sitzung; Namen der Notsicherung, Wiederherstellung und Zwischendatei vergibt nur Trendo),
2. sie ist **nicht** die Datei eines offenen Dokuments,
3. ihr Inhalt ist **genau** das, was Trendo zuletzt geschrieben hat (SHA-256 gleich).

Sonst bleibt die Datei unverändert. Die Meldung enthält **einen Satz je Datei**, z. B. „Arbeitskopie konnte nicht gelöscht werden: <Name> (<Grund>) – bitte schließen bzw. selbst löschen.“ oder „Version <Name> ist am neuen Ort, die alte konnte nicht gelöscht werden (<Grund>) – bitte selbst löschen.“ Gründe: Datei eines offenen Dokuments, nicht von Trendo geschrieben, seitdem verändert, geöffnet oder schreibgeschützt. Eine beim Verlegen gemeldete Datei wird danach nicht noch einmal als Arbeitskopie gemeldet.

Folge (bewusst): Hat Claude die Arbeitsdatei während des Laufs geändert und wird danach der Schutz eingeschaltet, bleibt Claudes Fassung liegen – mit den Daten, die Claude zu dem Zeitpunkt ohnehin hatte. Trendo fügt nichts hinzu und löscht keine Datei, deren Inhalt es nicht kennt.

## Nur zum Lesen geöffnete Dokumente (Verhaltensänderung seit T-0788)
- Ist ein Dokument nur zum Lesen offen (es ist in einem anderen Trendo-Fenster in Arbeit) und liegt seine eigene Datei in Claudes Ordner, schreibt Trendo dafür keine Arbeitsdatei und führt die Datei nicht als Arbeitsdatei.
- Ändert Claude diese Datei, übernimmt Trendo die Änderung **nicht** in das nur lesbare Dokument. Meldung: „Claudes Änderung an „<Name>“ gehört zu keinem offenen Dokument mehr – nichts übernommen.“
- Begründung: Ein nur lesbares Dokument soll keine Änderungen übernehmen (sonst überschreibt das andere Fenster sie oder es entstehen zwei Stände), und die Bindung an das Ursprungsdokument verhindert, dass Claudes Datei in ein falsches Dokument fließt. Die Änderung bleibt in Claudes Datei; die Person kann sie im bearbeitenden Fenster öffnen.

## Grenzen
- Die Liste der Arbeitsdateien und Prüfsummen lebt nur in dieser Sitzung. Dateien aus früheren Sitzungen kennt Trendo nicht und löscht sie nicht; gehören sie zum verlegten Dokument, werden sie beim Verlegen mit Namen gemeldet.
