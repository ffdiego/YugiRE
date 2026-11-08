<#
  Read-only final Ghidra probe for two unresolved native command high words.
  0x37 @ return address 0058CF96 has no defined instruction in the Ghidra
  listing; read raw bytes and try transient disassembly (rolled back).
  0x86 @ return address 00574804 just forwards an unmasked parameter;
  examine its major producers without assuming it is a card-ID lookup.
  All queries use the existing -readOnly Ghidra project.
#>
param([string]$Prefix = 'command-highword-final')
$ErrorActionPreference = 'Stop'
if ($Prefix -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') {
    throw 'Prefix must contain only letters, numbers, underscore or hyphen'
}
$query = Join-Path $PSScriptRoot 'Run-ArchiveQuery.ps1'

# Get the *bytes* around the otherwise undisassembled command producer:
# the return address from the Frida event is 0058CF96.
& $query -Report "$Prefix-37.txt" -Addresses @(
    'bytes:0058CEF0:256',
    'bytes:0058CFF0:80',
    'decode:0058CF00:85',
    'decode:0058CF50:80',
    'decode:0058CF91:25',
    'xref:0058CF96'
)
if ($LASTEXITCODE -ne 0) { throw "Raw 0x37 query failed: $LASTEXITCODE" }

# The 86 command is a wrapper that forwards its second parameter unmodified;
# its Ghidra result is insufficient to say what produced bits 16..31.
# Request the principal callers and their return paths. We need to identify
# which of them sent the value 0x06350445 in the trace.
& $query -Report "$Prefix-86.txt" -Addresses @(
    '005747E0',
    '00579790',
    '0059D370',
    '005B8210',
    '0059C8D0',
    'ins:00579790:50',
    'ins:0059D460:80',
    'ref:005747E0'
)
if ($LASTEXITCODE -ne 0) { throw "0x86 caller query failed: $LASTEXITCODE" }

Write-Host "Read-only evidence:"
Write-Host "  docs/evidence/$Prefix-37.txt"
Write-Host "  docs/evidence/$Prefix-86.txt"
Write-Host 'A byte dump alone does not prove which instruction supplied the high word.'
