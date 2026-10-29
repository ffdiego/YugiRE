$ErrorActionPreference='Stop'
Import-Module "$PSScriptRoot\ArchiveIndex.psm1" -Force
Import-Module "$PSScriptRoot\PocLzss.psm1" -Force
$repository=Split-Path $PSScriptRoot -Parent
$workspace=Split-Path $repository -Parent
$archive=Join-Path $workspace 'passion\data.dat'
$evidence=Join-Path $repository 'docs\evidence'
$before=(Get-FileHash $archive -Algorithm SHA256).Hash
$stream=[IO.File]::OpenRead($archive)
try {
    $index=Read-ArchiveIndex $stream
    $entry=@($index.entries | Where-Object name -CEQ 'j\deck_c\line.txt')
    if($entry.Count -ne 1){throw 'Expected one sample entry'}
    $entry=$entry[0]
    if($entry.size2 -gt 4096 -or $entry.size1 -gt 4096){throw 'Sample exceeds experiment limits'}
    $stream.Position=$entry.offset
    $compressed=New-Object byte[] $entry.size2
    $stream.ReadExactly($compressed,0,$compressed.Length)
} finally {$stream.Dispose()}
$result=Expand-PocLzss -InputBytes $compressed -OutputSize $entry.size1
if($result.produced -ne $entry.size1 -or $result.consumed -ne $entry.size2){throw 'Sample size mismatch'}
$output=Join-Path $evidence 'lzss-line-output.bin'
[IO.File]::WriteAllBytes($output,$result.bytes)
$after=(Get-FileHash $archive -Algorithm SHA256).Hash
if($before -ne $after){throw 'Original archive changed'}
$report=[ordered]@{archive_sha256_before=$before;archive_sha256_after=$after;entry=$entry;compressed_hex=[Convert]::ToHexString($compressed);output_hex=[Convert]::ToHexString($result.bytes);output_ascii=[Text.Encoding]::ASCII.GetString($result.bytes);output_sha256=(Get-FileHash $output -Algorithm SHA256).Hash;consumed=$result.consumed;produced=$result.produced;trailing_bytes=$result.trailing_bytes;literals=$result.literals;references=$result.references;reference_bytes=$result.reference_bytes}
$report|ConvertTo-Json -Depth 5|Set-Content (Join-Path $evidence 'lzss-small-validation.json') -Encoding utf8
$report|ConvertTo-Json -Depth 5
