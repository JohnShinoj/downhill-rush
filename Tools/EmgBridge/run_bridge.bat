@echo off
REM Starts the BioRadio -> Downhill Rush EMG bridge (live). Keep this window open while playing.
cd /d "%~dp0"
REM First run only: install the two Python libraries the bridge needs, if they are missing.
py -c "import clr, matplotlib" 2>nul || (
    echo Installing pythonnet and matplotlib - first run only...
    py -m pip install pythonnet matplotlib
)
py ski_emg_bridge.py %*
pause
