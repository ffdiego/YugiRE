# Runs CoreBoundaryReach.java headless on the analyzed Joey project (read-only).
# Examples:
#   .\scripts\Run-CoreBoundary.ps1                                  # defaults: 005BC700, 005B7B50, table 005EFBB0
#   .\scripts\Run-CoreBoundary.ps1 core-no-ai.txt -Addresses 005BC700,'stop:005B1360','stop:005B16C0'
#   .\scripts\Run-CoreBoundary.ps1 core-depth.txt -Addresses 005BC700,'table:005EFBB0','depth:6'
# Tokens: VA | root:VA | stop:VA | table:VA[:count] | depth:N | addr:follow
# Writes docs\evidence\<Report> and docs\evidence\<Report>.functions.csv
param([string]$Report='core-boundary.txt', [string[]]$Addresses=@())
$ErrorActionPreference='Stop'
if($Report -notmatch '^[a-zA-Z0-9_-]+\.txt$'){throw "Report must look like name.txt, got '$Report'"}
$repository=Split-Path $PSScriptRoot -Parent
$workspace=Split-Path $repository -Parent
$scratch=Join-Path $repository 'scratch'
$env:APPDATA=Join-Path $scratch 'appdata'
$env:LOCALAPPDATA=Join-Path $scratch 'localappdata'
$env:GHIDRA_HEADLESS_JAVA_OPTIONS="-Dapplication.settingsdir=$scratch\settings -Dapplication.cachedir=$scratch\cache"
& "$workspace\ghidra_12.2\support\analyzeHeadless.bat" "$repository\ghidra-projects" Joey -process joey_pc.exe -noanalysis -readOnly -scriptPath $PSScriptRoot -postScript CoreBoundaryReach.java "$repository\docs\evidence\$Report" @Addresses -log "$repository\docs\evidence\core-boundary.log"
if($LASTEXITCODE -ne 0){throw "Headless failed: $LASTEXITCODE"}
