# T-0775 ECHT: Grundriss & Wohnen – Claude verschiebt eine Wand

Datum: 2026-11-26 10:54 · Claude Code: installiert=True, angemeldet=True, Anmeldung=claude.ai, Abo=max
Nichts ersetzt außer der Fensterdarstellung (headless): kein ClaudeRunOverride, kein ClaudeStatusOverride, kein Ersatz-KI-Dienst. API-Variablen und Token entfernt (ClaudeEnv).

## Ablauf

- Vorlage „Badezimmer“, als eigener Plan gespeichert (grundriss-vorher.tdoc), Claude-Bereich von „Wohnung planen“ geöffnet; Claude bekam das Dokument als .tdoc im Projektordner (31 s).
- Wand 6002d85e: vorher (272|-12) → (272|232), nach Claude (322|-12) → (322|232).
- Gespeichert (wie Strg+S): grundriss-nach-claude.tdoc · SHA-256 05b8a06a2968b569cd4e7f7025af9b46a8afec36170cb9196b2070d876141cde
- Rückgängig-Schritt: „Projektdaten bearbeitet“; Strg+Z: Wand wieder an alter Stelle = True
- Rohlog: grundriss-rohlog.jsonl · SHA-256 b3f27bc1bbd5354881833fff3fdc4cde3bdad7ed010be6a40e1531138ecbd3f5

Ergebnis: BESTANDEN.
