function Read-ArchiveIndex {
    param([Parameter(Mandatory)][System.IO.Stream]$Stream)
    $header=New-Object byte[] 12
    $Stream.Position=0
    $Stream.ReadExactly($header,0,12)
    if([Text.Encoding]::ASCII.GetString($header,0,8) -cne 'KCEJYUGI'){throw 'Invalid archive signature'}
    $count=[BitConverter]::ToUInt32($header,8)
    $tableEnd=12L+268L*$count
    if($tableEnd -gt $Stream.Length){throw 'Index exceeds archive length'}
    $entries=@(for($i=0;$i -lt $count;$i++) {
        $record=New-Object byte[] 268
        $Stream.ReadExactly($record,0,268)
        $end=[Array]::IndexOf($record,[byte]0,0,256)
        if($end -lt 0){throw "Unterminated name in entry $i"}
        $decoded=New-Object byte[] $end
        for($j=0;$j -lt $end;$j++){$v=$record[$j];$decoded[$j]=($v -shr 4) -bor (($v -band 15) -shl 4)}
        $offset=[BitConverter]::ToUInt32($record,256)
        $size1=[BitConverter]::ToUInt32($record,260)
        $size2=[BitConverter]::ToUInt32($record,264)
        if($offset -lt $tableEnd -or ([long]$offset+$size2) -gt $Stream.Length){throw "Invalid payload range in entry $i"}
        [pscustomobject]@{index=$i;name=[Text.Encoding]::ASCII.GetString($decoded);offset=$offset;size1=$size1;size2=$size2}
    })
    [pscustomobject]@{count=$count;table_end=$tableEnd;entries=$entries}
}
Export-ModuleMember -Function Read-ArchiveIndex
