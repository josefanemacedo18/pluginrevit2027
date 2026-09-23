@echo off
REM Instala o DetalhaBIM no Revit 2027 (duplo clique).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Instalar.ps1" %*
pause
