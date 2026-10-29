param([string]$GameDirectory = 'Z:\YugiRE\passion', [string]$OutputDirectory = 'Z:\YugiRE\YugiRE\docs\evidence')
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$files = foreach ($name in 'joey_pc.exe','data.dat','Voice.dat','Region.dat','deck.ydc') {
    $path = Join-Path $GameDirectory $name
    $stream = [IO.File]::OpenRead($path)
    try {
        $buffer = New-Object byte[] 256
        $count = $stream.Read($buffer,0,$buffer.Length)
        [ordered]@{name=$name; size=$stream.Length; sha256=(Get-FileHash $path -Algorithm SHA256).Hash; first_bytes=[Convert]::ToHexString($buffer,0,$count); first_u32_le=@(for($i=0;$i+4 -le $count;$i+=4){[BitConverter]::ToUInt32($buffer,$i)})}
    } finally { $stream.Dispose() }
}
$b = [IO.File]::ReadAllBytes((Join-Path $GameDirectory 'joey_pc.exe'))
function U16($p) { [BitConverter]::ToUInt16($b,$p) }
function U32($p) { [BitConverter]::ToUInt32($b,$p) }
function Str($p) { $end=$p; while($end -lt $b.Length -and $b[$end] -ne 0){$end++}; [Text.Encoding]::ASCII.GetString($b,$p,$end-$p) }
$pe=U32 0x3c; $coff=$pe+4; $opt=$coff+20
$sections = @(for($i=0;$i -lt (U16 ($coff+2));$i++) {
    $p=$opt+(U16 ($coff+16))+40*$i
    $raw=U32 ($p+20); $size=U32 ($p+16)
    $freq=New-Object int[] 256
    for($j=$raw;$j -lt $raw+$size;$j++){$freq[$b[$j]]++}
    $entropy=0.0; foreach($n in $freq){if($n){$q=$n/[double]$size;$entropy-=$q*[Math]::Log($q,2)}}
    [ordered]@{name=[Text.Encoding]::ASCII.GetString($b,$p,8).Trim([char]0);rva=(U32 ($p+12));virtual_size=(U32 ($p+8));raw_offset=$raw;raw_size=$size;characteristics=('{0:X8}' -f (U32 ($p+36)));entropy=$entropy}
})
function Off($rva){foreach($s in $sections){if($rva -ge $s.rva -and $rva -lt $s.rva+[Math]::Max($s.virtual_size,$s.raw_size)){return [int]($s.raw_offset+$rva-$s.rva)}};throw "Unmapped RVA $rva"}
$imports=@();$irva=U32 ($opt+104)
if($irva){$p=Off $irva;while((U32 ($p+12)) -ne 0){$dll=Str (Off (U32 ($p+12)));$thunk=U32 $p;if(!$thunk){$thunk=U32 ($p+16)};$t=Off $thunk;$names=@();$k=0;while(($v=U32 ($t+4*$k)) -ne 0){$names+=if($v -band 0x80000000){"ordinal:$($v -band 0xffff)"}else{Str ((Off $v)+2)};$k++};$imports += [ordered]@{dll=$dll;iat_rva=(U32 ($p+16));symbols=$names};$p+=20}}
$directories=@(for($i=0;$i -lt 16;$i++){[ordered]@{index=$i;rva=(U32 ($opt+96+8*$i));size=(U32 ($opt+100+8*$i))}})
$report=[ordered]@{files=$files;pe=[ordered]@{pe_offset=$pe;signature=[Convert]::ToHexString($b,$pe,4);machine=('{0:X4}' -f (U16 $coff));timestamp=(U32 ($coff+4));optional_magic=('{0:X4}' -f (U16 $opt));linker="$($b[$opt+2]).$($b[$opt+3])";image_base=(U32 ($opt+28));entry_rva=(U32 ($opt+16));subsystem=(U16 ($opt+68));dll_characteristics=(U16 ($opt+70));sections=$sections;directories=$directories;imports=$imports}}
$report | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $OutputDirectory 'initial-inspection.json') -Encoding utf8
$ascii=[Text.Encoding]::ASCII.GetString($b)
$strings=foreach($m in [regex]::Matches($ascii,'[\x20-\x7e]{5,}')){[ordered]@{file_offset=$m.Index;text=$m.Value}}
$strings | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $OutputDirectory 'exe-ascii-strings.json') -Encoding utf8
$resourceBase = Off (U32 ($opt+112))
function Read-ResourceTree($relative, $parts) {
    $p=$resourceBase+$relative
    $count=(U16 ($p+12))+(U16 ($p+14))
    for($i=0;$i -lt $count;$i++) {
        $e=$p+16+8*$i; $id=U32 $e; $target=U32 ($e+4)
        $label=if($id -band 0x80000000){$n=$resourceBase+($id -band 0x7fffffff);[Text.Encoding]::Unicode.GetString($b,$n+2,2*(U16 $n))}else{"$id"}
        $next=@($parts)+$label
        if($target -band 0x80000000){Read-ResourceTree ($target -band 0x7fffffff) $next}
        else{$d=$resourceBase+$target;[ordered]@{path=($next -join '/');data_rva=(U32 $d);size=(U32 ($d+4));codepage=(U32 ($d+8))}}
    }
}
@(Read-ResourceTree 0 @()) | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDirectory 'pe-resources.json') -Encoding utf8
$report.pe | ConvertTo-Json -Depth 10
