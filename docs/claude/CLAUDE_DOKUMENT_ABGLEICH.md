# Claude-Abgleich mit Studio-Dokumenten (.tdoc)

Stand: T-0788 (Stammbaum, 3. Prüfrunde, 28.11.2026). Gilt für alle Programme der Programm-Hülle.

## Wie der Abgleich arbeitet
- Beim Senden im Claude-Bereich schreibt Trendo das offene Dokument als Arbeitsdatei (`<Titel>.tdoc`) in Claudes Projektordner. Trendo merkt sich dazu **Programm, Dokument-Id und SHA-256** der geschriebenen Datei (nur im Speicher dieser Sitzung).
- Ändert Claude die Datei, gleicht Trendo **nur mit dem Dokument ab, aus dem die Datei stammt** – nie mit dem gerade aktiven Reiter. Gehört die Datei zu keinem offenen Dokument mehr, wird nichts übernommen („… gehört zu keinem offenen Dokument mehr – nichts übernommen“).
- Lehnt der Wächter eine Änderung ab (gesperrte Felder, `KiCheck`, Vorab-Ablehnung), setzt Trendo die Datei auf den Stand davor zurück – und zwar nur mit der Sicht, die Claude sehen darf (`ProgramSpec.KiView`), nie mit mehr.

## „Nur lokale KI“ – Programm oder einzelnes Dokument
- `ProgramSpec.KiLocalOnly` (Programm, z. B. Nachhilfe, Kinderprofil) und neu `ProgramSpec.KiLocalOnlyDoc` (genau dieses Dokument, z. B. Stammbaum mit „Lebende ausblenden“ und lebenden Personen) sperren Claude auf allen Wegen: Bereich, Chat samt .tdoc, Plan-Zeilen, Brücke. Den Satz für die Person liefert `ProgramSpec.KiLocalOnlyWhy`.
- Kommt trotzdem eine geänderte Arbeitsdatei zurück (Schutz während des Laufs eingeschaltet), schreibt Trendo **nichts** zurück.
- Ein solches Dokument wird nie in Claudes Ordner gespeichert (Strg+S, automatisch). Liegt es dort, verlegt Trendo es in den Ordner des Studios: neue Datei schreiben, lesend prüfen, Versionen mitnehmen, erst dann die alte Datei entfernen. Rückgängig und Wiederherstellung bleiben erhalten; die Meldung nennt den neuen Ort.

## Wann Trendo eine Datei in Claudes Ordner löscht
Nur wenn **alle** Bedingungen gelten:
1. Trendo hat die Datei selbst dort geschrieben (eigene Liste dieser Sitzung),
2. sie ist **nicht** die Datei eines offenen Dokuments,
3. ihr Inhalt ist **genau** das, was Trendo zuletzt geschrieben hat (SHA-256 gleich).

Sonst bleibt die Datei unverändert, und die Person liest: „Arbeitskopie konnte nicht gelöscht werden: <Name> (<Grund>) – bitte schließen bzw. selbst löschen.“ Gründe: Datei eines offenen Dokuments, nicht von Trendo geschrieben, seitdem verändert (z. B. von Claude oder der Person), geöffnet oder schreibgeschützt.

Folge (bewusst): Hat Claude die Arbeitsdatei während des Laufs geändert und wird danach der Schutz eingeschaltet, bleibt Claudes Fassung liegen – mit den Daten, die Claude zu dem Zeitpunkt ohnehin hatte. Trendo fügt nichts hinzu und löscht keine Datei, deren Inhalt es nicht kennt.

## Nur zum Lesen geöffnete Dokumente (Verhaltensänderung seit T-0788)
- Ist ein Dokument nur zum Lesen offen (es ist in einem anderen Trendo-Fenster in Arbeit) und liegt seine eigene Datei in Claudes Ordner, schreibt Trendo dafür keine Arbeitsdatei und führt die Datei nicht als Arbeitsdatei.
- Ändert Claude diese Datei, übernimmt Trendo die Änderung **nicht** mehr in das nur lesbare Dokument. Meldung: „Claudes Änderung an „<Name>“ gehört zu keinem offenen Dokument mehr – nichts übernommen.“
- Begründung: Ein nur lesbares Dokument soll keine Änderungen übernehmen (sonst überschreibt das andere Fenster sie oder es entstehen zwei Stände), und die Bindung an das Ursprungsdokument verhindert, dass Claudes Datei in ein falsches Dokument fließt. Die Änderung bleibt in Claudes Datei; die Person kann sie im bearbeitenden Fenster öffnen.
- Grenze: Die Liste der Arbeitsdateien lebt nur in dieser Sitzung. Arbeitsdateien aus früheren Sitzungen kennt Trendo nicht und löscht sie nicht.
