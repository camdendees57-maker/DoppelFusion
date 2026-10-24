@echo off
cd /d "%~dp0"
if not exist .env (
  echo Missing .env. Copy .env.example to .env and edit the values first.
  pause
  exit /b 1
)
py -m pip install -r requirements.txt
if errorlevel 1 (
  echo Dependency installation failed.
  pause
  exit /b 1
)
py tiktok_listener.py
pause
