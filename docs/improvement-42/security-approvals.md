# Verbesserung 42 – Freigaben und Grenzen

Die Tabelle folgt Abschnitt 7 des Auftrags. „Blockiert“ heißt: der gemeinsame Kern lässt die Wirkung nicht zu. Ein Fachadapter darf sie nur innerhalb von `ApprovalGate.Execute` ausführen, und auch nur, wenn `CanPerformSideEffect` wahr ist.

| Vorgang | Im Kern | Ohne Freigabe |
|---|---|---|
| Lokaler Entwurf | `Produce` schreibt nur über `ProjectStore.ResolveInProject` | Läuft mit `Permission.RequestAction` |
| Nachricht | `SideEffectClass.Message`, Freigabe `prompt:{aktion}`, 10 Minuten, ein Ziel | Entwurf bleibt, Wirkung 0 |
| Zahlung, Buchung | `Payment` | Keine Buchung. `Ledger.WriteInvoice` wird von diesem Kern nicht aufgerufen |
| Veröffentlichen, Git | `Publish` | Kein Push, kein Merge |
| Löschen, Rechte, Sicherheit | `DeleteOrRights`, `Security` | Blockiert |
| Boot, Gerät, Maschine, Roboter, CNC | `Machine` | Keine Aktion. SPEC-14 und CORE-09 haben zusätzlich einen harten Blocker in der Matrix |
| Externe KI | `ConsentStore`, Standard aus | Audit `ki.external.blocked`, kein Aufruf |

## Weitere Grenzen

- Anhänge werden nur im Projekt aufgelöst. Datei-Symlinks (`FileInfo.LinkTarget`) und Pfade mit `..` werden abgewiesen. Der Anhangstext wird nicht in die Parameter gemischt, damit „Senden: ja“ in einer Datei keine Freigabe wird.
- Das Protokoll speichert Studio, Aktion, Phase und die Länge des Auftrags. Den Auftragstext und Geheimnisse schreibt es nicht.
- `Rbac`: anfordern darf, wer `RequestAction` hat. Freigeben bleibt `ApproveAction` (Owner). Ein Gast ohne dieses Recht kommt nicht in den Lauf.
- Eine abgelaufene Freigabe setzt `ApprovalGate.Execute` auf `Expired`, bevor die Wirkung läuft.
- Abbruch vor der Wirkung verbraucht die offene Freigabe über `Deny` und sendet nichts.
- Ein Schlüssel, der schon `claimed`, `done` oder `blocked` ist, wird bei Retry und Resume nicht noch einmal ausgeführt. `blocked` entsteht erst, wenn die Wirkung nach außen angefangen hat und keinen Beleg liefert. Eine Ablehnung davor (fehlende Zustimmung, fehlender Dienst) gibt den Schlüssel frei.
- Die Forensik bleibt `ForensicsLab.MaxLife` = 120 Stunden und `WarnBefore` = 24 Stunden. Dieser Auftrag ändert `Forensics.cs` nicht.

## Registrierte Adapter

`PromptActions.CreateDefault()` registriert 82 Aktionen für alle 54 Katalogstudios. `CanPerformSideEffect` ist nur bei `datei.laden`, `datei.teilen`, `programm.installieren` und `programm.starten` wahr. Diese vier laufen nur in `ApprovalGate.Execute`, nach dem Knopf „Einmal freigeben“. Installieren verlangt zusätzlich die Lizenzdatei und ein Programm aus `DependencyCatalog`. Der Auftragstext „Freigabe: erteilt“ setzt das nicht.

`SPEC-09` und `SPEC-12` bleiben `blocked`. `Gesperrte_Studios_schreiben_nichts` startet sie mit „Freigabe: erteilt“. Es entsteht keine Datei. Kamera, Mikrofon, Roboter, Handy-Verbindung, Kopplung, Explorer, Reparatur, Theme, Desktop und Geheimniswerte bleiben ohne Wirkung. Lesen (Namen, Liste, Protokoll, Speichername) schreibt nur eine Textdatei.

Die Geschwister für Veröffentlichen, Klonen, Senden, Zahlen, Hochladen, Blender, Unreal und Maschinenstart gewinnen, sobald das gefährliche Feld auf `ja` steht, und schreiben dann nichts.

Testadapter, die eine Wirkung nur im Test simulieren, stehen nicht in `CreateDefault`. Versand, Zahlung, Veröffentlichung, Boot und Maschinenlauf haben keine Produktionswirkung. Eine kostenpflichtige KI wird nicht vorausgesetzt. Verbesserung 42 ist nicht vollständig implementiert.
