function Read-DeckYdc {
    param([Parameter(Mandatory)][byte[]]$Bytes)
    if($Bytes.Length -lt 14){throw 'Truncated eight-byte header or three list counts'}
    $position=8
    $groups=@(for($g=0;$g -lt 3;$g++) {
        if($position+2 -gt $Bytes.Length){throw 'Truncated list count'}
        $countOffset=$position
        $count=[BitConverter]::ToUInt16($Bytes,$position);$position+=2
        $limit=if($g -eq 0){80}else{15}
        if($count -gt $limit){throw 'List exceeds recognized layout capacity'}
        if($position+2*$count -gt $Bytes.Length){throw 'Truncated card IDs'}
        $ids=@(for($i=0;$i -lt $count;$i++){[BitConverter]::ToUInt16($Bytes,$position+2*$i)})
        $position+=2*$count
        [pscustomobject]@{list=$g;count_offset=$countOffset;count=$count;ids=$ids}
    })
    if($position -ne $Bytes.Length){throw 'Unexpected trailing data in recognized layout'}
    [pscustomobject]@{header_hex=[Convert]::ToHexString($Bytes,0,8);header_u32_0=[BitConverter]::ToUInt32($Bytes,0);header_u32_4=[BitConverter]::ToUInt32($Bytes,4);groups=$groups;consumed=$position}
}
Export-ModuleMember -Function Read-DeckYdc
