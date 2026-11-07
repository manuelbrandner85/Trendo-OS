# Theatre.js (theatre-js/theatre, main 6ea82b9, letzter Commit 2024-04-11 – ruht)
- @theatre/core 0.7.0, r3f, dataverse: Apache-2.0. **@theatre/studio und app: AGPL-3.0** → Editor-Oberfläche nicht übernehmbar.
- Befund im Test: `getProject` ohne Studio verlangt einen fertigen Projektstand (`config.state`), sonst Fehler. → In diesem Lauf **nicht gebündelt**; Bewegungen laufen über die Web Animations API.
- Plan (Modul 09): eigene Zeitleiste in Trendo (clean-room), die gültige Theatre-Stände erzeugt; Abspielen in der fertigen Seite mit @theatre/core.
