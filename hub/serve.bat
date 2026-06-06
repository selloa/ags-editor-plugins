@echo off
cd /d "%~dp0\.."
call hub\build.bat
if errorlevel 1 exit /b 1
echo.
echo Serving docs at http://localhost:8000/
echo Press Ctrl+C to stop.
cd docs
python -m http.server 8000
