@echo off
REM Saves the current game version to your GitHub (the branch you are on).
cd /d "%~dp0.."
echo Branch:
git branch --show-current
echo.
echo Changes to save:
git status --short
echo.
set /p MSG="Describe this change (or press Enter for a default message): "
if "%MSG%"=="" set MSG=Update game
git add .
git commit -m "%MSG%"
git push
echo.
echo Done. Check the result above.
pause
