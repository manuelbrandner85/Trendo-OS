# Plugin-Prüfung vor dem Laden

Plugin: `Airwindows Consolidated.clap` (CLAP)  
Programmdatei: `Airwindows Consolidated.clap`, 12.682.240 Bytes  
SHA-256: `ff79a0fe2f78a281a3e06e6a13eef47549dbf0b58015c92098db6c3f5d3864f8`  
Architektur: x64 · Einstiegspunkt gefunden: ja · digitale Signatur vorhanden: nein

## Hinweise aus der Importtabelle
- kann Programme/Links öffnen (ShellExecuteW)
- fängt Fenster-Ereignisse ab – bei Plugin-Oberflächen üblich, abgeschottet nicht für andere Programme (SetWindowsHookExW)
- liest Tastenzustände (GetAsyncKeyState)
- lädt weitere Programmteile erst zur Laufzeit (LoadLibrary/GetProcAddress) – deren Inhalt ist hier nicht sichtbar

## Eingebundene Systembibliotheken
d2d1.dll, d3d11.dll, dcomp.dll, dwmapi.dll, dxgi.dll, GDI32.dll, IMM32.dll, KERNEL32.dll, ole32.dll, OLEAUT32.dll, SHELL32.dll, USER32.dll, WINMM.dll

Diese Prüfung liest nur die Datei und ist kein Virenscan. Geladen wird das Plugin nur abgeschottet (eigener Prozess, niedrige Integritätsstufe: keine Schreibrechte auf deine Dateien, Speicher- und Zeitgrenze, keine weiteren Programme).
