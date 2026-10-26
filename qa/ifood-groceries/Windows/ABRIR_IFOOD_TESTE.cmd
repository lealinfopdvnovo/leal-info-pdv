@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Conectar_iFood_Teste.ps1"
