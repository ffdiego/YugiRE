<#
  Read-only follow-up to Run-RngRecon.ps1.
  Confirms whether 0x005C3BF5 is a seed setter, and locates the state
  used by _rand @0x005C3C02 and the shuffle mode selector.
  No execution/patching of Joey or changes to the Ghidra project.
#>
param([string]$Prefix = 'rng-seed')
$ErrorActionPreference = 'Stop'
if ($Prefix -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') {
    throw 'Prefix must contain only letters, digits, underscores, hyphens'
}
$query = Join-Path $PSScriptRoot 'Run-ArchiveQuery.ps1'

& $query -Report "$Prefix-core.txt" -Addresses @(
    '005c3bf5',
    '005c3c02',
    '0044baa0',
    'ref:005c3bf5',
    'ins:005c3bf5:25',
    'ins:005c3c02:40',
    'ins:0044baa0:60'
)
if ($LASTEXITCODE -ne 0) { throw "RNG core query failed: $LASTEXITCODE" }

& $query -Report "$Prefix-callers.txt" -Addresses @(
    '00408b90',
    '0044bee0',
    '00558bf0',
    'ins:00408b90:100',
    'ins:0044bf90:80',
    'ref:005bddb0',
    'xref:005c3c02'
)
if ($LASTEXITCODE -ne 0) { throw "RNG callsite query failed: $LASTEXITCODE" }

& $query -Report "$Prefix-init.txt" -Addresses @(
    '0044b980',
    '005b9e50',
    '005bfeb0',
    'ins:0044b980:65',
    'ins:005b9e50:100'
)
if ($LASTEXITCODE -ne 0) { throw "RNG initialization query failed: $LASTEXITCODE" }

Write-Host "Reports: docs/evidence/$Prefix-core.txt, $Prefix-callers.txt, $Prefix-init.txt"
Write-Host 'Interpret memory writes and actual callsites before assuming a time-based seed.'
