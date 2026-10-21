# 2026-10-21 – Windows-Teile SYS-02/03/10/11/12 auf dem PC geprüft, qpdf verwaltet

- qpdf ist eine AutoUser-Abhängigkeit (Portable-ZIP mit fester SHA-256, curl.exe, Entpacken nur in den Benutzerordner).
- `WinRealTests` (nur Windows, sonst übersprungen): 10/10 auf dem PC grün; volle Suite Api 14 / Core 420 / App 112.
- Offen: Narrator (nur von Hand prüfbar), ein echter zweiter Bildschirm, Rückfall-Lauf mit 0.9.3 → 0.9.2 (folgt mit dem Release).
- Belege: reports/pc-0.9.3/.
