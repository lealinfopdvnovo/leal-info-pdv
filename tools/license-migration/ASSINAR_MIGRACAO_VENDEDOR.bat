@echo off
where pwsh.exe >nul 2>nul
if errorlevel 1 (
 echo Requer PowerShell 7 SOMENTE no computador do vendedor.
 pause
 exit /b 1
)
pwsh.exe -NoProfile -STA -File "%~dp0ASSINAR_MIGRACAO_VENDEDOR.ps1"
if errorlevel 1 pause
