@echo off
setlocal
set "MOTOR_EDITOR=C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Unity.exe"
if not exist "%MOTOR_EDITOR%" (
  echo Unity 6000.3.18f1 bulunamadi.
  exit /b 1
)
"%MOTOR_EDITOR%" -batchmode -quit -projectPath "%~dp0Unity" -executeMethod DortCuce.UnityGame.Editor.MotorCoopBuild.GenerateAndBuild -logFile "%~dp0qa\build.log"
exit /b %errorlevel%
