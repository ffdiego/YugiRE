<#
  Read-only targeted Ghidra investigation of the four still-unverified raw
  high-word differences from the two reproducible YugiLab captures.

  Known: 114 differences provably inherit EAX upper half of CARD_ID pointer
  in FUN_004022E0; one inherits EDX upper half from a MOV DX.
  Unknown: the two extra card-ID-looking sites, plus 0x37 and 0x86.
  Does NOT patch the executable or call native functions.
#>
param([string]$Prefix = 'command-highword-remaining')
$ErrorActionPreference = 'Stop'
if ($Prefix -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') {
    throw 'Prefix must contain only letters, digits, underscores or hyphens'
}
$query = Join-Path $PSScriptRoot 'Run-ArchiveQuery.ps1'

# Opcode 0x72, return 005783BC: a different branch than proven 005783F2.
# Opcode 0xE6, return 005ACF72: distinct from proven 005ABD70.
& $query -Report "$Prefix-card-id.txt" -Addresses @(
    '00578390',
    '005783BC',
    '005ACF72',
    '004022E0',
    'ins:00578370:75',
    'ins:005ACF35:85',
    'ins:00402310:30',
    'ref:004022E0'
)
if ($LASTEXITCODE -ne 0) { throw "Card-ID remaining query failed: $LASTEXITCODE" }

# Opcode 0x37, return 0058CF96: same high word as opcode 0x86 in
# this duel, but Ghidra had no instruction at 0058CF70.
# Opcode 0x86, return 00574804, comes from FUN_005747E0(param_2).
# Trace that raw param_2 upstream instead of assuming it is a card ID.
& $query -Report "$Prefix-raw-callers.txt" -Addresses @(
    '0058CF96',
    '005747E0',
    'ins:0058CF90:70',
    'ins:005747E0:32',
    'ref:005747E0',
    'ref:0058CF96'
)
if ($LASTEXITCODE -ne 0) { throw "Raw caller query failed: $LASTEXITCODE" }

Write-Host "Reports: docs/evidence/$Prefix-card-id.txt and $Prefix-raw-callers.txt"
Write-Host 'Do not infer unknown 0x37/0x86 high words are disposable from equality of low 16 bits.'
