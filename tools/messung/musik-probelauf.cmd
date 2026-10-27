@echo off
rem Musik-KI echt am PC ausprobieren (T-0227): Paket pruefen, entpacken, Server starten, 30-Sekunden-Probelied erzeugen.
rem Doppelklick genuegt. Beim ersten Mal 20-40 Minuten. Am Ende oeffnet sich der Ergebnisordner.
cd /d "%~dp0..\.."
set "TRENDO_ACE_LIVE=%TEMP%\claude\C--Users-manue-Desktop\61f33b34-8214-4ef6-96fd-dfc769da634a\scratchpad\acestep\ACE-Step-1.5.7z"
set "TRENDO_7Z=%LOCALAPPDATA%\TrendoWerkzeuge\7zip\7z.exe"
if exist "%TRENDO_7Z%" goto sevenok
for /d %%P in ("%LOCALAPPDATA%\Packages\Claude_*") do if exist "%%~P\LocalCache\Local\TrendoWerkzeuge\7zip\7z.exe" set "TRENDO_7Z=%%~P\LocalCache\Local\TrendoWerkzeuge\7zip\7z.exe"
:sevenok
if exist "%TRENDO_ACE_LIVE%" goto paketok
for /d %%P in ("%LOCALAPPDATA%\Packages\Claude_*") do if exist "%%~P\LocalCache\Local\Temp\claude\C--Users-manue-Desktop\61f33b34-8214-4ef6-96fd-dfc769da634a\scratchpad\acestep\ACE-Step-1.5.7z" set "TRENDO_ACE_LIVE=%%~P\LocalCache\Local\Temp\claude\C--Users-manue-Desktop\61f33b34-8214-4ef6-96fd-dfc769da634a\scratchpad\acestep\ACE-Step-1.5.7z"
:paketok
if not exist "%TRENDO_ACE_LIVE%" goto nopaket
if not exist "%TRENDO_7Z%" goto noseven
echo.
echo Musik-KI Probelauf startet. Bitte das Fenster offen lassen - beim ersten Mal 20 bis 40 Minuten.
echo Paket: %TRENDO_ACE_LIVE%
echo 7-Zip: %TRENDO_7Z%
echo Fortschritt: %TEMP%\trendo-ace-live\ergebnis\ablauf.log
echo.
dotnet test tests\Trendo.App.Tests -c Debug --filter "FullyQualifiedName~MusikLive" --logger "console;verbosity=normal"
echo.
if exist "%TEMP%\trendo-ace-live\ergebnis\trendo-musik-probe.mp3" goto fertig
echo Kein Lied entstanden. Einzelheiten: %TEMP%\trendo-ace-live\ergebnis\ablauf.log und server.log
if exist "%TEMP%\trendo-ace-live\ergebnis" explorer "%TEMP%\trendo-ace-live\ergebnis"
goto ende
:fertig
echo Fertig! Das Lied: %TEMP%\trendo-ace-live\ergebnis\trendo-musik-probe.mp3
explorer /select,"%TEMP%\trendo-ace-live\ergebnis\trendo-musik-probe.mp3"
goto ende
:nopaket
echo Das ACE-Step-Paket fehlt: %TRENDO_ACE_LIVE%
goto ende
:noseven
echo 7-Zip nicht gefunden: %TRENDO_7Z%
:ende
pause
