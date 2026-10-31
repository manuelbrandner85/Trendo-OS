# T-0241 – Erweiterungen nur mit Freigabe, sicherer Download

- Quelle: Audit 98–113, Befund 3 (V112 AP-1/AP-2); Roadmap P0
- Freigabe: Nutzer im Chat am 31.10.2026 („T-0241 Erweiterungen“)
- Branch: `claude/T-0241-erweiterungen` · Builder 03-security-permissions / 05-core-api · QA 19-qa-verification
- Ziel: Installieren/Entfernen über ApprovalGate (Ziel = Prüfsumme bzw. Kennung), Not-Stopp sperrt, Rückfrage mit Angaben; atomarer Austausch; Download ohne automatische Weiterleitung, nur GitHub/https, offline gesperrt, Audit; ZIP-Grenzen.
- Nicht Teil: Signaturen/Herausgeberprüfung (offen, Entscheidungs-Delta C10).
