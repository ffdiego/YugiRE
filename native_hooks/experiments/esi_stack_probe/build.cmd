@echo off
setlocal
rem Run from the "x86 Native Tools Command Prompt for VS" (32-bit target).
where cl >nul 2>nul
if errorlevel 1 (
  echo MSVC cl.exe not found. Open the x86 Native Tools Command Prompt. 1>&2
  exit /b 2
)
cl /nologo /W4 /Od /Z7 /TC /Fo:"%~dp0esi_fixture_x86.obj" /Fe:"%~dp0esi_fixture_x86.exe" "%~dp0esi_fixture_x86.c" /link /DEBUG /PDB:"%~dp0esi_fixture_x86.pdb"
if errorlevel 1 exit /b 1
echo Built: %~dp0esi_fixture_x86.exe
endlocal
