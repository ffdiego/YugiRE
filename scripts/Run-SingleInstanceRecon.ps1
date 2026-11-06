<# 
  Read-only Ghidra reconnaissance into Joey's single-instance enforcement.
  Investigates named mutexes, windows, events and file locks.
  Does not change the EXE or bypass inter-process synchronization.
#>
param([string]$Prefix = "single-instance")
$ErrorActionPreference = "Stop"
if ($Prefix -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') {
    throw 'Prefix must contain only letters, numbers, underscores or hyphens'
}
$query = Join-Path $PSScriptRoot 'Run-ArchiveQuery.ps1'

# Windows single-instance checks often use a named mutex and ERROR_ALREADY_EXISTS.
# The imports themselves are evidence of capability, not proof of usage.
& $query -Report "$Prefix-mutex.txt" -Addresses @(
    'symbol:CreateMutexA',
    'symbol:CreateMutexW',
    'symbol:OpenMutexA',
    'symbol:OpenMutexW',
    'symbol:ReleaseMutex',
    'symbol:CloseHandle'
)
if ($LASTEXITCODE -ne 0) { throw "Mutex query failed: $LASTEXITCODE" }

# Some Delphi/older Windows games check a registered window instead.
& $query -Report "$Prefix-windows.txt" -Addresses @(
    'symbol:FindWindowA',
    'symbol:FindWindowW',
    'symbol:FindWindowExA',
    'symbol:FindWindowExW',
    'symbol:RegisterClassA',
    'symbol:RegisterClassW',
    'symbol:CreateEventA',
    'symbol:CreateEventW'
)
if ($LASTEXITCODE -ne 0) { throw "Window/event query failed: $LASTEXITCODE" }

# Check error/status and alternative lock-based approaches separately,
# to avoid mixing hundreds of callsite owners with the mutex report.
& $query -Report "$Prefix-locks.txt" -Addresses @(
    'symbol:GetLastError',
    'symbol:CreateFileA',
    'symbol:CreateFileW',
    'symbol:LockFile',
    'symbol:LockFileEx'
)
if ($LASTEXITCODE -ne 0) { throw "Lock query failed: $LASTEXITCODE" }

Write-Host "Saved read-only reports to docs/evidence/$Prefix-*.txt"
