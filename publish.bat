@echo off
setlocal enableextensions
set "ROOT=%~dp0"
set "PROJECT=%ROOT%BatchRunner\BatchRunner.csproj"
set "OUTDIR=%ROOT%publish"

echo Publishing BatchRunner...
dotnet publish "%PROJECT%" -c Release -r win-x64 -o "%OUTDIR%"
if errorlevel 1 (
  echo Publish failed.
  exit /b 1
)

echo Publish complete.
echo Output: %OUTDIR%
