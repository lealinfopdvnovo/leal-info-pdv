@echo off
cd /d "%~dp0"
if not exist "%~dp0Conectar_iFood_Teste.exe" (
  echo Extraia todos os arquivos do ZIP antes de abrir.
  pause
  exit /b 1
)
start "" /wait "%~dp0Conectar_iFood_Teste.exe"
