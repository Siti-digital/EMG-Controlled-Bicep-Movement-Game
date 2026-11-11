@echo off
echo ========================================================
echo   Starting EMG Lift 3D Game + BioRadio Python Bridge
echo ========================================================
echo.

cd /d "%~dp0"

echo Launching Unity Game...
start "" "%~dp0EMGWeightlifting_Build\EMGWeightlifting.exe"

echo Launching BioRadio Python Bridge GUI...
cd "BioRadio_Student_Unity_Kit\Python_Bridge"
start "" python bioradio_unity_bridge.py

echo.
echo Both applications launched!
echo In the game: Press 'K' to switch input to EMG mode.
echo.
