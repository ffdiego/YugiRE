[CmdletBinding()]
param(
    [int]$TargetProcessId = 0,
    [string]$OutputPath
)

# Read-only Windows probe for the executable fingerprint studied in docs/26.
# Requires an already-running game; never launches, pauses or writes to it.
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Run this script on Windows with joey_pc.exe already open in a duel.'
}
if ($TargetProcessId -eq 0) {
    $targets = @(Get-Process -Name 'joey_pc' -ErrorAction SilentlyContinue)
    if ($targets.Count -ne 1) { throw 'Expected one joey_pc process. Select one with -TargetProcessId.' }
    $target = $targets[0]
} else {
    $target = Get-Process -Id $TargetProcessId
}
$module = $target.MainModule
$expectedHash = 'DB8FE7B7EA4B12F18A9BB1777B5F5B8EAB254011DB2A52571A0264725505B6DE'
$actualHash = (Get-FileHash -LiteralPath $module.FileName -Algorithm SHA256).Hash
if ($actualHash -ne $expectedHash) { throw 'Executable fingerprint differs: the documented addresses may not apply.' }

if (-not ('JoeyRE.SnapshotNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace JoeyRE {
    public static class SnapshotNative {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadProcessMemory(IntPtr process, IntPtr address,
            [Out] byte[] buffer, UIntPtr size, out UIntPtr bytesRead);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr handle);
        public static byte[] ReadBlock(IntPtr process, long address, int count) {
            byte[] buffer = new byte[count];
            UIntPtr received;
            if (!ReadProcessMemory(process, new IntPtr(address), buffer, new UIntPtr((uint)count), out received)
                || received.ToUInt64() != (ulong)count)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ReadProcessMemory failed or returned a partial block");
            return buffer;
        }
    }
}
'@
}
$handle = [JoeyRE.SnapshotNative]::OpenProcess(0x1010, $false, $target.Id) # QUERY_LIMITED_INFORMATION | VM_READ
if ($handle -eq [IntPtr]::Zero) {
    throw "OpenProcess failed: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
}
try {
    $imageBase = $module.BaseAddress.ToInt64()
    $rva = 0x00A55D64 - 0x00400000
    $stride = 0xD44
    [byte[]]$bytes = [JoeyRE.SnapshotNative]::ReadBlock($handle, $imageBase + $rva, $stride * 2)
    $players = @(for ($side = 0; $side -lt 2; $side++) {
        $start = $side * $stride
        [uint32]$playerFlags = [BitConverter]::ToUInt32($bytes, $start + (0xA56430 - 0xA55D64))
        $monsters = @(for ($zone = 0; $zone -lt 5; $zone++) {
            $record = $start + 0x10 + $zone * 0x90
            [uint32]$flags = [BitConverter]::ToUInt32($bytes, $record + 0x8C)
            $index = [BitConverter]::ToUInt32($bytes, $record) -band 0xFFF
            [pscustomobject][ordered]@{
                zone = $zone
                card_index = $index
                occupied = $index -ne 0
                occurrence_word = [BitConverter]::ToUInt16($bytes, $record + 4)
                position_word_hex = '{0:X4}' -f [BitConverter]::ToUInt16($bytes, $record + 6)
                flags_hex = '{0:X8}' -f $flags
                position_locked_this_turn = $index -ne 0 -and ($flags -band 0x20000) -ne 0
            }
        })
        [pscustomobject][ordered]@{
            side = $side
            life_points = [BitConverter]::ToUInt16($bytes, $start)
            hand_count = [int]$bytes[$start + 4]
            player_flags_hex = '{0:X8}' -f $playerFlags
            monsters = $monsters
        }
    })
    $snapshot = [pscustomobject][ordered]@{
        captured_utc = [DateTime]::UtcNow.ToString('o')
        process_id = $target.Id
        executable_sha256 = $actualHash
        image_base_hex = '{0:X8}' -f $imageBase
        limitation = 'One read of a live process, not an atomic paused snapshot. Card indices are not resource IDs.'
        players = $players
    }
    $json = $snapshot | ConvertTo-Json -Depth 8
    if ($OutputPath) {
        $resolved = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
        $file = [IO.File]::Open($resolved, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
        try {
            $encoding = New-Object Text.UTF8Encoding($false)
            $encoded = $encoding.GetBytes($json + [Environment]::NewLine)
            $file.Write($encoded, 0, $encoded.Length)
        } finally { $file.Dispose() }
    }
    $json
} finally {
    [void][JoeyRE.SnapshotNative]::CloseHandle($handle)
}
