@echo off
setlocal EnableExtensions

rem Build the deliberately x86-only fixture, even from an x64 Developer shell.
rem All changes remain within setlocal; the caller's environment is preserved.
set "VSDEVCMD="
if defined VSINSTALLDIR if exist "%VSINSTALLDIR%Common7\Tools\VsDevCmd.bat" set "VSDEVCMD=%VSINSTALLDIR%Common7\Tools\VsDevCmd.bat"
if defined VSDEVCMD goto have_vsdevcmd
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
  echo ERROR: Visual Studio installation not found. Use a VS Developer shell. 1>&2
  exit /b 2
)
for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSDEVCMD=%%I\Common7\Tools\VsDevCmd.bat"
:have_vsdevcmd
if not defined VSDEVCMD (
  echo ERROR: MSVC x86 tools missing. Install the Visual C++ x86 toolset. 1>&2
  exit /b 2
)
if not exist "%VSDEVCMD%" (
  echo ERROR: VsDevCmd.bat not found: "%VSDEVCMD%" 1>&2
  exit /b 2
)
call "%VSDEVCMD%" -no_logo -arch=x86 -host_arch=x64
if errorlevel 1 (
  echo ERROR: Unable to initialize MSVC x86 target. 1>&2
  exit /b 2
)
if /I not "%VSCMD_ARG_TGT_ARCH%"=="x86" (
  echo ERROR: Target is "%VSCMD_ARG_TGT_ARCH%", expected x86. 1>&2
  exit /b 2
)
where cl >nul 2>nul
if errorlevel 1 (
  echo ERROR: cl.exe not found after x86 toolchain initialization. 1>&2
  exit /b 2
)
echo Compiling target: %VSCMD_ARG_TGT_ARCH%
cl /nologo /W4 /Od /Z7 /TC /Fo:"%~dp0esi_fixture_x86.obj" /Fe:"%~dp0esi_fixture_x86.exe" "%~dp0esi_fixture_x86.c" /link /DEBUG /PDB:"%~dp0esi_fixture_x86.pdb"
if errorlevel 1 exit /b 1
echo Built: %~dp0esi_fixture_x86.exe
endlocal
