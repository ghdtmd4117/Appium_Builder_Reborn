@echo off
cd /d "%~dp0\.."
python -m pytest Tests\python -q
pause
