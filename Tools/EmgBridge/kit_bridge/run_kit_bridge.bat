@echo off
REM ALTERNATIVE EMG bridge: the BioRadio Student Unity Kit's own bridge (with CSV recording).
REM Recommended instead: ..\run_bridge.bat (ski_emg_bridge.py). Use only ONE bridge at a time.
cd /d "%~dp0"
py -c "import clr, matplotlib" 2>nul || (
    echo Installing pythonnet and matplotlib - first run only...
    py -m pip install pythonnet matplotlib
)
py bioradio_unity_bridge.py
pause
