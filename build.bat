@echo off
setlocal

REM ============================
REM CONFIG
REM ============================
set "CONFIG=Release"
set "ROOT=%~dp0"
set "PROJECT=%ROOT%UpuGuiAvalonia"
set "BUILDS=%ROOT%Builds"

REM ============================
REM PREPARE OUTPUT DIRS
REM ============================
if not exist "%BUILDS%" mkdir "%BUILDS%"

REM ============================
REM BUILD WINDOWS
REM ============================
:build_win
echo.
echo ============================
echo Publishing UpuGuiAvalonia
echo ============================

cd /d "%PROJECT%"

dotnet publish "UpuGuiAvalonia.csproj" ^
    -c %CONFIG% ^
    -r win-x64 ^
    -o "%BUILDS%"

if errorlevel 1 goto :error

goto :done

REM ============================
REM END
REM ============================
:done
echo.
echo Build complete
echo Output directory:
echo   %BUILDS%
exit /b 0

:error
echo.
echo Build failed
exit /b 1