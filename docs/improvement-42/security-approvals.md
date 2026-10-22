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
- Ein Schlüssel, der schon `claimed`, `done` oder `blocked` ist, wird bei Retry und Resume nicht noch einmal ausgeführt.
- Die Forensik bleibt `ForensicsLab.MaxLife` = 120 Stunden und `WarnBefore` = 24 Stunden. Dieser Auftrag ändert `Forensics.cs` nicht.

## Registrierte Adapter

`PromptActions.CreateDefault()` registriert 68 Aktionen für alle 54 Katalogstudios. Keine hat `CanPerformSideEffect`. Gesperrte Aktionen liefern aus `Produce` den Wert `Supported: false`. Es entsteht keine Datei und kein Beleg. `Gesperrte_Studios_schreiben_nichts` startet jedes blockierte Studio mit dem Text „Freigabe: erteilt“.

Lokale Dateien schreiben nur Adapter mit `SideEffectClass.None`. Die Geschwister für Veröffentlichen, Klonen, Senden, Zahlen, Hochladen, Blender, Unreal, Maschinenstart und Installation gewinnen, sobald das gefährliche Feld auf `ja` steht, und schreiben dann nichts.

Testadapter, die eine Wirkung nur im Test simulieren, stehen nicht in `CreateDefault`. Versand, Zahlung, Veröffentlichung, Boot und Maschinenlauf haben keine Produktionswirkung. Verbesserung 42 ist nicht vollständig implementiert.
