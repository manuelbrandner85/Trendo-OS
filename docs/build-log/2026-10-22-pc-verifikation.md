# PC-Verifikation – 22 Module auf diesem PC

Auftrag T-0003. Die 22 Module mit Status TESTED_BOX (CORE-02, CORE-03, CORE-09, CORE-10, CORE-11, CORE-12, SYS-04, SYS-05, SYS-06, SYS-07, SYS-08, SYS-09, SPEC-08, SPEC-09, SPEC-10, SPEC-12, SPEC-13, SPEC-14, SPEC-15, SPEC-16, X-02, X-05) haben auf diesem Windows-PC eigene Testläufe.

Jeder Lauf filtert `DisplayName~<Modul>` in Core und App, Release, Rohprotokoll unter `reports/pc-t0003/`. Alle 44 Modulprotokolle sind grün (`failed=0`). Die drei Gesamtsuiten sind ebenfalls grün: Core 427, App 132, Api 14, jeweils 0 Fehler. Api hat keine Tests mit diesen Modul-IDs.

Nachweis: `reports/pc-t0003/UEBERSICHT.md`. In `requirements.yaml` ist nur bei diesen 22 der Teststatus auf PC_VERIFIED und der Nachweis auf diese Übersicht gesetzt. BUS-* unverändert. QA-017, QA-019 und Codesignatur sind nicht geprüft und nicht als bestanden gewertet.

SHA-256 der `.trx` ist in der Übersicht noch offen. Die Prüfsummen werden nachgetragen. Die Rohprotokolle enthalten Kontoname, Rechnername und Pfade unter `C:\Users\<user>\`. Die Stellen bleiben im Rohprotokoll stehen.
