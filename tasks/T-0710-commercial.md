# T-0710 Commercial (Phase 1 und 2)

**Auftrag:** Masterprompt „TRENDO OS – ULTIMATE COMMERCIAL GODMODE 2026/2027“. Phase 1 war der Read-only-Audit samt Lückenanalyse. Phase 2 umfasst die Grundlagen nach den Entscheidungen E1–E9, die der Inhaber am 13.11.2026 angenommen hat.

**Branch:** `claude/T-0710-commercial`

**Phase 2, Umfang:**
- Qwen2.5 3B ersetzen (E8).
- Editionsmodell und Prüfung signierter Berechtigungen im Client. Nur öffentliche Schlüssel liegen im Client, ohne Serverschlüssel bleibt die Prüfung aus.
- Seite „Konto, Lizenz & Vollversion“ mit den Lizenzhinweisen.
- Die Version kommt nur noch aus einer Quelle.

**Nicht im Umfang:**
- Server, Paddle, E-Mail, Owner Console (eigenes privates Repository, E1)
- Kauf und Zahlungen
- Änderungen an `requirements.yaml` und `decisions.yaml`

**Erfolg:** Alle Testprojekte und die Paket-Tests sind grün, und das Verhalten für bisherige Nutzer bleibt gleich (alle Studios frei).
