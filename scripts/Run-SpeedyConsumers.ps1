<#
  Second read-only Ghidra pass after verifying that -speedy sets
  _DAT_005EB618 = 0x3C in FUN_00411B50.

  All addresses are VAs for the documented Joey PE32 hash. This script
  does NOT launch Joey, patch its memory or write the Ghidra project.
#>
param([string]$Prefix = "speedy-consumers")
$ErrorActionPreference = 'Stop'
if ($Prefix -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') {
    throw 'Prefix must contain only letters, digits, underscore and hyphen'
}
$query = Join-Path $PSScriptRoot 'Run-ArchiveQuery.ps1'

# Find all sites reading/writing the value changed by -speedy, and
# inspect its command-line writer. Include xrefs even if Ghidra's
# analysis did not establish a full function at every use.
& $query -Report "$Prefix-global.txt" -Addresses @(
    'ref:005eb618',
    'xref:005eb618',
    'ins:00411c70:55'
)
if ($LASTEXITCODE -ne 0) { throw "Global query failed: $LASTEXITCODE" }

# Timing wrappers select GetTickCount/timeGetTime based on 00A53738.
& $query -Report "$Prefix-clock.txt" -Addresses @(
    'ref:00a53738',
    '005653e0',
    '00565420',
    '00545730',
    '00494c10',
    '00565320'
)
if ($LASTEXITCODE -ne 0) { throw "Clock query failed: $LASTEXITCODE" }

# 005BC700 gates phase progression on presentation and queued commands.
& $query -Report "$Prefix-duel-gates.txt" -Addresses @(
    '0040cf20',
    '005b9ca0',
    '0040ca70',
    '005aa490',
    '005bc700'
)
if ($LASTEXITCODE -ne 0) { throw "Duel gate query failed: $LASTEXITCODE" }

Write-Host "Saved read-only reports to docs/evidence/$Prefix-*.txt"
