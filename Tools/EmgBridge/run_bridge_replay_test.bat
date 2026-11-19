@echo off
REM No hardware: streams the recorded EMG to the game exactly like the live bridge does.
cd /d "%~dp0"
py ski_emg_bridge.py --replay "..\..\Assets\StreamingAssets\john_final.csv"
pause
