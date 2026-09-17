@echo off
setlocal
if not exist "%~dp0Unity\Builds\Windows\Yol-Arkadasi.exe" (
  echo Oyun derlemesi bulunamadi. Once DERLE.cmd dosyasini calistirin.
  pause
  exit /b 1
)
start "Yol Arkadasi" /D "%~dp0Unity\Builds\Windows" "%~dp0Unity\Builds\Windows\Yol-Arkadasi.exe"
