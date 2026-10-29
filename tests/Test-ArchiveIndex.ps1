$ErrorActionPreference='Stop'
Import-Module "$PSScriptRoot\..\scripts\ArchiveIndex.psm1" -Force
function Fixture {
    $bytes=New-Object byte[] 284
    [Text.Encoding]::ASCII.GetBytes('KCEJYUGI').CopyTo($bytes,0)
    [BitConverter]::GetBytes([uint32]1).CopyTo($bytes,8)
    # Independent encoded fixture: a.wav -> 16 E2 77 16 67
    ([byte[]]@(0x16,0xe2,0x77,0x16,0x67)).CopyTo($bytes,12)
    [BitConverter]::GetBytes([uint32]280).CopyTo($bytes,268)
    [BitConverter]::GetBytes([uint32]4).CopyTo($bytes,272)
    [BitConverter]::GetBytes([uint32]4).CopyTo($bytes,276)
    return ,$bytes
}
function Parse($bytes){$stream=[IO.MemoryStream]::new([byte[]]$bytes);try{Read-ArchiveIndex $stream}finally{$stream.Dispose()}}
function Reject($bytes){$rejected=$false;try{$null=Parse $bytes}catch{$rejected=$true};if(!$rejected){throw 'Expected rejection'}}
$valid=Parse (Fixture)
if($valid.count -ne 1 -or $valid.table_end -ne 280 -or $valid.entries[0].name -cne 'a.wav' -or $valid.entries[0].offset -ne 280){throw 'Fixture mismatch'}
$b=Fixture;$b[0]=0;Reject $b
$b=Fixture;[BitConverter]::GetBytes([uint32]2).CopyTo($b,8);Reject $b
$b=Fixture;[BitConverter]::GetBytes([uint32]279).CopyTo($b,268);Reject $b
$b=Fixture;[BitConverter]::GetBytes([uint32]5).CopyTo($b,276);Reject $b
$b=Fixture;for($i=12;$i -lt 268;$i++){$b[$i]=1};Reject $b
'PASS: known name/record, signature, truncated index, overlap, payload bounds, unterminated name'
