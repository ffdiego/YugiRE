param([string]$GameDirectory='Z:\YugiRE\passion',[string]$OutputDirectory='Z:\YugiRE\YugiRE\docs\evidence')
$ErrorActionPreference='Stop'
Import-Module "$PSScriptRoot\ArchiveIndex.psm1" -Force
$reports=foreach($name in 'data.dat','Voice.dat') {
    $stream=[IO.File]::OpenRead((Join-Path $GameDirectory $name))
    try {
        $index=Read-ArchiveIndex $stream
        $expected=$index.table_end
        $contiguous=$true
        foreach($entry in $index.entries){if($entry.offset -ne $expected){$contiguous=$false};$expected=[long]$entry.offset+$entry.size2}
        $samples=@(foreach($entry in ($index.entries | Select-Object -First 2)) {
            $stream.Position=$entry.offset
            $bytes=New-Object byte[] ([Math]::Min(32,$entry.size2))
            if($bytes.Length){$stream.ReadExactly($bytes,0,$bytes.Length)}
            [pscustomobject]@{entry=$entry;first_bytes=[Convert]::ToHexString($bytes)}
        })
        $index.entries | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $OutputDirectory "$name-index.json") -Encoding utf8
        [pscustomobject]@{file=$name;count=$index.count;table_end=$index.table_end;all_ranges_valid=$true;contiguous=$contiguous;final_payload_end=$expected;file_size=$stream.Length;samples=$samples}
    } finally {$stream.Dispose()}
}
$reports | ConvertTo-Json -Depth 7 | Set-Content (Join-Path $OutputDirectory 'archive-index-validation.json') -Encoding utf8
$reports | Select-Object file,count,table_end,contiguous,final_payload_end,file_size
