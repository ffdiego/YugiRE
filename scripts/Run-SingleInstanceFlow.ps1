<#
  Second read-only Ghidra pass: determine how the CreateMutexA
  ERROR_ALREADY_EXISTS result reaches Joey's startup gate.
  No patching, no injection and no launch of the target executable.
#>
param([string]$Prefix = 'single-instance-flow')
$ErrorActionPreference = 'Stop'
if ($Prefix -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') {
    throw 'Prefix must contain only letters, numbers, underscores or hyphens'
}
$query = Join-Path $PSScriptRoot 'Run-ArchiveQuery.ps1'

& $query -Report "$Prefix-startup.txt" -Addresses @(
    '0055db10',
    '0055dcb0',
    '00411b50',
    '0054d460',
    '0054d580',
    'ins:0055db10:130'
)
if ($LASTEXITCODE -ne 0) { throw "Startup path query failed: $LASTEXITCODE" }

& $query -Report "$Prefix-mutex-name.txt" -Addresses @(
    '0055dbec',
    '005da40c',
    'ref:005da40c',
    '0054d550',
    '0054d450',
    'ins:0055dbd0:70'
)
if ($LASTEXITCODE -ne 0) { throw "Mutex-name query failed: $LASTEXITCODE" }

Write-Host "Saved read-only reports to docs/evidence/$Prefix-*.txt"
