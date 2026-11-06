<# 
    Ghidra static reconnaissance for -speedy and turn-delay candidates.
    No target execution or patching. Uses the existing read-only archive query.
    Reports are written to docs/evidence/ (the same policy as Run-ArchiveQuery).
#>
param(
    [string]$Prefix = 'speedy-recon'
)
$ErrorActionPreference = 'Stop'

if ($Prefix -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') {
    throw 'Prefix must contain only letters, numbers, _ and -'
}
$query = Join-Path $PSScriptRoot 'Run-ArchiveQuery.ps1'

# Command-line arguments might be stored in either ASCII or UTF-16LE.
# Query case variants without assuming the option's encoding or parser.
& $query -Report "$Prefix-argument-strings.txt" -Addresses @(
    'text:-speedy',
    'wtext:-speedy',
    'text:-SPEEDY',
    'wtext:-SPEEDY',
    'text:Speedy',
    'wtext:Speedy',
    'text:speedy',
    'wtext:speedy'
)

# Existing docs/20-turn-scheduling.md identifies the phase handlers.
# Both 005A5A0D and 005A4224 are command-emitting return call sites,
# not necessarily beginnings of functions.
& $query -Report "$Prefix-duel-handlers.txt" -Addresses @(
    '005A59B0',
    '005A41C0',
    '005BC700',
    '005A5A0D',
    '005A4224',
    'ins:005A59B0:50',
    'ins:005A41C0:50'
)

# The symbol query prints references even if there are more owners than
# QueryArchive's 12-function decompiler limit. An absent import is not
# evidence that Joey has no timing logic (e.g. it might use a wrapper).
& $query -Report "$Prefix-timer-imports.txt" -Addresses @(
    'symbol:Sleep',
    'symbol:SleepEx',
    'symbol:timeGetTime',
    'symbol:GetTickCount',
    'symbol:GetTickCount64',
    'symbol:QueryPerformanceCounter',
    'symbol:SetTimer',
    'symbol:WaitForSingleObject'
)

Write-Host "Saved static read-only Ghidra reports to docs/evidence/$Prefix-*.txt"
