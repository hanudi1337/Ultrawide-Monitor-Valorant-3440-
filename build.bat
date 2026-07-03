@echo off
setlocal
cd /d "%~dp0"

rem C#-Compiler des in Windows enthaltenen .NET Framework 4.x verwenden
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo FEHLER: csc.exe nicht gefunden - .NET Framework 4.x fehlt.
    exit /b 1
)

if not exist bin mkdir bin
copy /y lib\NvAPIWrapper.dll bin\ >nul

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
  /out:bin\ValorantStretchHelper.exe ^
  /r:lib\NvAPIWrapper.dll ^
  /r:System.Management.dll ^
  src\Program.cs src\MainForm.cs src\DisplayHelper.cs src\NvidiaScaler.cs src\BackupStore.cs

if errorlevel 1 (
    echo.
    echo BUILD FEHLGESCHLAGEN
    exit /b 1
)

echo.
echo Build OK:  bin\ValorantStretchHelper.exe
endlocal
