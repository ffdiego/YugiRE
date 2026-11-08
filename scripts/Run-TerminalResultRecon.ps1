<#
  Read-only Ghidra follow-up: inspect the terminal command 0x0004/0x8004
  producer callsite at return VA 0x005BC545. Does not modify joey_pc.exe.
#>
param([string]$Prefix = 'terminal-result')
$ErrorActionPreference = 'Stop'
if ($Prefix -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') {
    throw 'Prefix must contain only letters, numbers, underscores or hyphens'
}
$query = Join-Path $PSScriptRoot 'Run-ArchiveQuery.ps1'

& $query -Report "$Prefix-dispatch.txt" -Addresses @(
    '005bc545',
    '005bc700',
    '005b9170',
    'ins:005bc4e0:100',
    'ins:005bc545:60'
)
if ($LASTEXITCODE -ne 0) { throw "Terminal dispatch query failed: $LASTEXITCODE" }

& $query -Report "$Prefix-state.txt" -Addresses @(
    '005bc545',
    '005a59b0',
    '005a41c0',
    'ins:005bc500:100',
    'ins:005bc590:65'
)
if ($LASTEXITCODE -ne 0) { throw "Terminal state query failed: $LASTEXITCODE" }

Write-Host "Reports: docs/evidence/$Prefix-dispatch.txt and $Prefix-state.txt"
