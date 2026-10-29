$ErrorActionPreference='Stop'
Import-Module "$PSScriptRoot\ArchiveIndex.psm1" -Force
Import-Module "$PSScriptRoot\PocLzss.psm1" -Force
$repository=Split-Path $PSScriptRoot -Parent
$path=Join-Path (Split-Path $repository -Parent) 'passion\data.dat'
$before=(Get-FileHash $path -Algorithm SHA256).Hash
$tables=@{}
$stream=[IO.File]::OpenRead($path)
try {
    $index=Read-ArchiveIndex $stream
    foreach($name in 'bin#\card_id.bin','bin#\card_intid.bin') {
        $entries=@($index.entries|Where-Object name -CEQ $name)
        if($entries.Count -ne 1){throw "Expected one resource: $name"}
        $entry=$entries[0]
        if($entry.size1 -gt 8192 -or $entry.size2 -gt 8192){throw 'Experiment exceeds limits'}
        $stream.Position=$entry.offset
        $inputBytes=New-Object byte[] $entry.size2
        $stream.ReadExactly($inputBytes,0,$inputBytes.Length)
        if($entry.size1 -ne $entry.size2) {
            $result=Expand-PocLzss -InputBytes $inputBytes -OutputSize $entry.size1
            if($result.trailing_bytes){throw 'Unexpected trailing data'}
            $bytes=$result.bytes
            $consumed=$result.consumed
        } else {$bytes=$inputBytes;$consumed=$inputBytes.Length}
        if([Text.Encoding]::ASCII.GetString($bytes,0,4) -ceq 'YPK1'){throw 'YPK1 not supported in this experiment'}
        if($bytes.Length % 2){throw 'Odd-sized uint16 table'}
        $words=@(for($i=0;$i -lt $bytes.Length;$i+=2){[BitConverter]::ToUInt16($bytes,$i)})
        $tables[$name]=[pscustomobject]@{entry=$entry;words=$words;consumed=$consumed;produced=$bytes.Length;sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes));first48_hex=[Convert]::ToHexString($bytes,0,48)}
    }
} finally {$stream.Dispose()}
$forward=$tables['bin#\card_id.bin'].words
$reverse=$tables['bin#\card_intid.bin'].words
$checks=@(for($i=0;$i -lt $forward.Count;$i++) {
    $id=[int]$forward[$i]
    $slot=$id % 2000
    $valid=$id -lt 4000 -and $slot -lt $reverse.Count
    $mapped=if($valid){[int]$reverse[$slot]+[int]($id -ge 2000)}else{$null}
    [pscustomobject]@{property_index=$i;id=$id;reverse_slot=$slot;reverse_base=$(if($valid){$reverse[$slot]}else{$null});mapped_index=$mapped;matches=($valid -and $mapped -eq $i)}
})
$pairs=@(foreach($check in $checks|Where-Object {$_.id -ge 2000 -and $_.id -lt 4000}) {
    $baseIndex=[int]$check.reverse_base
    [pscustomobject]@{base_id=($check.id-2000);base_index=$baseIndex;forward_at_base=$forward[$baseIndex];variant_id=$check.id;variant_index=$check.property_index;adjacent=($check.property_index -eq $baseIndex+1);base_matches=($forward[$baseIndex] -eq $check.id-2000)}
})
$reverseChecks=@(for($i=0;$i -lt 2000;$i++) {
    if($reverse[$i] -ne 0) {
        $mapped=[int]$reverse[$i]
        [pscustomobject]@{id=$i;property_index=$mapped;matches=($mapped -lt $forward.Count -and $forward[$mapped] -eq $i)}
    }
})
$validFailures=@($checks|Where-Object {$_.id -lt 4000 -and !$_.matches})
if($validFailures.Count -or @($reverseChecks|Where-Object {!$_.matches}).Count -or @($pairs|Where-Object {!$_.adjacent -or !$_.base_matches}).Count){throw 'Table relation failed'}
if($forward[0] -ne 65535){throw 'Unexpected sentinel'}
$summaries=@(foreach($name in 'bin#\card_id.bin','bin#\card_intid.bin') {
    $t=$tables[$name]
    [pscustomobject]@{name=$name;entry=$t.entry;count=$t.words.Count;consumed=$t.consumed;produced=$t.produced;decoded_sha256=$t.sha256;first48_hex=$t.first48_hex;min=($t.words|Measure-Object -Minimum).Minimum;max=($t.words|Measure-Object -Maximum).Maximum;zero_count=@($t.words|Where-Object {$_ -eq 0}).Count;sentinel_ffff_count=@($t.words|Where-Object {$_ -eq 65535}).Count;unique_count=@($t.words|Sort-Object -Unique).Count;first24=@($t.words|Select-Object -First 24);last8=@($t.words|Select-Object -Last 8)}
})
$after=(Get-FileHash $path -Algorithm SHA256).Hash
if($before -ne $after){throw 'Original archive changed'}
$report=[ordered]@{archive_sha256_before=$before;archive_sha256_after=$after;tables=$summaries;forward_valid_ids=@($checks|Where-Object {$_.id -lt 4000}).Count;ids_ge_2000=@($checks|Where-Object {$_.id -ge 2000 -and $_.id -lt 4000});variant_pairs=$pairs;reverse_nonzero_0_1999=$reverseChecks.Count;reverse_roundtrip_failures=@($reverseChecks|Where-Object {!$_.matches});roundtrip_matches=@($checks|Where-Object matches).Count;roundtrip_mismatches=@($checks|Where-Object {!$_.matches});first24_roundtrips=@($checks|Select-Object -First 24);unused_reverse_0_1999=@(for($i=0;$i -lt [Math]::Min(2000,$reverse.Count);$i++){if($reverse[$i] -eq 0){$i}}).Count}
$report|ConvertTo-Json -Depth 8|Set-Content "$repository\docs\evidence\card-id-validation.json" -Encoding utf8
$report|ConvertTo-Json -Depth 8
