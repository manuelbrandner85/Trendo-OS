# REA-Nachbaupolitik – verbindlich für jedes Paket

## Ziel
Der Benutzer soll im normalen Betrieb **so wenig Extra-Programme wie möglich installieren müssen**. REA dient zur legalen Funktionsanalyse und zur Priorisierung der nachzubauenden Trendo-Fachoberflächen.

## Grenzen
REA darf Anforderungen/Workflows aus legalen öffentlichen Dokumenten und zulässiger Interaktion ableiten. Verboten: geschütztes Original-UI, Marken, Bilder, Quellcode, proprietäre Binärteile oder private Lizenz-/Credential-Daten zu übernehmen; keine Umgehung technischer Schutzmaßnahmen oder verbotene Decompilation. Eigene Implementierung und Tests, nach Möglichkeit offene standardisierte Formate.

## Make/Adapt/Integrate/Defer
**MAKE**: Gemeinsame native Arbeitsflächen, Dateibrowser, Layer/Tracks, einfache Effekte, Preview, Editierdaten, Undo/Redo.
**ADAPT**: Vorhandene Trendo-Dienste und Bibliotheken mit lokaler API, ohne neuen Kern.
**INTEGRATE**: Unreal/Unity/Blender, GPU-Codec, Hardwaretreiber oder Qualitätsmodelle, wenn Eigenimplementierung unverhältnismäßig ist; Installationen optional/freigegeben.
**DEFER**: unklar lizenzierte, nicht sichere, ungetestete oder zu teure Funktionen. Immer transparent.

## REA-Analyseartefakt je Vorbild
`feature`, `observable_behavior`, `source_url`, `legal_basis_review`, `own_design`, `reuse_path`, `native_scope`, `external_adapter_if_needed`, `license`, `acceptance_test`, `maintenance_cost`, `decision`, `owner_approval`.

## Beweisregel
Kein „nachgebaut“, wenn lediglich ein Button öffnet oder ein Markdown-Konzept existiert. Tatsächlicher Dateizustand muss geändert werden, inklusive Reload-, Undo-, Export- und Negativtests.
