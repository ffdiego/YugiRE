<#
  Read-only Ghidra investigation of raw upper 16 bits of native command args.
  Triggered by two seed-identical YugiLab runs that differed in 119 raw
  arguments while lower 16 bits and AI decisions stayed identical.
  Reuses the existing query script and NEVER modifies the original binary.
#>
param([string]$Prefix = 'command-highword')
$ErrorActionPreference = 'Stop'
if ($Prefix -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') {
    throw 'Prefix must contain only letters, digits, underscores, hyphens'
}
$query = Join-Path $PSScriptRoot 'Run-ArchiveQuery.ps1'

# Common repeated high-word differences in args[1]:
# CE (callsite 005ABEAB), E6 (005ABD70), 6F (005ABF22).
& $query -Report "$Prefix-placement.txt" -Addresses @(
    '005ABD00',
    '005B9170',
    'ins:005ABD40:100',
    'ins:005ABE7D:90',
    'ins:005ABEF0:90'
)
if ($LASTEXITCODE -ne 0) { throw "Placement command query failed: $LASTEXITCODE" }

# CF (0057977F), 72 (005783F2), 75 (00578459).
& $query -Report "$Prefix-card-commands.txt" -Addresses @(
    '0057977F',
    '005783F2',
    '00578459',
    'ins:00579740:85',
    'ins:005783B0:100',
    'ins:00578420:60'
)
if ($LASTEXITCODE -ne 0) { throw "Card command query failed: $LASTEXITCODE" }

# DF (00595142), uncommon 37 (0058CF96), 86 (00574804),
# and 61 (005BA000). These have different high-word patterns.
& $query -Report "$Prefix-other.txt" -Addresses @(
    '00595142',
    '0058CF96',
    '00574804',
    '005BA000',
    'ins:00595115:70',
    'ins:0058CF70:70',
    'ins:005747D5:60',
    'ins:005B9FC0:70'
)
if ($LASTEXITCODE -ne 0) { throw "Other command query failed: $LASTEXITCODE" }

Write-Host "Reports: docs/evidence/$Prefix-placement.txt, $Prefix-card-commands.txt, $Prefix-other.txt"
Write-Host 'Look for how command args[0]/args[1] are assembled from card words or object IDs.'
Write-Host 'Never normalize raw upper words as disposable until their semantics are established.'
