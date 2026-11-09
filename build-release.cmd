@echo off
setlocal

set "PROJ=%~dp0"
set "DIST=%PROJ%dist"
set "PUB=%DIST%\publish"
set "ZIP=%DIST%\WinGetMGui.zip"

if exist "%PUB%" rd /s /q "%PUB%"
dotnet publish "%PROJ%WinGetMGui.csproj" -c Release -o "%PUB%" -p:PublishSingleFile=true -p:SelfContained=false -p:DebugType=none -p:DebugSymbols=false
if errorlevel 1 goto :fail

set "SEVENZIP="
if exist "%ProgramFiles%\7-Zip\7z.exe" set "SEVENZIP=%ProgramFiles%\7-Zip\7z.exe"
if not defined SEVENZIP if exist "%ProgramFiles(x86)%\7-Zip\7z.exe" set "SEVENZIP=%ProgramFiles(x86)%\7-Zip\7z.exe"
if not defined SEVENZIP (echo 7-Zip not found. & goto :fail)

if not exist "%DIST%" mkdir "%DIST%"
if exist "%ZIP%" del /q "%ZIP%"

"%SEVENZIP%" a -tzip -mx=9 "%ZIP%" "%PUB%\*"
if errorlevel 1 goto :fail

echo Built "%ZIP%".
exit /b 0

:fail
echo Build failed.
exit /b 1
