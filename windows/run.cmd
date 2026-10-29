@echo off
setlocal
if not exist "%~dp0dist\Liangtu-0.10.7\Liangtu.exe" (
  echo Liangtu.exe was not found. Please run build.ps1 first.
  pause
  exit /b 1
)
start "" /D "%~dp0dist\Liangtu-0.10.7" "%~dp0dist\Liangtu-0.10.7\Liangtu.exe" --show
exit /b 0
