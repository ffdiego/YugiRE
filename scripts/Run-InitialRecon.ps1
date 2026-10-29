$ErrorActionPreference='Stop'
$repository = Split-Path $PSScriptRoot -Parent
$workspace = Split-Path $repository -Parent
$scratch = Join-Path $repository 'scratch'
New-Item -ItemType Directory -Force $scratch | Out-Null
$env:APPDATA=Join-Path $scratch 'appdata'
$env:LOCALAPPDATA=Join-Path $scratch 'localappdata'
New-Item -ItemType Directory -Force $env:APPDATA,$env:LOCALAPPDATA | Out-Null
$env:GHIDRA_HEADLESS_JAVA_OPTIONS="-Dapplication.settingsdir=$scratch\settings -Dapplication.cachedir=$scratch\cache"
$projects = Join-Path $repository 'ghidra-projects'
New-Item -ItemType Directory -Force $projects | Out-Null
$mode = if(Test-Path (Join-Path $projects 'Joey.gpr')) {
    @('-process','joey_pc.exe','-noanalysis','-readOnly')
} else {
    @('-import',"$workspace\passion\joey_pc.exe",'-analysisTimeoutPerFile','180')
}
& "$workspace\ghidra_12.2\support\analyzeHeadless.bat" $projects Joey @mode -max-cpu 2 -scriptPath $PSScriptRoot -postScript InitialRecon.java "$repository\docs\evidence\ghidra-recon.txt" -log "$repository\docs\evidence\ghidra-headless.log"
if($LASTEXITCODE -ne 0){throw "Headless failed: $LASTEXITCODE"}
