@echo off
echo ========================================================
echo   Starting EMG Lift 3D Game + Replay Dataset Sender
echo ========================================================
echo.

cd /d "%~dp0"

echo Launching Unity Game...
start "" "%~dp0EMGWeightlifting_Build\EMGWeightlifting.exe"

echo Waiting 2 seconds for game to open...
timeout /t 2 >nul

echo Starting dataset replay (KUNAL LOADED.csv, Filter B)...
cd "BioRadio_Student_Unity_Kit\Python_Bridge"
python replay_sender.py "LOADED\KUNAL LOADED.csv" --filter B --loop
pause
