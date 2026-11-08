<#
  Read-only Ghidra reconnaissance for Joey's deck shuffle RNG and seed.
  No patching, no game execution, no Frida.
  Uses the existing Run-ArchiveQuery.ps1 and QueryArchive.java.
  The "symbol:*" searches are hypotheses, not proofs of RNG implementation.
#>
param([string]$Prefix = 'rng-recon')
$ErrorActionPreference = 'Stop'
if ($Prefix -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') {
    throw 'Prefix must contain only letters, numbers, underscores or hyphens'
}
$query = Join-Path $PSScriptRoot 'Run-ArchiveQuery.ps1'

# Locate code responsible for shuffling, and inspect nearby instructions.
# FUN_00568D80(side,passes) is observed with both sides during demo init.
& $query -Report "$Prefix-shuffle.txt" -Addresses @(
    '00568D80',
    '00569380',
    'ref:00568D80',
    'ins:00568D80:100',
    'ins:00568DE0:100'
)
if ($LASTEXITCODE -ne 0) { throw "Shuffle query failed: $LASTEXITCODE" }

# Candidate CRT RNG and process startup/time-based seeds.
& $query -Report "$Prefix-crt.txt" -Addresses @(
    'symbol:rand',
    'symbol:srand',
    'symbol:_rand',
    'symbol:_srand',
    'symbol:time',
    'symbol:_time64',
    'symbol:GetTickCount',
    'symbol:timeGetTime'
)
if ($LASTEXITCODE -ne 0) { throw "CRT RNG query failed: $LASTEXITCODE" }

# Alternative RNG imports; absence is not evidence against custom PRNG.
& $query -Report "$Prefix-alternatives.txt" -Addresses @(
    'symbol:QueryPerformanceCounter',
    'symbol:GetSystemTimeAsFileTime',
    'symbol:CryptGenRandom',
    'symbol:BCryptGenRandom',
    'symbol:RtlRandom',
    'symbol:RtlRandomEx',
    'symbol:CoCreateGuid'
)
if ($LASTEXITCODE -ne 0) { throw "Alternative RNG query failed: $LASTEXITCODE" }

Write-Host "Saved read-only reports to docs/evidence/$Prefix-{shuffle,crt,alternatives}.txt"
Write-Host 'Check shuffle callees and their callers before assuming rand/srand or a time seed.'
