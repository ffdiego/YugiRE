param([string]$Report='archive-reader-ghidra.txt', [string[]]$Addresses=@())
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$workspace=Split-Path $repository -Parent
$scratch=Join-Path $repository 'scratch'
$env:APPDATA=Join-Path $scratch 'appdata'
$env:LOCALAPPDATA=Join-Path $scratch 'localappdata'
$env:GHIDRA_HEADLESS_JAVA_OPTIONS="-Dapplication.settingsdir=$scratch\settings -Dapplication.cachedir=$scratch\cache"
& "$workspace\ghidra_12.2\support\analyzeHeadless.bat" "$repository\ghidra-projects" Joey -process joey_pc.exe -noanalysis -readOnly -scriptPath $PSScriptRoot -postScript QueryArchive.java "$repository\docs\evidence\$Report" @Addresses -log "$repository\docs\evidence\archive-query.log"
if($LASTEXITCODE -ne 0){throw "Headless failed: $LASTEXITCODE"}
