@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
set "PYTHONUTF8=1"
set "PYTHONIOENCODING=utf-8"
title Podcast Auto Editor

rem ---- 1. Python ------------------------------------------------------------
set "PY="
py -3 -c "import sys" >nul 2>nul && set "PY=py -3"
if not defined PY python -c "import sys" >nul 2>nul && set "PY=python"
if not defined PY goto install_python

rem ---- 2. FFmpeg ------------------------------------------------------------
where ffmpeg >nul 2>nul && goto have_ffmpeg
if exist "%LOCALAPPDATA%\Microsoft\WinGet\Links\ffmpeg.exe" goto have_ffmpeg
echo [setup] Installing FFmpeg (only the first time)...
winget install -e --id Gyan.FFmpeg --accept-source-agreements --accept-package-agreements
:have_ffmpeg

rem ---- 3. Python packages (only the first time) -----------------------------
if exist ".venv\Scripts\python.exe" goto have_venv
echo [setup] Preparing Python packages (only the first time)...
%PY% -m venv .venv
if errorlevel 1 goto fail
:have_venv
".venv\Scripts\python.exe" -m pip install --disable-pip-version-check -q -r requirements.txt
if errorlevel 1 goto fail

rem ---- 4. Run -----------------------------------------------------------------
".venv\Scripts\python.exe" -m podsync %*
echo.
pause
exit /b 0

:install_python
echo [setup] Python is not installed. Installing it with winget (only the first time)...
winget install -e --id Python.Python.3.12 --accept-source-agreements --accept-package-agreements
echo.
echo Python is installed. Close this window and double-click run_windows.bat again.
pause
exit /b 1

:fail
echo.
echo Setup failed. Please take a screenshot of this window and send it.
pause
exit /b 1
