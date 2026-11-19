@echo off
REM Downhill Rush - live Wii Balance Board.
REM Starts wiiuse.exe IN THIS FOLDER, so it writes WiiMote1_All.txt here,
REM which is exactly where the game reads it (SensorSettings > Cop Live File).
REM Pair the board in Windows Bluetooth first (see README, "Live sensors").
cd /d "%~dp0"
if exist WiiMote1_All.txt (
    for %%A in (WiiMote1_All.txt) do echo Old WiiMote1_All.txt found: %%~zA bytes.
    choice /M "Delete the old log before starting (recommended, it only grows)"
    if errorlevel 2 goto start
    del WiiMote1_All.txt
)
:start
echo Starting wiiuse.exe - press the red SYNC button on the board if it does not connect.
echo Keep this window open while playing. Close it to stop.
wiiuse.exe
pause
