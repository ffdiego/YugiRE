$ErrorActionPreference='Stop'
Import-Module "$PSScriptRoot\ArchiveIndex.psm1" -Force
Import-Module "$PSScriptRoot\PocLzss.psm1" -Force
$repository=Split-Path $PSScriptRoot -Parent
$workspace=Split-Path $repository -Parent
$path=Join-Path $workspace 'passion\data.dat'
$before=(Get-FileHash $path -Algorithm SHA256).Hash
$stream=[IO.File]::OpenRead($path)
try {
    $index=Read-ArchiveIndex $stream
    $matches=@($index.entries|Where-Object name -CEQ 'bin#\card_prop.bin')
    if($matches.Count -ne 1){throw 'Expected one property resource'}
    $entry=$matches[0]
    if($entry.size1 -gt 8192 -or $entry.size2 -gt 8192){throw 'Experiment limit exceeded'}
    $stream.Position=$entry.offset
    $source=New-Object byte[] $entry.size2
    $stream.ReadExactly($source,0,$source.Length)
} finally {$stream.Dispose()}
$decoded=Expand-PocLzss -InputBytes $source -OutputSize $entry.size1
if($decoded.trailing_bytes -ne 0){throw 'Unexpected trailing data'}
$ypk1=$decoded.bytes.Length -ge 4 -and [Text.Encoding]::ASCII.GetString($decoded.bytes,0,4) -ceq 'YPK1'
if($ypk1){throw 'YPK1 requires separate investigation'}
if($decoded.bytes.Length % 4){throw 'Property data is not a whole number of uint32 records'}
$rows=@(for($i=0;$i -lt $decoded.bytes.Length/4;$i++){
    $word=[BitConverter]::ToUInt32($decoded.bytes,4*$i)
    [pscustomobject]@{index=$i;raw=('{0:X8}' -f $word);bits_0_8=($word -band 0x1ff);bits_9_17=(($word -shr 9) -band 0x1ff);bits_18_19=(($word -shr 18) -band 3);bits_20_24=(($word -shr 20) -band 31);bits_25_28=(($word -shr 25) -band 15);bits_29_31=($word -shr 29)}
})
$after=(Get-FileHash $path -Algorithm SHA256).Hash
if($before -ne $after){throw 'Original archive changed'}
$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($decoded.bytes))
$report=[ordered]@{archive_sha256_before=$before;archive_sha256_after=$after;entry=$entry;consumed=$decoded.consumed;produced=$decoded.produced;trailing_bytes=$decoded.trailing_bytes;ypk1=$ypk1;record_count=$rows.Count;decoded_sha256=$hash;first64_hex=[Convert]::ToHexString($decoded.bytes,0,64);first16_records=@($rows|Select-Object -First 16);class_counts=@($rows|Group-Object bits_20_24|Sort-Object {[int]$_.Name}|Select-Object Name,Count)}
$report|ConvertTo-Json -Depth 7|Set-Content "$repository\docs\evidence\card-property-validation.json" -Encoding utf8
$report|ConvertTo-Json -Depth 7
