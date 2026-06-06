@echo off
cd /d "%~dp0\.."
python hub\build.py
if errorlevel 1 exit /b 1
