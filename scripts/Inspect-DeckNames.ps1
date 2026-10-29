$ErrorActionPreference='Stop'
Import-Module "$PSScriptRoot\ArchiveIndex.psm1" -Force
Import-Module "$PSScriptRoot\PocLzss.psm1" -Force
Import-Module "$PSScriptRoot\DeckYdc.psm1" -Force
$repository=Split-Path $PSScriptRoot -Parent
$game=Join-Path (Split-Path $repository -Parent) 'passion'
$archive=Join-Path $game 'data.dat';$deckPath=Join-Path $game 'deck.ydc'
$before=(Get-FileHash $archive -Algorithm SHA256).Hash
$deckHash=(Get-FileHash $deckPath -Algorithm SHA256).Hash
$exePath=Join-Path $game 'joey_pc.exe'
$exeHash=(Get-FileHash $exePath -Algorithm SHA256).Hash
if($exeHash -ne 'DB8FE7B7EA4B12F18A9BB1777B5F5B8EAB254011DB2A52571A0264725505B6DE'){throw 'Executable fingerprint differs; table offset must be reviewed'}
$exeBytes=[IO.File]::ReadAllBytes($exePath)
$namePointer=[BitConverter]::ToUInt32($exeBytes,0x1eb444)
if($namePointer -ne 0x5eb4c0){throw 'Name path table pointer differs'}
$resources=@{};$metadata=@()
$stream=[IO.File]::OpenRead($archive)
try {
    $index=Read-ArchiveIndex $stream
    foreach($name in 'bin#\card_id.bin','bin#\card_intid.bin','bin#\card_nameeng.bin') {
        $entries=@($index.entries|Where-Object name -CEQ $name)
        if($entries.Count -ne 1){throw "Expected one resource $name"}
        $entry=$entries[0]
        if($entry.size1 -gt 131072 -or $entry.size2 -gt 32768){throw 'Experiment size limit exceeded'}
        $stream.Position=$entry.offset
        $source=New-Object byte[] $entry.size2;$stream.ReadExactly($source,0,$source.Length)
        $consumed=$source.Length
        if($entry.size1 -ne $entry.size2){$r=Expand-PocLzss -InputBytes $source -OutputSize $entry.size1;if($r.trailing_bytes){throw 'Trailing compressed bytes'};$bytes=$r.bytes;$consumed=$r.consumed}else{$bytes=$source}
        if([Text.Encoding]::ASCII.GetString($bytes,0,4) -ceq 'YPK1'){throw 'Unexpected YPK1'}
        $resources[$name]=$bytes
        $metadata+=[pscustomobject]@{entry=$entry;consumed=$consumed;produced=$bytes.Length;sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))}
    }
} finally {$stream.Dispose()}
$forward=$resources['bin#\card_id.bin'];$reverse=$resources['bin#\card_intid.bin'];$names=$resources['bin#\card_nameeng.bin']
if($names.Length % 64){throw 'Name table not aligned to 64-byte records'}
function NameAt([int]$Index) {
    if($Index -lt 0 -or ($Index+1)*64 -gt $names.Length){throw 'Invalid name index'}
    $offset=$Index*64;$end=[Array]::IndexOf($names,[byte]0,$offset,64)
    if($end -lt 0){throw 'Unterminated name'}
    [Text.Encoding]::ASCII.GetString($names,$offset,$end-$offset)
}
function MapId([int]$Id) {
    if($Id -ge 4000){throw 'ID outside conversion domain'}
    $mapped=[int][BitConverter]::ToUInt16($reverse,($Id%2000)*2)+[int]($Id -ge 2000)
    if(($mapped+1)*2 -gt $forward.Length -or [BitConverter]::ToUInt16($forward,2*$mapped) -ne $Id){throw "ID not present: $Id"}
    [pscustomobject]@{id=$Id;property_index=$mapped;name=(NameAt $mapped)}
}
$deck=Read-DeckYdc ([IO.File]::ReadAllBytes($deckPath))
$groups=@(foreach($group in $deck.groups){[pscustomobject]@{list=$group.list;count_offset=$group.count_offset;count=$group.count;cards=@(foreach($id in $group.ids){MapId $id})}})
$sampleIndices=@(1,2,3,31,32,97,98,816,817)
$samples=@(foreach($i in $sampleIndices){[pscustomobject]@{property_index=$i;id=[BitConverter]::ToUInt16($forward,2*$i);name=(NameAt $i)}})
$after=(Get-FileHash $archive -Algorithm SHA256).Hash
if($before -ne $after -or $deckHash -ne (Get-FileHash $deckPath -Algorithm SHA256).Hash){throw 'Original changed'}
$report=[ordered]@{archive_sha256_before=$before;archive_sha256_after=$after;deck_sha256=$deckHash;exe_sha256=$exeHash;name_path_table=[ordered]@{va='0x005EB444';file_offset='0x001EB444';first_pointer=('0x{0:X8}' -f $namePointer);first_string=[Text.Encoding]::ASCII.GetString($exeBytes,0x1eb4c0,19)};resources=$metadata;name_record_count=($names.Length/64);name_samples=$samples;constant_id_samples=@((MapId 762),(MapId 1091));header_hex=$deck.header_hex;consumed=$deck.consumed;groups=$groups;all_card_ids_roundtrip=$true}
$report|ConvertTo-Json -Depth 9|Set-Content "$repository\docs\evidence\deck-name-validation.json" -Encoding utf8
$report|ConvertTo-Json -Depth 9
