@echo off
REM Starts the BioRadio -> Downhill Rush EMG bridge (live). Keep this window open while playing.
cd /d "%~dp0"
py ski_emg_bridge.py %*
pause
